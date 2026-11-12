using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T014: first-minute London PM auction continuation. Use a tick-data backtest.
    /// AuctionDates must contain preverified auction dates after calendar/news exclusions.
    /// Each control is a separate manual run with identical dates and costs.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdLondonPmAuctionContinuation : Robot
    {
        private const string BuildId = "T014_LONDON_PM_AUCTION_V1_20261104";
        private static readonly TimeSpan AuctionTime = new TimeSpan(15, 0, 0);
        private static readonly TimeSpan DecisionTime = new TimeSpan(15, 1, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(15, 4, 0);
        private static readonly TimeSpan MaxQuoteAge = TimeSpan.FromSeconds(2);
        private const int Lookback = 60;

        public enum TestVariant { Continuation, OppositeDirection, RandomDirection, UnconditionalContinuation }

        [Parameter("Test Variant", DefaultValue = TestVariant.Continuation)]
        public TestVariant Variant { get; set; }

        [Parameter("Verified Auction Dates (YYYY-MM-DD, comma separated)", DefaultValue = "")]
        public string AuctionDates { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T014_PM_AUCTION")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", DefaultValue = false)]
        public bool AllowLiveTrading { get; set; }

        private readonly HashSet<DateTime> _auctionDates = new HashSet<DateTime>();
        private readonly Queue<double> _priorMoves = new Queue<double>();
        private readonly Queue<double> _priorSpreads = new Queue<double>();
        private TimeZoneInfo _london;
        private DateTime _date = DateTime.MinValue;
        private DateTime _lastQuoteTime = DateTime.MinValue;
        private DateTime _openQuoteTime = DateTime.MinValue;
        private DateTime _decisionQuoteTime = DateTime.MinValue;
        private double _openMid;
        private double _decisionMid;
        private bool _evaluated;
        private bool _exitAttempted;
        private bool _quoteGap;
        private bool _eligibleDate;
        private string _label;

        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading)
            {
                Print("T014_STOP,reason=live_trading_disabled");
                Stop();
                return;
            }
            try
            {
                try { _london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London"); }
                catch (TimeZoneNotFoundException) { _london = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"); }
                foreach (var token in AuctionDates.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    DateTime date;
                    if (!DateTime.TryParseExact(token.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out date) || !_auctionDates.Add(date.Date))
                        throw new FormatException("Invalid or duplicate auction date: " + token);
                }
                if (_auctionDates.Count == 0)
                    throw new FormatException("Supply verified auction dates after cancellations, late starts, closures and USD-news exclusions.");
            }
            catch (Exception error)
            {
                Print("T014_STOP,reason=configuration,error={0}", error.Message);
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Variant;
            Positions.Closed += OnPositionClosed;
            Print("T014_START,build={0},variant={1},dates={2},timezone=Europe/London,threshold=prior60_p75,spread=prior60_median,exit=15:04,stop=2x_move,volume={3},tick_size={4}",
                BuildId, Variant, _auctionDates.Count, FixedVolumeInUnits, Symbol.TickSize);
        }

        protected override void OnTick()
        {
            var now = Server.Time; // Robot timezone is UTC; convert each date with London DST rules.
            var londonNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), _london);
            var date = londonNow.Date;
            if (date != _date)
            {
                if (_date != DateTime.MinValue && _eligibleDate && !_evaluated)
                    Print("T014_SKIP,date={0:yyyy-MM-dd},reason=missing_decision_quote", _date);
                CloseManaged("new_day_cleanup");
                _date = date;
                _eligibleDate = _auctionDates.Contains(date);
                _lastQuoteTime = _openQuoteTime = _decisionQuoteTime = DateTime.MinValue;
                _openMid = _decisionMid = 0;
                _evaluated = _exitAttempted = _quoteGap = false;
            }
            if (!_eligibleDate) return;

            var time = londonNow.TimeOfDay;
            if (time >= AuctionTime.Subtract(TimeSpan.FromSeconds(1)) && time <= ExitTime)
            {
                if (_lastQuoteTime != DateTime.MinValue && now - _lastQuoteTime > MaxQuoteAge)
                    _quoteGap = true;
                _lastQuoteTime = now;
            }

            if (Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid) return;
            var mid = (Symbol.Bid + Symbol.Ask) / 2.0;
            if (time >= AuctionTime.Subtract(MaxQuoteAge) && time <= AuctionTime)
            {
                _openQuoteTime = now;
                _openMid = mid;
            }
            if (time >= DecisionTime.Subtract(MaxQuoteAge) && time <= DecisionTime)
            {
                _decisionQuoteTime = now;
                _decisionMid = mid;
            }

            if (!_evaluated && time > DecisionTime)
            {
                _evaluated = true;
                Evaluate(now, time);
            }
            if (!_exitAttempted && time >= ExitTime)
            {
                _exitAttempted = true;
                CloseManaged("15:04_time_exit");
            }
        }

        private void Evaluate(DateTime now, TimeSpan time)
        {
            var localAuction = DateTime.SpecifyKind(_date.Add(AuctionTime), DateTimeKind.Unspecified);
            var localDecision = DateTime.SpecifyKind(_date.Add(DecisionTime), DateTimeKind.Unspecified);
            var auctionUtc = TimeZoneInfo.ConvertTimeToUtc(localAuction, _london);
            var decisionUtc = TimeZoneInfo.ConvertTimeToUtc(localDecision, _london);
            if (_openQuoteTime == DateTime.MinValue || _decisionQuoteTime == DateTime.MinValue
                || auctionUtc - _openQuoteTime > MaxQuoteAge
                || decisionUtc - _decisionQuoteTime > MaxQuoteAge
                || _quoteGap || now - decisionUtc > MaxQuoteAge)
            {
                Print("T014_SKIP,date={0:yyyy-MM-dd},reason=missing_or_stale_ticks,open={1:O},decision={2:O},entry={3:O},gap={4}",
                    _date, _openQuoteTime, _decisionQuoteTime, now, _quoteGap);
                return;
            }
            var move = _decisionMid - _openMid;
            var absMove = Math.Abs(move);
            var spread = Symbol.Ask - Symbol.Bid;
            var warmed = _priorMoves.Count == Lookback;
            var moveThreshold = warmed ? Percentile(_priorMoves, 0.75) : double.NaN;
            var spreadThreshold = warmed ? Percentile(_priorSpreads, 0.5) : double.NaN;
            // Today's data enter the rolling sample only after today's threshold is frozen.
            _priorMoves.Enqueue(absMove);
            _priorSpreads.Enqueue(spread);
            if (_priorMoves.Count > Lookback) { _priorMoves.Dequeue(); _priorSpreads.Dequeue(); }
            if (!warmed || move == 0 || (Variant != TestVariant.UnconditionalContinuation && absMove <= moveThreshold)
                || spread > spreadThreshold)
            {
                Print("T014_NO_TRADE,date={0:yyyy-MM-dd},reason={1},move={2},p75={3},spread={4},median_spread={5},warmup={6}",
                    _date, !warmed ? "warmup" : move == 0 ? "zero_move" : absMove <= moveThreshold && Variant != TestVariant.UnconditionalContinuation ? "below_threshold" : "wide_spread",
                    move, moveThreshold, spread, spreadThreshold, _priorMoves.Count);
                return;
            }
            var sign = Math.Sign(move);
            if (Variant == TestVariant.OppositeDirection) sign = -sign;
            if (Variant == TestVariant.RandomDirection) sign = StableSign(_date);
            var direction = sign > 0 ? TradeType.Buy : TradeType.Sell;
            var entry = direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var stopDistance = 2.0 * absMove;
            var stopPips = stopDistance / Symbol.PipSize;
            var stopPrice = direction == TradeType.Buy ? entry - stopDistance : entry + stopDistance;
            var minDistance = Symbol.MinDistanceType.ToString().IndexOf("Pip", StringComparison.OrdinalIgnoreCase) >= 0
                ? Symbol.MinStopLossDistance * Symbol.PipSize
                : entry * Symbol.MinStopLossDistance / 100.0;
            var executableDistance = direction == TradeType.Buy ? Symbol.Bid - stopPrice : stopPrice - Symbol.Ask;
            if (executableDistance <= 0 || executableDistance < minDistance)
            {
                Print("T014_SKIP,date={0:yyyy-MM-dd},reason=stop_too_close,stop_distance={1},broker_min={2},spread={3}",
                    _date, executableDistance, minDistance, spread);
                return;
            }
            var volume = Symbol.NormalizeVolumeInUnits(Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("T014_SKIP,date={0:yyyy-MM-dd},reason=volume_below_minimum", _date);
                return;
            }
            Print("T014_SIGNAL,date={0:yyyy-MM-dd},variant={1},move={2},p75={3},spread={4},median_spread={5},direction={6},quote_utc={7:O},bid={8},ask={9},stop_pips={10}",
                _date, Variant, move, moveThreshold, spread, spreadThreshold, direction, now, Symbol.Bid, Symbol.Ask, stopPips);
            var result = ExecuteMarketOrder(direction, SymbolName, volume, _label, stopPips, null, BuildId);
            if (!result.IsSuccessful || result.Position == null)
                Print("T014_ORDER_FAILED,date={0:yyyy-MM-dd},error={1}", _date, result.Error);
            else
                Print("T014_ENTRY,date={0:yyyy-MM-dd},id={1},price={2},stop={3},volume={4}",
                    _date, result.Position.Id, result.Position.EntryPrice, result.Position.StopLoss, volume);
        }

        private static double Percentile(IEnumerable<double> values, double p)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            var rank = (sorted.Length - 1) * p;
            var lower = (int)Math.Floor(rank);
            var upper = (int)Math.Ceiling(rank);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
        }

        private static int StableSign(DateTime date)
        {
            unchecked
            {
                uint x = (uint)(date.Year * 10000 + date.Month * 100 + date.Day);
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                return (x & 1) == 0 ? 1 : -1;
            }
        }

        private void CloseManaged(string reason)
        {
            if (_label == null) return;
            foreach (var position in Positions.FindAll(_label, SymbolName))
            {
                var result = ClosePosition(position);
                Print("T014_EXIT,date={0:yyyy-MM-dd},id={1},reason={2},quote_utc={3:O},success={4},error={5}",
                    _date, position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T014_CLOSED,date={0:yyyy-MM-dd},id={1},reason={2},net={3},direction={4}",
                    _date, args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnPositionClosed;
        }
    }
}
