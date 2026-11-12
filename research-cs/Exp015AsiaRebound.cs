using System.IO.Compression;
using System.Text.Json;

namespace ResearchCandidates;

// EXP-015: frozen independent-feed screen from the 2026-11-03 preregistration.
internal static class Exp015AsiaRebound
{
    private const double CommissionPerMillion = 30;
    private const double SlippagePerSide = 0.02;
    private const int WarmupWindows = 20;

    private record Bar(DateTimeOffset Time, double BidOpen, double BidHigh, double BidLow,
        double BidClose, double AskOpen, double AskHigh, double AskLow, double AskClose)
    {
        public double MidOpen => (BidOpen + AskOpen) / 2;
        public double MidClose => (BidClose + AskClose) / 2;
        public double BidRange => BidHigh - BidLow;
    }

    private record Night(DateTime Date, Bar UsOpen, Bar UsClose, List<Bar> Asia, Bar Exit)
    {
        public bool Signal => UsClose.MidClose / UsOpen.MidOpen - 1 < 0;
        public double BidRange => Asia.Max(x => x.BidHigh) - Asia.Min(x => x.BidLow);
    }

    private record Outcome(string Reason, DateTimeOffset ExitTime, double Entry, double Exit, double Net);

    public static void Run(string[] args)
    {
        if (args.Length is < 3 or > 4) throw new ArgumentException("exp015 <m5_bid_ask.csv|evtl_directory> <out_prefix> [end_year: 2023|2025]");
        var endYear = args.Length == 4 ? int.Parse(args[3]) : 2023;
        if (endYear is not (2023 or 2025)) throw new ArgumentException("end_year must be 2023 or 2025");
        var bars = LoadBars(args[1], endYear);
        var byTime = bars.ToDictionary(x => x.Time);
        var history = new Queue<double>();
        var detail = new List<string> { "ny_date,split,status,reason,us_mid_return,prior20_median_bid_range,asia_bid_range,stop_long,stop_short,long_entry_ask,long_exit_bid,long_exit_utc,long_exit_reason,conditional_long_net,unconditional_long_net,short_entry_bid,short_exit_ask,short_exit_utc,short_exit_reason,signal_short_net" };
        var observations = new List<(DateTime Date, bool Signal, double? Primary, double? Unconditional, double? Short)>();
        for (var date = new DateTime(2018, 1, 1); date <= new DateTime(endYear, 12, 31); date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var split = date.Year <= 2023 ? "2018-2023" : "2024-2025";
            var asia = Window(date, 20, 0, 72, byTime);
            var exitTime = LocalTime(date.AddDays(1), 2, 0);
            if (asia == null || exitTime == null || !byTime.TryGetValue(exitTime.Value, out var exitBar))
            {
                detail.Add(Record(date.ToString("yyyy-MM-dd"), split, "skip", "incomplete_asia_window"));
                continue;
            }
            var range = asia.Max(x => x.BidHigh) - asia.Min(x => x.BidLow);
            var threshold = history.Count == WarmupWindows ? ResearchData.Median(history) : double.NaN;
            // Every complete Asian window enters history, even when its U.S. bars are missing.
            history.Enqueue(range);
            if (history.Count > WarmupWindows) history.Dequeue();
            if (double.IsNaN(threshold))
            {
                detail.Add(Record(date.ToString("yyyy-MM-dd"), split, "warmup", "prior_20_complete_windows_required",
                    "", "", range));
                continue;
            }
            var us = Window(date, 8, 20, 92, byTime);
            if (us == null)
            {
                detail.Add(Record(date.ToString("yyyy-MM-dd"), split, "skip", "incomplete_us_window",
                    "", threshold, range));
                continue;
            }
            var night = new Night(date, us[0], us[^1], asia, exitBar);
            var usReturn = night.UsClose.MidClose / night.UsOpen.MidOpen - 1;
            var entry = night.Asia[0];
            var stopLong = entry.AskOpen - threshold;
            var stopShort = entry.BidOpen + threshold;
            if (threshold <= 0 || stopLong >= entry.BidOpen)
            {
                detail.Add(Record(date.ToString("yyyy-MM-dd"), split, "skip", "invalid_stop_geometry", usReturn,
                    threshold, range, stopLong, stopShort));
                continue;
            }
            var longResult = SimulateLong(night, stopLong);
            var shortResult = night.Signal && stopShort > entry.AskOpen
                ? SimulateShort(night, stopShort) : null;
            observations.Add((date, night.Signal, night.Signal ? longResult.Net : null,
                longResult.Net, shortResult?.Net));
            detail.Add(Record(date.ToString("yyyy-MM-dd"), split, night.Signal ? "signal" : "no_signal", "complete",
                usReturn, threshold, range, stopLong, stopShort,
                longResult.Entry, longResult.Exit, longResult.ExitTime.ToString("O"),
                longResult.Reason, night.Signal ? longResult.Net : null, longResult.Net,
                shortResult?.Entry, shortResult?.Exit, shortResult?.ExitTime.ToString("O"),
                shortResult?.Reason, shortResult?.Net));
        }
        var detailPath = args[2] + "-trades.csv";
        File.WriteAllLines(detailPath, detail);
        var summaryPath = args[2] + "-summary.csv";
        WriteSummary(summaryPath, observations, endYear);
        Console.WriteLine($"EXP-015: {observations.Count} post-warmup complete windows, {observations.Count(x => x.Signal)} signal dates; {detailPath}; {summaryPath}");
    }

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
                b.BidOpen > b.AskOpen || b.BidClose > b.AskClose ||
                b.BidHigh > b.AskHigh || b.BidLow > b.AskLow)
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

    private static List<Bar>? Window(DateTime date, int hour, int minute, int count,
        Dictionary<DateTimeOffset, Bar> byTime)
    {
        var start = date.AddHours(hour).AddMinutes(minute);
        var rows = new List<Bar>(count);
        for (var i = 0; i < count; i++)
        {
            var local = start.AddMinutes(5 * i);
            var utc = LocalTime(local.Date, local.Hour, local.Minute);
            if (utc == null || !byTime.TryGetValue(utc.Value, out var bar) ||
                (i > 0 && bar.Time - rows[^1].Time != TimeSpan.FromMinutes(5))) return null;
            rows.Add(bar);
        }
        return rows;
    }

    private static DateTimeOffset? LocalTime(DateTime date, int hour, int minute)
    {
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0,
            DateTimeKind.Unspecified);
        if (ResearchData.Eastern.IsAmbiguousTime(local) || ResearchData.Eastern.IsInvalidTime(local))
            return null;
        return new DateTimeOffset(local, ResearchData.Eastern.GetUtcOffset(local)).ToUniversalTime();
    }

    private static Outcome SimulateLong(Night night, double stop)
    {
        var entry = night.Asia[0].AskOpen;
        foreach (var b in night.Asia)
            if (b.BidLow <= stop)
            {
                var exit = Math.Min(stop, b.BidOpen);
                return new Outcome("stop", b.Time, entry, exit, NetLong(entry, exit));
            }
        return new Outcome("02:00_time", night.Exit.Time, entry,
            night.Exit.BidOpen, NetLong(entry, night.Exit.BidOpen));
    }

    private static Outcome SimulateShort(Night night, double stop)
    {
        var entry = night.Asia[0].BidOpen;
        foreach (var b in night.Asia)
            if (b.AskHigh >= stop)
            {
                var exit = Math.Max(stop, b.AskOpen);
                return new Outcome("stop", b.Time, entry, exit, NetShort(entry, exit));
            }
        return new Outcome("02:00_time", night.Exit.Time, entry,
            night.Exit.AskOpen, NetShort(entry, night.Exit.AskOpen));
    }

    private static double NetLong(double askEntry, double bidExit) =>
        bidExit - askEntry - CommissionPerMillion * (askEntry + bidExit) / 1_000_000 - 2 * SlippagePerSide;

    private static double NetShort(double bidEntry, double askExit) =>
        bidEntry - askExit - CommissionPerMillion * (bidEntry + askExit) / 1_000_000 - 2 * SlippagePerSide;

    private static string Record(params object?[] values)
    {
        var padded = new object?[20];
        Array.Copy(values, padded, values.Length);
        return ResearchData.Row(padded);
    }

    private static void WriteSummary(string path,
        List<(DateTime Date, bool Signal, double? Primary, double? Unconditional, double? Short)> observations,
        int endYear)
    {
        var rows = new List<string> { "period,variant,trades,net_per_ounce,mean_net_per_ounce,profit_factor" };
        var periods = (endYear == 2025 ? new[] { "2018-2023", "2024-2025" }
            : new[] { "2018-2023" }).Concat(Enumerable.Range(2018, endYear - 2018 + 1).Select(y => y.ToString()));
        foreach (var period in periods)
        {
            var sample = observations.Where(x => period.Contains('-')
                ? (period == "2018-2023" ? x.Date.Year <= 2023 : x.Date.Year >= 2024)
                : x.Date.Year.ToString() == period).ToList();
            Add(rows, period, "conditioned_long", sample.Select(x => x.Primary));
            Add(rows, period, "conditioned_long_per_eligible_date",
                sample.Select(x => (double?)(x.Primary ?? 0)));
            Add(rows, period, "unconditional_long_all_eligible", sample.Select(x => x.Unconditional));
            Add(rows, period, "unconditional_long_signal_dates", sample.Where(x => x.Signal).Select(x => x.Unconditional));
            Add(rows, period, "signal_date_short", sample.Select(x => x.Short));
        }
        File.WriteAllLines(path, rows);
    }

    private static void Add(List<string> rows, string period, string variant, IEnumerable<double?> source)
    {
        var values = source.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var profits = values.Sum(x => Math.Max(0, x));
        var losses = -values.Sum(x => Math.Min(0, x));
        rows.Add(ResearchData.Row(period, variant, values.Length, values.Sum(),
            values.Length == 0 ? double.NaN : values.Average(),
            losses == 0 ? double.NaN : profits / losses));
    }
}
