using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T003: Compare same-sign continuation across Tokyo, London and New York. Supply verified local-session first-30-minute midpoint-return signs, observed on the same executing broker, at the end of that complete interval. Historical session calendars, local DST and session group labels must be audited upstream. Run each session separately with identical 30-minute horizons; zero or incomplete predictors are excluded.
    /// Backtest only. Receipt times are when complete inputs became available.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdSessionContinuationStrength : Robot
    {
        private const string BuildId = "T003_POINT_IN_TIME_V1_20261107";
        private static readonly TimeSpan MaxEntryDelay = TimeSpan.FromSeconds(2);

        public enum TestVariant { FollowSessionMove, OppositeSignControl, RandomDirectionControl }

        [Parameter("Test Variant", DefaultValue = TestVariant.FollowSessionMove)]
        public TestVariant Variant { get; set; }

        [Parameter("Verified Receipts UTC (YYYY-MM-DDTHH:mm:ssZ=+1/-1, comma separated)", DefaultValue = "")]
        public string SignedReceiptsUtc { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T003_MARGIN")]
        public string PositionLabel { get; set; }

        private sealed class Signal
        {
            public DateTime Receipt;
            public int Sign;
        }

        private readonly List<Signal> _signals = new List<Signal>();
        private int _nextSignal;
        private DateTime _exitAt = DateTime.MinValue;
        private string _label;

        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime)
            {
                Print("T003_STOP,reason=research_only_no_live_trading");
                Stop();
                return;
            }
            try
            {
                var seen = new HashSet<DateTime>();
                foreach (var token in (SignedReceiptsUtc ?? "").Split(
                    new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = token.Trim().Split('=');
                    DateTime receipt;
                    if (parts.Length != 2 || (parts[1].Trim() != "+1" && parts[1].Trim() != "-1")
                        || !DateTime.TryParseExact(parts[0].Trim(), new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out receipt) || !seen.Add(receipt))
                        throw new FormatException("Invalid or duplicate signed UTC receipt: " + token);

                    _signals.Add(new Signal { Receipt = receipt, Sign = parts[1].Trim() == "+1" ? 1 : -1 });
                }
                if (_signals.Count == 0)
                    throw new FormatException("Compare same-sign continuation across Tokyo, London and New York. Supply verified local-session first-30-minute midpoint-return signs, observed on the same executing broker, at the end of that complete interval. Historical session calendars, local DST and session group labels must be audited upstream. Run each session separately with identical 30-minute horizons; zero or incomplete predictors are excluded.");
                _signals.Sort((a, b) => a.Receipt.CompareTo(b.Receipt));
            }
            catch (Exception error)
            {
                Print("T003_STOP,reason=configuration,error={0}", error.Message);
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Variant;
            Positions.Closed += OnPositionClosed;
            Print("T003_START,build={0},variant={1},signals={2},max_entry_delay=2s,exit=receipt_plus_30_minutes,volume={3},tick_size={4}",
                BuildId, Variant, _signals.Count, FixedVolumeInUnits, Symbol.TickSize);
        }

        protected override void OnTick()
        {
            var now = DateTime.SpecifyKind(Server.Time, DateTimeKind.Utc);
            if (_exitAt != DateTime.MinValue && now >= _exitAt)
            {
                CloseManaged("fixed_event_deadline");
                if (Positions.FindAll(_label, SymbolName).Length == 0)
                    _exitAt = DateTime.MinValue;
            }
            while (_nextSignal < _signals.Count && _signals[_nextSignal].Receipt < now)
            {
                var signal = _signals[_nextSignal++];
                if (now - signal.Receipt > MaxEntryDelay)
                {
                    Print("T003_SKIP,receipt={0:O},reason=late_or_missing_quote,first_quote={1:O}", signal.Receipt, now);
                    continue;
                }
                if (Positions.FindAll(_label, SymbolName).Length > 0)
                {
                    Print("T003_SKIP,receipt={0:O},reason=overlapping_position", signal.Receipt);
                    continue;
                }
                if (Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid)
                {
                    Print("T003_SKIP,receipt={0:O},reason=invalid_bid_ask", signal.Receipt);
                    continue;
                }
                var volume = Symbol.NormalizeVolumeInUnits(
                    Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
                if (volume < Symbol.VolumeInUnitsMin)
                {
                    Print("T003_SKIP,receipt={0:O},reason=volume_below_minimum", signal.Receipt);
                    continue;
                }
                var sign = Variant == TestVariant.FollowSessionMove ? signal.Sign
                    : Variant == TestVariant.OppositeSignControl ? -signal.Sign
                    : StableSign(signal.Receipt);
                var direction = sign > 0 ? TradeType.Buy : TradeType.Sell;
                var exitAt = signal.Receipt.AddMinutes(30);
                Print("T003_SIGNAL,receipt={0:O},entry_quote={1:O},event_sign={2},variant={3},direction={4},bid={5},ask={6},spread={7},exit_at={8:O}",
                    signal.Receipt, now, signal.Sign, Variant, direction, Symbol.Bid, Symbol.Ask,
                    Symbol.Ask - Symbol.Bid, exitAt);
                var result = ExecuteMarketOrder(direction, SymbolName, volume, _label,
                    (double?)null, (double?)null, BuildId);
                if (!result.IsSuccessful || result.Position == null)
                    Print("T003_ORDER_FAILED,receipt={0:O},error={1}", signal.Receipt, result.Error);
                else
                {
                    _exitAt = exitAt;
                    Print("T003_ENTRY,receipt={0:O},id={1},price={2},volume={3}",
                        signal.Receipt, result.Position.Id, result.Position.EntryPrice, volume);
                }
            }
        }

        private static int StableSign(DateTime timestamp)
        {
            unchecked
            {
                ulong x = (ulong)timestamp.Ticks;
                x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
                return (x & 1) == 0 ? 1 : -1;
            }
        }

        private void CloseManaged(string reason)
        {
            if (_label == null) return;
            foreach (var position in Positions.FindAll(_label, SymbolName))
            {
                var result = ClosePosition(position);
                Print("T003_EXIT,id={0},reason={1},quote_utc={2:O},success={3},error={4}",
                    position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T003_CLOSED,id={0},reason={1},net={2},direction={3}",
                    args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }
    }
}
