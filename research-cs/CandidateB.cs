namespace ResearchCandidates;

// Cross-market diagnostic only. Never use this to route FTMO orders.
internal static class CandidateB
{
    public static void Run(string[] args)
    {
        if (args.Length != 8)
            throw new ArgumentException("b <spot_quotes.csv> <gc_quotes.csv> <clock_error_ms> <commission_usd_per_million> <slippage_per_side> <round_trip_cost_estimate> <out.csv>");
        var spot = ResearchData.Quotes(args[1]);
        var gc = ResearchData.Quotes(args[2], contractRequired: true);
        var clockErrorMs = ResearchData.Number(args[3]);
        var commission = ResearchData.Number(args[4]);
        var slippage = ResearchData.Number(args[5]);
        var costEstimate = ResearchData.Number(args[6]);
        if (clockErrorMs < 0 || clockErrorMs > 100 || commission < 0 || slippage < 0 || costEstimate < 0)
            throw new ArgumentException("Clock error must be measured and <=100 ms; costs must be nonnegative");
        var dates = spot.Select(q => q.Time.UtcDateTime.Date).Distinct().Order().ToArray();
        var gcByDate = new Dictionary<DateTime, List<double>>();
        var spreadByDateMinute = new Dictionary<(DateTime, int), List<double>>();
        for (var i = 0; i < gc.Count; i++)
        {
            var old = ResearchData.LatestAtOrBefore(gc, gc[i].Time.AddSeconds(-5), TimeSpan.FromSeconds(1));
            if (old == null || old.Contract != gc[i].Contract || old.Time.UtcDateTime.Date != gc[i].Time.UtcDateTime.Date) continue;
            var date = gc[i].Time.UtcDateTime.Date;
            if (!gcByDate.TryGetValue(date, out var changes)) gcByDate[date] = changes = new();
            changes.Add(Math.Abs(gc[i].Mid - old.Mid));
        }
        foreach (var q in spot)
        {
            var key = (q.Time.UtcDateTime.Date, q.Time.UtcDateTime.Hour * 60 + q.Time.UtcDateTime.Minute);
            if (!spreadByDateMinute.TryGetValue(key, out var values)) spreadByDateMinute[key] = values = new();
            values.Add(q.Spread);
        }
        var lines = new List<string> { "decision_utc,contract,gc_change,spot_change,prior20_gc_p95,prior20_same_minute_spread_median,expected_catchup,entry_utc,exit_utc,exit_reason,net_per_ounce,sign_randomized_net,delayed_quote_placebo_net,latency_250ms_net,latency_1000ms_net,clock_error_ms" };
        DateTimeOffset nextArm = DateTimeOffset.MinValue;
        foreach (var date in dates)
        {
            var prior = gcByDate.Keys.Where(d => d < date).OrderDescending().Take(20).ToArray();
            if (prior.Length < 20) continue;
            var threshold = ResearchData.Percentile95(prior.SelectMany(d => gcByDate[d]));
            var dayStart = new DateTimeOffset(date, TimeSpan.Zero);
            var start = ResearchData.FirstAtOrAfter(spot, dayStart);
            for (var i = start; i < spot.Count && spot[i].Time < dayStart.AddDays(1); i++)
            {
                var q = spot[i];
                if (q.Time < nextArm || q.Time.Millisecond != 0) continue;
                var minute = q.Time.UtcDateTime.Hour * 60 + q.Time.UtcDateTime.Minute;
                var oldSpot = ResearchData.LatestAtOrBefore(spot, q.Time.AddSeconds(-5), TimeSpan.FromSeconds(1));
                var nowGc = ResearchData.LatestAtOrBefore(gc, q.Time, TimeSpan.FromSeconds(1));
                var oldGc = ResearchData.LatestAtOrBefore(gc, q.Time.AddSeconds(-5), TimeSpan.FromSeconds(1));
                if (oldSpot == null || nowGc == null || oldGc == null ||
                    nowGc.Contract != oldGc.Contract || oldGc.Time.UtcDateTime.Date != date ||
                    oldSpot.Time.UtcDateTime.Date != date) continue;
                var gcChange = nowGc.Mid - oldGc.Mid;
                var spotChange = q.Mid - oldSpot.Mid;
                var direction = Math.Sign(gcChange);
                if (direction == 0 || Math.Abs(gcChange) <= threshold ||
                    direction * spotChange >= Math.Abs(gcChange) / 2) continue;
                var priorSpreads = prior.Select(d => spreadByDateMinute.TryGetValue((d, minute), out var v)
                        ? (double?)ResearchData.Median(v) : null)
                    .Where(v => v.HasValue).Select(v => v!.Value).ToArray();
                if (priorSpreads.Length != 20) continue;
                var medianSpread = ResearchData.Median(priorSpreads);
                var catchup = Math.Abs(gcChange) - direction * spotChange;
                if (q.Spread >= medianSpread || catchup <= 2 * costEstimate) continue;
                var entryIndex = ResearchData.FirstAtOrAfter(spot, q.Time.AddTicks(1));
                if (entryIndex >= spot.Count || spot[entryIndex].Time - q.Time > TimeSpan.FromSeconds(2)) continue;
                var entry = spot[entryIndex];
                var maxExit = ResearchData.FirstAtOrAfter(spot, q.Time.AddMinutes(5));
                if (maxExit >= spot.Count || spot[maxExit].Time - q.Time.AddMinutes(5) > TimeSpan.FromSeconds(2)) continue;
                var gap = false;
                for (var j = entryIndex + 1; j <= maxExit; j++)
                    if (spot[j].Time - spot[j - 1].Time > TimeSpan.FromSeconds(2)) { gap = true; break; }
                if (gap) continue;
                var exitIndex = maxExit;
                var reason = "five_minutes";
                for (var j = entryIndex + 1; j <= maxExit; j++)
                {
                    var mark = direction > 0 ? spot[j].Bid : spot[j].Ask;
                    var entryPrice = direction > 0 ? entry.Ask : entry.Bid;
                    if (direction * (mark - entryPrice) <= -catchup) { exitIndex = j; reason = "protective_loss"; break; }
                    if (direction * (spot[j].Mid - q.Mid) >= catchup) { exitIndex = j; reason = "convergence"; break; }
                }
                var exit = spot[exitIndex];
                var randomDirection = StableSign(q.Time);
                var delayed = ResearchData.FirstAtOrAfter(spot, q.Time.AddSeconds(5));
                var latency250 = ResearchData.FirstAtOrAfter(spot, q.Time.AddMilliseconds(250));
                var latency1000 = ResearchData.FirstAtOrAfter(spot, q.Time.AddMilliseconds(1000));
                lines.Add(ResearchData.Row(q.Time.ToString("O"), nowGc.Contract, gcChange, spotChange,
                    threshold, medianSpread, catchup, entry.Time.ToString("O"), exit.Time.ToString("O"), reason,
                    Pnl(direction, entry, exit, commission, slippage),
                    Pnl(randomDirection, entry, exit, commission, slippage),
                    delayed <= exitIndex ? Pnl(direction, spot[delayed], exit, commission, slippage) : double.NaN,
                    latency250 <= exitIndex ? Pnl(direction, spot[latency250], exit, commission, slippage) : double.NaN,
                    latency1000 <= exitIndex ? Pnl(direction, spot[latency1000], exit, commission, slippage) : double.NaN,
                    clockErrorMs));
                nextArm = q.Time.AddMinutes(10) > exit.Time ? q.Time.AddMinutes(10) : exit.Time;
                i = exitIndex;
            }
        }
        File.WriteAllLines(args[7], lines);
        var summary = ResultSummary.Write(args[7], lines, 1,
            ("primary", 10), ("sign_randomized", 11), ("delayed_quote", 12),
            ("latency_250ms", 13), ("latency_1000ms", 14));
        Console.WriteLine($"Candidate B: {lines.Count - 1} research trades; {args[7]}; {summary}");
    }

    private static double Pnl(int direction, Quote entry, Quote exit, double commission, double slippage)
    {
        var buy = direction > 0 ? entry.Ask + slippage : exit.Ask + slippage;
        var sell = direction > 0 ? exit.Bid - slippage : entry.Bid - slippage;
        return sell - buy - commission * (buy + sell) / 1_000_000;
    }

    private static int StableSign(DateTimeOffset time)
    {
        ulong x = (ulong)time.UtcTicks;
        x ^= x >> 33; x *= 0xff51afd7ed558ccdUL; x ^= x >> 33;
        return (x & 1) == 0 ? 1 : -1;
    }
}
