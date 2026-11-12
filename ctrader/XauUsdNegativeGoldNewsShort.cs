using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T020 exploratory short after a delivered negative gold-news update.
    /// Supply point-in-time, audited UTC DELIVERY timestamps, after scheduled-news
    /// exclusions. Do not use article creation times or XAUUSD price to classify.
    /// Entry is the first broker tick strictly after delivery, within two seconds.
    /// Exit is the first tick at/after the end of the next full UTC M5 bar.
    /// The deterministic random-direction variant uses the identical events.
    /// Research only: real-time trading is always disabled.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdNegativeGoldNewsShort : Robot
    {
        private const string BuildId = "T020_NEGATIVE_GOLD_NEWS_V1_20261104";
        private static readonly TimeSpan MaxEntryDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan BarLength = TimeSpan.FromMinutes(5);

        public enum TestVariant { NegativeNewsShort, RandomDirectionControl }

        [Parameter("Test Variant", DefaultValue = TestVariant.NegativeNewsShort)]
        public TestVariant Variant { get; set; }

        [Parameter("Verified Delivery Times UTC (YYYY-MM-DDTHH:mm:ssZ, comma separated)", DefaultValue = "")]
        public string DeliveryTimesUtc { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T020_NEG_NEWS")]
        public string PositionLabel { get; set; }

        private readonly List<DateTime> _deliveries = new List<DateTime>();
        private int _nextDelivery;
        private DateTime _exitAt = DateTime.MinValue;
        private string _label;

        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime)
            {
                Print("T020_STOP,reason=research_only_no_live_trading");
                Stop();
                return;
            }
            try
            {
                var seen = new HashSet<DateTime>();
                foreach (var token in (DeliveryTimesUtc ?? "").Split(
                    new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    DateTime delivery;
                    if (!DateTime.TryParseExact(token.Trim(), new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out delivery) || !seen.Add(delivery))
                        throw new FormatException("Invalid or duplicate UTC delivery timestamp: " + token);
                    _deliveries.Add(delivery);
                }
                if (_deliveries.Count == 0)
                    throw new FormatException("Verified point-in-time news delivery timestamps are required.");
                _deliveries.Sort();
            }
            catch (Exception error)
            {
                Print("T020_STOP,reason=configuration,error={0}", error.Message);
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Variant;
            Positions.Closed += OnPositionClosed;
            Print("T020_START,build={0},variant={1},deliveries={2},max_entry_delay=2s,exit=next_full_UTC_M5_bar,volume={3},symbol={4},tick_size={5}",
                BuildId, Variant, _deliveries.Count, FixedVolumeInUnits, SymbolName, Symbol.TickSize);
        }

        protected override void OnTick()
        {
            var now = DateTime.SpecifyKind(Server.Time, DateTimeKind.Utc);
            if (_exitAt != DateTime.MinValue && now >= _exitAt)
            {
                CloseManaged("next_full_m5_bar_exit");
                _exitAt = DateTime.MinValue;
            }
            while (_nextDelivery < _deliveries.Count && _deliveries[_nextDelivery] < now)
            {
                var delivery = _deliveries[_nextDelivery++];
                if (now - delivery > MaxEntryDelay)
                {
                    Print("T020_SKIP,delivery={0:O},reason=late_or_missing_quote,first_quote={1:O}", delivery, now);
                    continue;
                }
                if (Positions.FindAll(_label, SymbolName).Length > 0)
                {
                    Print("T020_SKIP,delivery={0:O},reason=overlapping_position", delivery);
                    continue;
                }
                if (Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid)
                {
                    Print("T020_SKIP,delivery={0:O},reason=invalid_bid_ask", delivery);
                    continue;
                }
                var volume = Symbol.NormalizeVolumeInUnits(
                    Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
                if (volume < Symbol.VolumeInUnitsMin)
                {
                    Print("T020_SKIP,delivery={0:O},reason=volume_below_minimum", delivery);
                    continue;
                }
                var direction = Variant == TestVariant.NegativeNewsShort || StableSign(delivery) < 0
                    ? TradeType.Sell : TradeType.Buy;
                var firstBarBoundary = new DateTime(
                    now.Ticks - now.Ticks % BarLength.Ticks + BarLength.Ticks, DateTimeKind.Utc);
                var exitAt = firstBarBoundary.Add(BarLength);
                Print("T020_SIGNAL,delivery={0:O},entry_quote={1:O},variant={2},direction={3},bid={4},ask={5},spread={6},exit_at={7:O}",
                    delivery, now, Variant, direction, Symbol.Bid, Symbol.Ask,
                    Symbol.Ask - Symbol.Bid, exitAt);
                var result = ExecuteMarketOrder(direction, SymbolName, volume, _label,
                    (double?)null, (double?)null, BuildId);
                if (!result.IsSuccessful || result.Position == null)
                    Print("T020_ORDER_FAILED,delivery={0:O},error={1}", delivery, result.Error);
                else
                {
                    _exitAt = exitAt;
                    Print("T020_ENTRY,delivery={0:O},id={1},price={2},volume={3}",
                        delivery, result.Position.Id, result.Position.EntryPrice, volume);
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
                Print("T020_EXIT,id={0},reason={1},quote_utc={2:O},success={3},error={4}",
                    position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T020_CLOSED,id={0},reason={1},net={2},direction={3}",
                    args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }
    }
}
