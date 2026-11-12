using System.IO.Compression;
using System.Text.Json;

namespace ResearchCandidates;

// EXP-016: fixed overnight screen. Research simulation only; no order routing.
internal static class Exp016OvernightDrift
{
    private const double CommissionPerMillion = 30;
    private const double SlippagePerSide = 0.02;
    private const int HeldBars = 171;

    private record Bar(DateTimeOffset Time, double BidOpen, double BidHigh, double BidLow,
        double BidClose, double AskOpen, double AskHigh, double AskLow, double AskClose)
    {
        public double MidOpen => (BidOpen + AskOpen) / 2;
    }

    private record Fill(DateTimeOffset Time, string Reason, double Price);

    public static void Run(string[] args)
    {
        if (args.Length is < 3 or > 4)
            throw new ArgumentException("exp016 <evtl_directory|m5_bid_ask.csv> <out_prefix> [end_year: 2023|2025]");
        var endYear = args.Length == 4 ? int.Parse(args[3]) : 2023;
        if (endYear is not (2023 or 2025)) throw new ArgumentException("end_year must be 2023 or 2025");
        var bars = LoadBars(args[1], endYear);
        var byTime = bars.ToDictionary(b => b.Time);
        var rows = new List<string> { "ny_date,period,status,reason,entry_utc,scheduled_exit_utc,raw_midpoint_move_to_0820,long_entry_ask,long_stop,long_exit_bid,long_exit_utc,long_exit_reason,long_gross_after_spread,long_commission,long_slippage,long_net,short_entry_bid,short_stop,short_exit_ask,short_exit_utc,short_exit_reason,short_gross_after_spread,short_commission,short_slippage,short_net" };
        var results = new List<(DateTime Date, double LongNet, double ShortNet)>();
        for (var date = new DateTime(2018, 1, 1); date <= new DateTime(endYear, 12, 31); date = date.AddDays(1))
        {
            if (date.DayOfWeek is not (DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday))
                continue;
            var period = date.Year <= 2023 ? "2018-2023" : "2024-2025";
            var window = GetWindow(date, byTime);
            if (window == null)
            {
                rows.Add(Record(date, period, "skip", "incomplete_171_bar_or_exit_window"));
                continue;
            }
            var (held, exitBar) = window.Value;
            var entry = held[0];
            var longStop = entry.AskOpen * .99;
            var shortStop = entry.BidOpen * 1.01;
            if (entry.BidOpen <= longStop)
            {
                rows.Add(Record(date, period, "skip", "long_stop_crossed_at_entry",
                    entry.Time.ToString("O"), exitBar.Time.ToString("O")));
                continue;
            }
            var longFill = LongExit(held, exitBar, longStop);
            var longGross = longFill.Price - entry.AskOpen;
            var longFee = Fee(entry.AskOpen, longFill.Price);
            var longNet = longGross - longFee - 2 * SlippagePerSide;
            var shortValid = entry.AskOpen < shortStop;
            Fill? shortFill = shortValid ? ShortExit(held, exitBar, shortStop) : null;
            var shortGross = shortFill == null ? (double?)null : entry.BidOpen - shortFill.Price;
            var shortFee = shortFill == null ? (double?)null : Fee(entry.BidOpen, shortFill.Price);
            var shortNet = shortGross - shortFee - (shortFill == null ? 0 : 2 * SlippagePerSide);
            if (shortNet.HasValue) results.Add((date, longNet, shortNet.Value));
            rows.Add(Record(date, period, "trade", shortValid ? "complete" : "short_control_stop_crossed_at_entry",
                entry.Time.ToString("O"), exitBar.Time.ToString("O"),
                exitBar.MidOpen - entry.MidOpen,
                entry.AskOpen, longStop, longFill.Price, longFill.Time.ToString("O"),
                longFill.Reason, longGross, longFee, 2 * SlippagePerSide, longNet,
                shortValid ? entry.BidOpen : null, shortValid ? shortStop : null,
                shortFill?.Price, shortFill?.Time.ToString("O"), shortFill?.Reason,
                shortGross, shortFee, shortFill == null ? null : 2 * SlippagePerSide, shortNet));
        }
        var detailPath = args[2] + "-trades.csv";
        var summaryPath = args[2] + "-summary.csv";
        File.WriteAllLines(detailPath, rows);
        WriteSummary(summaryPath, rows, endYear);
        Console.WriteLine($"EXP-016: {rows.Count(r => r.Contains(",trade,"))} development trades, {results.Count} matched short controls; {detailPath}; {summaryPath}");
    }

    private static (List<Bar> Held, Bar Exit)? GetWindow(DateTime date, Dictionary<DateTimeOffset, Bar> byTime)
    {
        var start = LocalTime(date, 18, 5);
        var exitTime = LocalTime(date.AddDays(1), 8, 20);
        if (start == null || exitTime == null) return null;
        var held = new List<Bar>(HeldBars);
        for (var i = 0; i < HeldBars; i++)
        {
            var time = start.Value.AddMinutes(5 * i);
            if (!byTime.TryGetValue(time, out var bar)) return null;
            held.Add(bar);
        }
        return held[^1].Time.AddMinutes(5) == exitTime.Value && byTime.TryGetValue(exitTime.Value, out var exit)
            ? (held, exit) : null;
    }

    private static DateTimeOffset? LocalTime(DateTime date, int hour, int minute)
    {
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
        if (ResearchData.Eastern.IsAmbiguousTime(local) || ResearchData.Eastern.IsInvalidTime(local)) return null;
        return new DateTimeOffset(local, ResearchData.Eastern.GetUtcOffset(local)).ToUniversalTime();
    }

    private static Fill LongExit(List<Bar> held, Bar exit, double stop)
    {
        foreach (var bar in held)
            if (bar.BidLow <= stop)
                return new Fill(bar.Time, "stop", Math.Min(stop, bar.BidOpen));
        return new Fill(exit.Time, "08:20_time", exit.BidOpen);
    }

    private static Fill ShortExit(List<Bar> held, Bar exit, double stop)
    {
        foreach (var bar in held)
            if (bar.AskHigh >= stop)
                return new Fill(bar.Time, "stop", Math.Max(stop, bar.AskOpen));
        return new Fill(exit.Time, "08:20_time", exit.AskOpen);
    }

    private static double Fee(double entry, double exit) =>
        CommissionPerMillion * (entry + exit) / 1_000_000;

    private static List<Bar> LoadBars(string path, int endYear)
    {
        var result = Directory.Exists(path) ? LoadYearlyGzip(path, endYear) : LoadCsv(path);
        if (result.Count == 0) throw new FormatException("No M5 bars");
        for (var i = 0; i < result.Count; i++)
        {
            var b = result[i];
            var prices = new[] { b.BidOpen, b.BidHigh, b.BidLow, b.BidClose,
                b.AskOpen, b.AskHigh, b.AskLow, b.AskClose };
            if (prices.Any(x => !double.IsFinite(x) || x <= 0) ||
                b.Time.Minute % 5 != 0 || b.Time.Second != 0 || b.Time.Millisecond != 0 ||
                (i > 0 && b.Time <= result[i - 1].Time) ||
                b.BidLow > Math.Min(b.BidOpen, b.BidClose) || b.BidHigh < Math.Max(b.BidOpen, b.BidClose) ||
                b.AskLow > Math.Min(b.AskOpen, b.AskClose) || b.AskHigh < Math.Max(b.AskOpen, b.AskClose) ||
                b.BidOpen > b.AskOpen || b.BidClose > b.AskClose || b.BidHigh > b.AskHigh || b.BidLow > b.AskLow)
                throw new FormatException($"Invalid or out-of-order M5 bar at row {i + 2}");
        }
        return result;
    }

    private static List<Bar> LoadCsv(string path) => ResearchData.Csv(path).Select(row => new Bar(
        ResearchData.Utc(row["timestamp_utc"]),
        ResearchData.Number(row["bid_open"]), ResearchData.Number(row["bid_high"]),
        ResearchData.Number(row["bid_low"]), ResearchData.Number(row["bid_close"]),
        ResearchData.Number(row["ask_open"]), ResearchData.Number(row["ask_high"]),
        ResearchData.Number(row["ask_low"]), ResearchData.Number(row["ask_close"]))).ToList();

    private static List<Bar> LoadYearlyGzip(string directory, int endYear)
    {
        var bars = new List<Bar>();
        for (var year = 2018; year <= endYear; year++)
        {
            var path = Path.Combine(directory, $"evtl_xauusd_m5_{year}.json.gz");
            using var source = File.OpenRead(path);
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var document = JsonDocument.Parse(gzip);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new FormatException($"Expected JSON array: {path}");
            foreach (var row in document.RootElement.EnumerateArray())
                bars.Add(new Bar(DateTimeOffset.FromUnixTimeSeconds(row.GetProperty("ts").GetInt64()),
                    row.GetProperty("o").GetDouble(), row.GetProperty("h").GetDouble(),
                    row.GetProperty("l").GetDouble(), row.GetProperty("c").GetDouble(),
                    row.GetProperty("ao").GetDouble(), row.GetProperty("ah").GetDouble(),
                    row.GetProperty("al").GetDouble(), row.GetProperty("ac").GetDouble()));
        }
        return bars;
    }

    private static string Record(DateTime date, params object?[] values)
    {
        var padded = new object?[25];
        padded[0] = date.ToString("yyyy-MM-dd");
        Array.Copy(values, 0, padded, 1, values.Length);
        return ResearchData.Row(padded);
    }

    private static void WriteSummary(string path, List<string> detail, int endYear)
    {
        var trades = detail.Skip(1).Select(ResearchData.Fields).Where(row => row[2] == "trade").ToList();
        var periods = (endYear == 2025 ? new[] { "2018-2023", "2024-2025" }
            : new[] { "2018-2023" }).Concat(Enumerable.Range(2018, endYear - 2018 + 1).Select(y => y.ToString()));
        var output = new List<string> { "period,variant,trades,net_total_per_ounce,net_mean_per_trade,profit_factor,positive_years" };
        foreach (var period in periods)
        {
            var subset = trades.Where(row => period.Contains('-') ? row[1] == period : row[0].StartsWith(period)).ToList();
            AddSummary(output, period, "long_primary", subset, 15);
            AddSummary(output, period, "same_date_short_control", subset, 24);
        }
        File.WriteAllLines(path, output);
    }

    private static void AddSummary(List<string> output, string period, string variant,
        List<string[]> rows, int column)
    {
        var values = rows.Where(row => !string.IsNullOrWhiteSpace(row[column]))
            .Select(row => (Year: row[0][..4], Net: ResearchData.Number(row[column]))).ToArray();
        var profits = values.Sum(x => Math.Max(0, x.Net));
        var losses = -values.Sum(x => Math.Min(0, x.Net));
        var positiveYears = values.GroupBy(x => x.Year).Count(group => group.Sum(x => x.Net) > 0);
        output.Add(ResearchData.Row(period, variant, values.Length, values.Sum(x => x.Net),
            values.Length == 0 ? double.NaN : values.Average(x => x.Net),
            losses == 0 ? double.NaN : profits / losses, positiveYears));
    }
}
