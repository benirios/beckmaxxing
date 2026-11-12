using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using cAlgo.API;
using File = System.IO.File;

namespace cAlgo.Robots
{
    // Run locally on the intended broker account, outside Backtesting.
    // This robot has no trading operations. FullAccess permits local CSV writes.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class XauUsdTickExporter : Robot
    {
        [Parameter("Start UTC (inclusive)", DefaultValue = "2026-11-02T00:00:00Z")]
        public string StartUtc { get; set; }

        [Parameter("End UTC (exclusive)", DefaultValue = "2026-11-07T00:00:00Z")]
        public string EndUtc { get; set; }

        [Parameter("Output folder (blank = Documents)", DefaultValue = "")]
        public string OutputFolder { get; set; }

        [Parameter("Feed label", DefaultValue = "SET_BROKER_AND_DEMO_OR_LIVE")]
        public string FeedLabel { get; set; }

        [Parameter("Max loaded ticks", DefaultValue = 5000000, MinValue = 10000)]
        public int MaxLoadedTicks { get; set; }

        private Ticks _ticks;
        private DateTime _start, _end;
        private bool _loading, _finished, _exhausted;
        private int _pages;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        protected override void OnStart()
        {
            try
            {
                if (RunningMode != RunningMode.RealTime)
                    throw new InvalidOperationException("Run locally as a normal instance, not in Backtesting or Optimisation.");
                _start = ParseUtc(StartUtc);
                _end = ParseUtc(EndUtc);
                if (_start >= _end || _end > Server.TimeInUtc)
                    throw new ArgumentException("Require start < end <= current UTC time.");
                if (string.IsNullOrWhiteSpace(FeedLabel) || FeedLabel == "SET_BROKER_AND_DEMO_OR_LIVE")
                    throw new ArgumentException("Set Feed label, e.g. FTMO-demo. Use the intended broker account and chart symbol.");
                _ticks = MarketData.GetTicks(SymbolName);
                _ticks.Reloaded += OnReloaded;
                Print("EXPORT_START symbol={0} feed={1} range=[{2:O},{3:O}) loaded={4}",
                    SymbolName, FeedLabel, _start, _end, _ticks.Count);
                Timer.Start(TimeSpan.FromSeconds(1));
            }
            catch (Exception error) { Fail(error); }
        }

        protected override void OnTimer()
        {
            if (_finished || _loading) return;
            try
            {
                if (_ticks.Count > 0 && _ticks[0].Time < _start)
                {
                    Export("start_boundary_reached");
                    return;
                }
                if (_exhausted || _ticks.Count >= MaxLoadedTicks)
                {
                    Export(_exhausted ? "history_exhausted_before_start" : "tick_limit_before_start");
                    return;
                }
                _loading = true;
                _ticks.LoadMoreHistoryAsync(args =>
                {
                    if (_finished) return;
                    _loading = false;
                    _exhausted = args.Count == 0;
                    _pages++;
                    if (_pages == 1 || _pages % 25 == 0 || _exhausted)
                        Print("EXPORT_PROGRESS pages={0} loaded={1} earliest={2}", _pages, _ticks.Count,
                            _ticks.Count == 0 ? "none" : _ticks[0].Time.ToString("O", _culture));
                });
            }
            catch (Exception error) { Fail(error); }
        }

        private void OnReloaded(TicksHistoryLoadedEventArgs args)
        {
            Fail(new InvalidOperationException("Tick collection reloaded after reconnect. Restart export for a consistent snapshot."));
        }

        private void Export(string historyStatus)
        {
            _finished = true;
            Timer.Stop();
            string root = string.IsNullOrWhiteSpace(OutputFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "cTraderTickExports")
                : OutputFolder;
            if (!Path.IsPathRooted(root)) throw new ArgumentException("Output folder must be an absolute path.");
            string folder = Path.Combine(root, "ticks_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss", _culture)
                + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(folder);
            string pending = Path.Combine(folder, "ticks.csv.partial");
            string csv = Path.Combine(folder, "ticks.csv");
            long count = 0, identicalConsecutive = 0;
            DateTime? first = null, last = null, previousTime = null;
            double previousBid = 0, previousAsk = 0;
            int snapshotCount = _ticks.Count;
            using (var writer = new StreamWriter(pending, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("timestamp,bid,ask");
                for (int i = 0; i < snapshotCount; i++)
                {
                    var tick = _ticks[i];
                    if (previousTime.HasValue && tick.Time < previousTime.Value)
                        throw new InvalidDataException("Nonmonotonic tick history; partial file must not be used.");
                    previousTime = tick.Time;
                    if (tick.Time < _start || tick.Time >= _end) continue;
                    if (!double.IsFinite(tick.Bid) || !double.IsFinite(tick.Ask)
                        || tick.Bid <= 0 || tick.Ask < tick.Bid)
                        throw new InvalidDataException("Invalid bid/ask quote; partial file must not be used.");
                    if (last == tick.Time && previousBid == tick.Bid && previousAsk == tick.Ask)
                        identicalConsecutive++;
                    // Preserve source order, timestamp precision and repeated quotes.
                    writer.WriteLine(DateTime.SpecifyKind(tick.Time, DateTimeKind.Utc).ToString("O", _culture)
                        + "," + tick.Bid.ToString("R", _culture) + "," + tick.Ask.ToString("R", _culture));
                    if (!first.HasValue) first = DateTime.SpecifyKind(tick.Time, DateTimeKind.Utc);
                    last = DateTime.SpecifyKind(tick.Time, DateTimeKind.Utc);
                    previousBid = tick.Bid;
                    previousAsk = tick.Ask;
                    count++;
                }
            }
            string hash;
            using (var stream = File.OpenRead(pending))
            using (var sha = SHA256.Create())
                hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            var manifest = new
            {
                exporter = "XauUsdTickExporter-v1", provider = "cTrader-MarketData.GetTicks",
                feed_label = FeedLabel, broker = Account.BrokerName, account_is_live = Account.IsLive,
                symbol = SymbolName, digits = Symbol.Digits, tick_size = Symbol.TickSize,
                requested_start_utc = _start, requested_end_utc_exclusive = _end,
                downloaded_at_utc = DateTime.UtcNow, history_status = historyStatus,
                first_exported_utc = first, last_exported_utc = last, rows = count,
                identical_consecutive_quotes_preserved = identicalConsecutive,
                loaded_ticks = snapshotCount, history_pages = _pages,
                csv_file = "ticks.csv", bytes = new FileInfo(pending).Length, sha256 = hash,
                timestamp_semantics = "UTC quote event time exposed by cTrader; original order preserved",
                quote_semantics = "broker best bid and ask; no size fields supplied by Tick API",
                quality_status = "unaudited: boundary status does not establish gap-free coverage or verified price scale"
            };
            File.WriteAllText(Path.Combine(folder, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(pending, csv);
            Print("EXPORT_DONE status={0} rows={1} CSV={2}", historyStatus, count, csv);
            Print("Keep manifest.json with the CSV. Audit coverage and price scale before research use.");
            Stop();
        }

        private static DateTime ParseUtc(string value)
        {
            if (value == null || !value.EndsWith("Z", StringComparison.Ordinal)
                || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                throw new ArgumentException("Dates must be ISO-8601 UTC ending in Z, e.g. 2026-11-02T00:00:00Z.");
            return parsed.UtcDateTime;
        }

        private void Fail(Exception error)
        {
            _finished = true;
            Timer.Stop();
            Print("EXPORT_FAILED: {0}", error.Message);
            Stop();
        }

        protected override void OnStop()
        {
            if (_ticks != null) _ticks.Reloaded -= OnReloaded;
            if (!_finished) Print("EXPORT_CANCELLED: no completed export was produced.");
            _finished = true;
            Timer.Stop();
        }
    }
}
