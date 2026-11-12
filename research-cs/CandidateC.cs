namespace ResearchCandidates;

// Candidate C compares frozen trade ledgers; it never changes the strategy's signal generation.
internal static class CandidateC
{
    private record Trade(string Id, string Strategy, DateTimeOffset Entry, double Net);

    public static void Run(string[] args)
    {
        if (args.Length != 4)
            throw new ArgumentException("c <frozen_trades.csv> <events.csv> <out_prefix>");
        var trades = ResearchData.Csv(args[1]).Select(row => new Trade(row["trade_id"],
            row["strategy"], ResearchData.Utc(row["entry_utc"]), ResearchData.Number(row["net_profit"]))).ToList();
        if (trades.Any(t => !double.IsFinite(t.Net))) throw new FormatException("Nonfinite trade net profit");
        if (trades.Select(t => t.Id).Distinct().Count() != trades.Count) throw new FormatException("Duplicate trade_id");
        var eventDates = ResearchData.Events(args[2]).Where(e =>
        {
            var local = TimeZoneInfo.ConvertTime(e.Time, ResearchData.Eastern);
            return (e.Kind.Equals("CPI", StringComparison.OrdinalIgnoreCase) ||
                    e.Kind.Equals("EMPLOYMENT", StringComparison.OrdinalIgnoreCase)) &&
                   e.Status == "actual" && !e.Overlap && local.Hour == 8 && local.Minute == 30;
        }).Select(e => TimeZoneInfo.ConvertTime(e.Time, ResearchData.Eastern).Date).ToHashSet();
        var rows = new List<string> { "trade_id,strategy,entry_utc,eastern_date,eastern_time,event_date,skip_0825_0845,net_profit" };
        var classified = trades.Select(t =>
        {
            var local = TimeZoneInfo.ConvertTime(t.Entry, ResearchData.Eastern);
            var eventDate = eventDates.Contains(local.Date);
            var skip = eventDate && local.TimeOfDay >= new TimeSpan(8, 25, 0) &&
                       local.TimeOfDay < new TimeSpan(8, 46, 0);
            rows.Add(ResearchData.Row(t.Id, t.Strategy, t.Entry.ToString("O"),
                local.ToString("yyyy-MM-dd"), local.ToString("HH:mm:ss.fffffff"), eventDate, skip, t.Net));
            return (Trade: t, EventDate: eventDate, Skip: skip);
        }).ToList();
        var detailPath = args[3] + "-trades.csv";
        File.WriteAllLines(detailPath, rows);
        var summary = new List<string> { "strategy,population,variant,trades,skipped_trades,net_profit,mean_net,profit_factor" };
        foreach (var group in classified.GroupBy(x => x.Trade.Strategy).OrderBy(x => x.Key))
        {
            foreach (var population in new[] { "all_dates", "event_dates" })
            {
                var source = population == "all_dates" ? group.ToList() : group.Where(x => x.EventDate).ToList();
                var kept = source.Where(x => !x.Skip).ToList();
                Add(summary, group.Key, population, "frozen_baseline", source, 0);
                Add(summary, group.Key, population, "calendar_exclusion", kept, source.Count - kept.Count);
            }
        }
        var summaryPath = args[3] + "-summary.csv";
        File.WriteAllLines(summaryPath, summary);
        Console.WriteLine($"Candidate C: {trades.Count} frozen trades; {detailPath}; {summaryPath}");
    }

    private static void Add(List<string> output, string strategy, string population, string variant,
        List<(Trade Trade, bool EventDate, bool Skip)> trades, int skipped)
    {
        var profits = trades.Sum(x => Math.Max(0, x.Trade.Net));
        var losses = -trades.Sum(x => Math.Min(0, x.Trade.Net));
        output.Add(ResearchData.Row(strategy, population, variant, trades.Count, skipped,
            trades.Sum(x => x.Trade.Net), trades.Count == 0 ? double.NaN : trades.Average(x => x.Trade.Net),
            losses == 0 ? double.NaN : profits / losses));
    }
}
