namespace ResearchCandidates;

// Candidate A from the 2026-11-03 thesis scan. Research simulation, not an order router.
internal static class CandidateA
{
    public static void Run(string[] args)
    {
        if (args.Length != 6)
            throw new ArgumentException("a <spot_quotes.csv> <events.csv> <commission_usd_per_million> <adverse_slippage_usd_per_ounce_per_side> <out.csv>");
        var quotes = ResearchData.Quotes(args[1]);
        var events = ResearchData.Events(args[2]);
        var commission = ResearchData.Number(args[3]);
        var slippage = ResearchData.Number(args[4]);
        if (commission < 0 || slippage < 0) throw new ArgumentException("Costs must be nonnegative");
        var history = new Queue<double>();
        var lines = new List<string> { "event_utc,kind,status,reason,move,prior60_median,direction,entry_utc,exit_utc,entry_price,exit_price,stop_price,net_per_ounce,opposite_net_per_ounce,random_net_per_ounce,unfiltered_net_per_ounce" };
        foreach (var e in events)
        {
            var local = TimeZoneInfo.ConvertTime(e.Time, ResearchData.Eastern);
            var date = local.Date;
            var kind = e.Kind.Trim().ToUpperInvariant();
            if ((kind != "CPI" && kind != "EMPLOYMENT") || e.Status != "actual" || e.Overlap ||
                local.Hour != 8 || local.Minute != 30 || local.Second != 0 ||
                date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                lines.Add(Record(e.Time.ToString("O"), e.Kind, "skip", "ineligible_event"));
                continue;
            }
            var before = ResearchData.EasternTime(date, 8, 29, 59);
            var start = ResearchData.EasternTime(date, 8, 30);
            var entryTime = ResearchData.EasternTime(date, 8, 35);
            var scheduledExit = ResearchData.EasternTime(date, 9, 0);
            var reference = ResearchData.LatestAtOrBefore(quotes, before, TimeSpan.FromSeconds(1));
            var first = ResearchData.FirstAtOrAfter(quotes, start);
            var entryIndex = ResearchData.FirstAtOrAfter(quotes, entryTime);
            var exitIndex = ResearchData.FirstAtOrAfter(quotes, scheduledExit);
            if (reference == null || first >= entryIndex || entryIndex >= quotes.Count ||
                exitIndex >= quotes.Count || quotes[first].Time > start.AddSeconds(1) ||
                quotes[entryIndex].Time > entryTime.AddSeconds(1) ||
                quotes[exitIndex].Time > scheduledExit.AddSeconds(1) ||
                !Continuous(quotes, first, exitIndex, TimeSpan.FromSeconds(5)))
            {
                lines.Add(Record(e.Time.ToString("O"), kind, "skip", "missing_or_stale_ticks"));
                continue;
            }
            var window = quotes.GetRange(first, entryIndex - first);
            var last = window[^1];
            var move = last.Mid / reference.Mid - 1.0;
            var threshold = history.Count == 60 ? ResearchData.Median(history) : double.NaN;
            // Every complete eligible event enters the prior-event history, including zero and filtered events.
            history.Enqueue(Math.Abs(move));
            if (history.Count > 60) history.Dequeue();
            if (move == 0)
            {
                lines.Add(Record(e.Time.ToString("O"), kind,
                    double.IsNaN(threshold) ? "warmup" : "filtered", "zero_move", move,
                    double.IsNaN(threshold) ? null : threshold));
                continue;
            }
            var direction = Math.Sign(move);
            var stop = direction > 0 ? window.Min(x => x.Mid) : window.Max(x => x.Mid);
            var entry = quotes[entryIndex];
            if ((direction > 0 && entry.Bid <= stop) || (direction < 0 && entry.Ask >= stop))
            {
                lines.Add(Record(e.Time.ToString("O"), kind, "skip", "stop_crossed_before_entry", move, threshold));
                continue;
            }
            var actualExit = exitIndex;
            for (var i = entryIndex + 1; i <= exitIndex; i++)
            {
                if (direction > 0 ? quotes[i].Bid <= stop : quotes[i].Ask >= stop)
                { actualExit = i; break; }
            }
            var randomizedDirection = StableDirection(date, kind);
            var chosenExit = quotes[actualExit];
            var net = Pnl(direction, entry, chosenExit, commission, slippage);
            if (double.IsNaN(threshold))
            {
                lines.Add(Record(e.Time.ToString("O"), kind, "warmup", "prior_60_events_required",
                    move, "", direction, entry.Time.ToString("O"), chosenExit.Time.ToString("O"),
                    direction > 0 ? entry.Ask + slippage : entry.Bid - slippage,
                    direction > 0 ? chosenExit.Bid - slippage : chosenExit.Ask + slippage,
                    stop, "", "", "", net));
                continue;
            }
            if (Math.Abs(move) <= threshold)
            {
                lines.Add(Record(e.Time.ToString("O"), kind, "filtered", "move_below_threshold",
                    move, threshold, direction, entry.Time.ToString("O"), chosenExit.Time.ToString("O"),
                    direction > 0 ? entry.Ask + slippage : entry.Bid - slippage,
                    direction > 0 ? chosenExit.Bid - slippage : chosenExit.Ask + slippage,
                    stop, "", "", "", net));
                continue;
            }
            lines.Add(Record(e.Time.ToString("O"), kind, "trade",
                actualExit == exitIndex ? "time_exit" : "stop_exit", move, threshold, direction,
                entry.Time.ToString("O"), chosenExit.Time.ToString("O"),
                direction > 0 ? entry.Ask + slippage : entry.Bid - slippage,
                direction > 0 ? chosenExit.Bid - slippage : chosenExit.Ask + slippage,
                stop, net,
                Pnl(-direction, entry, chosenExit, commission, slippage),
                Pnl(randomizedDirection, entry, chosenExit, commission, slippage), net));
        }
        File.WriteAllLines(args[5], lines);
        var summary = ResultSummary.Write(args[5], lines, 1,
            ("thresholded", 12), ("opposite", 13), ("fixed_random", 14), ("unfiltered", 15));
        Console.WriteLine($"Candidate A: {lines.Count - 1} event rows, {lines.Count(x => x.Contains(",trade,"))} trades; {args[5]}; {summary}");
    }

    private static bool Continuous(List<Quote> quotes, int first, int last, TimeSpan maximumGap)
    {
        for (var i = first + 1; i <= last; i++)
            if (quotes[i].Time - quotes[i - 1].Time > maximumGap) return false;
        return true;
    }

    private static string Record(params object?[] values)
    {
        var padded = new object?[16];
        Array.Copy(values, padded, values.Length);
        return ResearchData.Row(padded);
    }

    private static double Pnl(int direction, Quote entry, Quote exit, double commissionPerMillion, double slippage)
    {
        var entryPrice = direction > 0 ? entry.Ask + slippage : entry.Bid - slippage;
        var exitPrice = direction > 0 ? exit.Bid - slippage : exit.Ask + slippage;
        // One ounce of XAUUSD; per-million commission applies to both transaction notionals.
        return direction * (exitPrice - entryPrice) -
            commissionPerMillion * (entryPrice + exitPrice) / 1_000_000;
    }

    private static int StableDirection(DateTime date, string kind)
    {
        // Fixed, portable FNV-1a hash; no runtime-randomized string hash or look-ahead.
        uint hash = 2166136261;
        foreach (var c in date.ToString("yyyyMMdd") + kind)
            hash = (hash ^ c) * 16777619;
        return (hash & 1) == 0 ? 1 : -1;
    }
}
