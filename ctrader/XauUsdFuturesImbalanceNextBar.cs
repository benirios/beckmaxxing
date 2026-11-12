using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T024: enter XAUUSD in the sign of an externally measured, COMPLETED
    /// CME gold five-minute taker-volume imbalance. Input timestamps are UTC
    /// feed-receipt times, after the exchange bar and threshold classification.
    /// Entry is the first broker tick strictly after receipt; exit follows the
    /// next complete UTC M5 bar. Exclude scheduled-news events before input.
    /// Research only; real-time trading is disabled.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdFuturesImbalanceNextBar : Robot
    {
        private const string BuildId = "T024_FUTURES_IMBALANCE_V1_20261104";
        private static readonly TimeSpan MaxEntryDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan BarLength = TimeSpan.FromMinutes(5);

        public enum TestVariant { FollowImbalance, OppositeSignControl, RandomDirectionControl }

        [Parameter("Test Variant", DefaultValue = TestVariant.FollowImbalance)]
        public TestVariant Variant { get; set; }

        [Parameter("Verified Receipts UTC (YYYY-MM-DDTHH:mm:ssZ=+1/-1, comma separated)", DefaultValue = "")]
        public string SignedReceiptsUtc { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T024_IMBALANCE")]
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
                Print("T024_STOP,reason=research_only_no_live_trading");
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
                    throw new FormatException("Point-in-time CME imbalance receipts are required.");
                _signals.Sort((a, b) => a.Receipt.CompareTo(b.Receipt));
            }
            catch (Exception error)
            {
                Print("T024_STOP,reason=configuration,error={0}", error.Message);
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Variant;
            Positions.Closed += OnPositionClosed;
            Print("T024_START,build={0},variant={1},signals={2},max_entry_delay=2s,exit=next_full_UTC_M5_bar,volume={3},tick_size={4}",
                BuildId, Variant, _signals.Count, FixedVolumeInUnits, Symbol.TickSize);
        }

        protected override void OnTick()
        {
            var now = DateTime.SpecifyKind(Server.Time, DateTimeKind.Utc);
            if (_exitAt != DateTime.MinValue && now >= _exitAt)
            {
                CloseManaged("next_full_m5_bar_exit");
                _exitAt = DateTime.MinValue;
            }
            while (_nextSignal < _signals.Count && _signals[_nextSignal].Receipt < now)
            {
                var signal = _signals[_nextSignal++];
                if (now - signal.Receipt > MaxEntryDelay)
                {
                    Print("T024_SKIP,receipt={0:O},reason=late_or_missing_quote,first_quote={1:O}", signal.Receipt, now);
                    continue;
                }
                if (Positions.FindAll(_label, SymbolName).Length > 0)
                {
                    Print("T024_SKIP,receipt={0:O},reason=overlapping_position", signal.Receipt);
                    continue;
                }
                if (Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid)
                {
                    Print("T024_SKIP,receipt={0:O},reason=invalid_bid_ask", signal.Receipt);
                    continue;
                }
                var volume = Symbol.NormalizeVolumeInUnits(
                    Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
                if (volume < Symbol.VolumeInUnitsMin)
                {
                    Print("T024_SKIP,receipt={0:O},reason=volume_below_minimum", signal.Receipt);
                    continue;
                }
                var sign = Variant == TestVariant.FollowImbalance ? signal.Sign
                    : Variant == TestVariant.OppositeSignControl ? -signal.Sign
                    : StableSign(signal.Receipt);
                var direction = sign > 0 ? TradeType.Buy : TradeType.Sell;
                var firstBarBoundary = new DateTime(
                    now.Ticks - now.Ticks % BarLength.Ticks + BarLength.Ticks, DateTimeKind.Utc);
                var exitAt = firstBarBoundary.Add(BarLength);
                Print("T024_SIGNAL,receipt={0:O},entry_quote={1:O},imbalance_sign={2},variant={3},direction={4},bid={5},ask={6},spread={7},exit_at={8:O}",
                    signal.Receipt, now, signal.Sign, Variant, direction, Symbol.Bid, Symbol.Ask,
                    Symbol.Ask - Symbol.Bid, exitAt);
                var result = ExecuteMarketOrder(direction, SymbolName, volume, _label,
                    (double?)null, (double?)null, BuildId);
                if (!result.IsSuccessful || result.Position == null)
                    Print("T024_ORDER_FAILED,receipt={0:O},error={1}", signal.Receipt, result.Error);
                else
                {
                    _exitAt = exitAt;
                    Print("T024_ENTRY,receipt={0:O},id={1},price={2},volume={3}",
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
                Print("T024_EXIT,id={0},reason={1},quote_utc={2:O},success={3},error={4}",
                    position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T024_CLOSED,id={0},reason={1},net={2},direction={3}",
                    args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }
    }
}
