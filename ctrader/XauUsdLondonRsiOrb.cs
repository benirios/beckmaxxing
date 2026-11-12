using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-014 / H5: London 30-minute opening-range breakout with Wilder RSI(14).
    /// Attach to an M1 XAUUSD chart. GMT Standard Time follows London DST.
    /// Research only; real-time trading is disabled unless explicitly enabled.
    /// </summary>
    [Robot(TimeZone = TimeZones.GMTStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdLondonRsiOrbV1 : Robot
    {
        private static readonly TimeSpan OpeningTime = new TimeSpan(8, 0, 0);
        private static readonly TimeSpan RangeEndTime = new TimeSpan(8, 30, 0);
        private static readonly TimeSpan EntryDeadline = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(15, 0, 0);
        private static readonly TimeSpan MaximumRsiAge = TimeSpan.FromHours(3);
        private const int RequiredOpeningBars = 30;
        private const int RsiPeriod = 14;
        private const double RsiMidpoint = 50.0;
        private const double MinimumRange = 0.60;
        private const double FixedVolumeInUnits = 1.0;
        private const string BuildId = "EXP014_LONDON_RSI_ORB_V1_20261103";

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_EXP014_LDN_RSI_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false,
            Description = "Keep false for research. Real-time instances stop immediately unless explicitly enabled.")]
        public bool AllowLiveTrading { get; set; }

        private Bars _m30Bars;
        private RelativeStrengthIndex _rsi;
        private DateTime _sessionDate;
        private double _rangeHigh;
        private double _rangeLow;
        private int _openingBarCount;
        private bool _rangeFinalized;
        private bool _rangeValid;
        private bool _longAvailable;
        private bool _shortAvailable;
        private bool _tradeConsumed;
        private bool _deadlineProcessed;
        private bool _timeExitProcessed;

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute)
            {
                Print("EXP014_ERROR,reason=requires_m1,timeframe={0}", TimeFrame);
                Stop();
                return;
            }

            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading)
            {
                Print("EXP014_ERROR,reason=live_trading_disabled");
                Stop();
                return;
            }

            _m30Bars = MarketData.GetBars(TimeFrame.Minute30, SymbolName);
            _rsi = Indicators.RelativeStrengthIndex(_m30Bars.ClosePrices, RsiPeriod);
            Positions.Closed += OnManagedPositionClosed;
            ResetSession(DateTime.MinValue.Date);

            Print("EXP014_START,build={0},symbol={1},timeframe={2},timezone={3},mode={4},session=08:00-15:00,range=08:00-08:29,entry_window=08:30-11:59:59,rsi_period={5},rsi_midpoint={6},minimum_range={7},fixed_units={8},label={9},digits={10},pip_size={11},tick_size={12},min_distance_type={13},min_sl_distance={14}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode,
                RsiPeriod, RsiMidpoint, MinimumRange, FixedVolumeInUnits,
                PositionLabel, Symbol.Digits, Symbol.PipSize, Symbol.TickSize,
                Symbol.MinDistanceType, Symbol.MinStopLossDistance);
        }

        protected override void OnBar()
        {
            if (Bars.Count < 2)
                return;

            var currentBar = Bars.LastBar;
            var closedBar = Bars.Last(1);
            var currentDate = currentBar.OpenTime.Date;

            if (_sessionDate != currentDate)
            {
                CloseManagedPositions("new_session_cleanup");
                ResetSession(currentDate);
            }

            var currentTime = currentBar.OpenTime.TimeOfDay;
            if (currentTime >= ExitTime)
            {
                if (!_timeExitProcessed)
                {
                    CloseManagedPositions(currentTime == ExitTime ? "15:00_time_exit" : "delayed_time_exit");
                    _timeExitProcessed = true;
                    Print("EXP014_EXIT_AUDIT,date={0:yyyy-MM-dd},time={1},exact={2}",
                        _sessionDate, currentTime, currentTime == ExitTime);
                }
                return;
            }

            ProcessOpeningRangeBar(closedBar);
            FinalizeOpeningRangeIfDue(currentTime);

            if (currentTime >= EntryDeadline && !_deadlineProcessed)
            {
                _deadlineProcessed = true;
                if (!_tradeConsumed)
                {
                    _tradeConsumed = true;
                    Print("EXP014_DEADLINE,date={0:yyyy-MM-dd},long_available={1},short_available={2}",
                        _sessionDate, _longAvailable, _shortAvailable);
                }
            }
        }

        protected override void OnTick()
        {
            if (!_rangeValid || _tradeConsumed || Bars.Count == 0)
                return;

            var currentBar = Bars.LastBar;
            if (currentBar.OpenTime.Date != _sessionDate)
                return;

            var currentTime = currentBar.OpenTime.TimeOfDay;
            if (currentTime < RangeEndTime || currentTime >= EntryDeadline)
                return;

            var upperTrigger = _rangeHigh + Symbol.TickSize;
            var lowerTrigger = _rangeLow - Symbol.TickSize;

            if (_longAvailable && Symbol.Bid >= upperTrigger)
                EvaluateBreakout(TradeType.Buy, upperTrigger);
            else if (_shortAvailable && Symbol.Bid <= lowerTrigger)
                EvaluateBreakout(TradeType.Sell, lowerTrigger);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnManagedPositionClosed;
        }

        protected override void OnException(Exception exception)
        {
            Print("EXP014_EXCEPTION,type={0},message={1}",
                exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _rangeHigh = double.MinValue;
            _rangeLow = double.MaxValue;
            _openingBarCount = 0;
            _rangeFinalized = false;
            _rangeValid = false;
            _longAvailable = true;
            _shortAvailable = true;
            _tradeConsumed = false;
            _deadlineProcessed = false;
            _timeExitProcessed = false;
        }

        private void ProcessOpeningRangeBar(Bar bar)
        {
            if (_rangeFinalized || bar.OpenTime.Date != _sessionDate)
                return;

            var barTime = bar.OpenTime.TimeOfDay;
            if (barTime < OpeningTime || barTime >= RangeEndTime)
                return;

            _rangeHigh = Math.Max(_rangeHigh, bar.High);
            _rangeLow = Math.Min(_rangeLow, bar.Low);
            _openingBarCount++;
        }

        private void FinalizeOpeningRangeIfDue(TimeSpan currentTime)
        {
            if (_rangeFinalized || currentTime < RangeEndTime)
                return;

            _rangeFinalized = true;
            var weekday = _sessionDate.DayOfWeek != DayOfWeek.Saturday
                && _sessionDate.DayOfWeek != DayOfWeek.Sunday;
            var width = _rangeHigh - _rangeLow;
            _rangeValid = weekday
                && _openingBarCount == RequiredOpeningBars
                && _rangeHigh > _rangeLow
                && width >= MinimumRange
                && _rangeHigh != double.MinValue
                && _rangeLow != double.MaxValue;

            if (!_rangeValid)
            {
                _tradeConsumed = true;
                Print("EXP014_RANGE_INVALID,date={0:yyyy-MM-dd},weekday={1},bars={2},required={3},high={4},low={5},width={6},minimum={7}",
                    _sessionDate, weekday, _openingBarCount, RequiredOpeningBars,
                    _rangeHigh, _rangeLow, width, MinimumRange);
                return;
            }

            Print("EXP014_RANGE,date={0:yyyy-MM-dd},high={1},low={2},width={3},bars={4},upper_trigger={5},lower_trigger={6},bid={7},ask={8},spread={9}",
                _sessionDate, _rangeHigh, _rangeLow, width, _openingBarCount,
                _rangeHigh + Symbol.TickSize, _rangeLow - Symbol.TickSize,
                Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid);
        }

        private void EvaluateBreakout(TradeType tradeType, double triggerPrice)
        {
            double rsiValue;
            DateTime rsiBarOpen;
            var rsiValid = TryGetLastCompletedRsi(out rsiValue, out rsiBarOpen);
            var rsiAgrees = rsiValid && (tradeType == TradeType.Buy
                ? rsiValue > RsiMidpoint
                : rsiValue < RsiMidpoint);

            Print("EXP014_BREAKOUT,date={0:yyyy-MM-dd},build={1},direction={2},trigger={3},bid={4},ask={5},spread={6},rsi_valid={7},rsi={8},rsi_bar={9:O},agrees={10},bar={11:O}",
                _sessionDate, BuildId, tradeType, triggerPrice, Symbol.Bid,
                Symbol.Ask, Symbol.Ask - Symbol.Bid, rsiValid,
                rsiValid ? rsiValue.ToString() : "nan", rsiBarOpen,
                rsiAgrees, Bars.LastBar.OpenTime);

            if (!rsiAgrees)
            {
                if (tradeType == TradeType.Buy)
                    _longAvailable = false;
                else
                    _shortAvailable = false;

                Print("EXP014_RSI_REJECT,date={0:yyyy-MM-dd},direction={1},rsi_valid={2},rsi={3},long_available={4},short_available={5}",
                    _sessionDate, tradeType, rsiValid,
                    rsiValid ? rsiValue.ToString() : "nan",
                    _longAvailable, _shortAvailable);
                return;
            }

            _tradeConsumed = true;
            EnterBreakout(tradeType, triggerPrice, rsiValue, rsiBarOpen);
        }

        private bool TryGetLastCompletedRsi(out double rsiValue, out DateTime rsiBarOpen)
        {
            rsiValue = double.NaN;
            rsiBarOpen = DateTime.MinValue;
            var now = Server.Time;

            for (var index = _m30Bars.Count - 1; index >= 0; index--)
            {
                var open = _m30Bars.OpenTimes[index];
                var completed = open.AddMinutes(30);
                if (completed > now)
                    continue;

                var age = now - completed;
                if (age < TimeSpan.Zero || age > MaximumRsiAge)
                    return false;

                var value = _rsi.Result[index];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    return false;

                rsiValue = value;
                rsiBarOpen = open;
                return true;
            }

            return false;
        }

        private void EnterBreakout(TradeType tradeType, double triggerPrice,
            double rsiValue, DateTime rsiBarOpen)
        {
            var preBid = Symbol.Bid;
            var preAsk = Symbol.Ask;
            var expectedEntry = tradeType == TradeType.Buy ? preAsk : preBid;
            var intendedStop = tradeType == TradeType.Buy
                ? _rangeLow - Symbol.TickSize
                : _rangeHigh + Symbol.TickSize;
            var geometryValid = tradeType == TradeType.Buy
                ? intendedStop < preBid
                : intendedStop > preAsk;

            if (!geometryValid)
            {
                Print("EXP014_REJECT,date={0:yyyy-MM-dd},reason=stop_crossed_or_inside_spread,direction={1},bid={2},ask={3},entry={4},stop={5},range_high={6},range_low={7}",
                    _sessionDate, tradeType, preBid, preAsk, expectedEntry,
                    intendedStop, _rangeHigh, _rangeLow);
                return;
            }

            var stopPips = Math.Abs(expectedEntry - intendedStop) / Symbol.PipSize;
            if (stopPips <= 0 || double.IsNaN(stopPips) || double.IsInfinity(stopPips))
            {
                Print("EXP014_REJECT,date={0:yyyy-MM-dd},reason=invalid_stop_distance,direction={1},stop_pips={2}",
                    _sessionDate, tradeType, stopPips);
                return;
            }

            var volume = Symbol.NormalizeVolumeInUnits(
                Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax),
                RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("EXP014_REJECT,date={0:yyyy-MM-dd},reason=volume_below_minimum,requested={1},normalized={2},minimum={3}",
                    _sessionDate, FixedVolumeInUnits, volume,
                    Symbol.VolumeInUnitsMin);
                return;
            }

            var comment = string.Format(
                "EXP014 London RSI ORB; RSI={0:F4}; OR={1}-{2}; trigger={3}",
                rsiValue, _rangeLow, _rangeHigh, triggerPrice);
            var result = ExecuteMarketOrder(tradeType, SymbolName, volume,
                PositionLabel, stopPips, null, comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print("EXP014_ORDER_FAILED,date={0:yyyy-MM-dd},direction={1},error={2}",
                    _sessionDate, tradeType, result.Error);
                return;
            }

            var position = result.Position;
            var actualStop = position.StopLoss;
            var protectionValid = actualStop.HasValue
                && (tradeType == TradeType.Buy
                    ? actualStop.Value < position.EntryPrice
                    : actualStop.Value > position.EntryPrice);

            Print("EXP014_ENTRY,date={0:yyyy-MM-dd},build={1},direction={2},trigger={3},pre_bid={4},pre_ask={5},pre_spread={6},entry={7},intended_stop={8},actual_stop={9},stop_pips={10},volume={11},rsi={12},rsi_bar={13:O},range_high={14},range_low={15}",
                _sessionDate, BuildId, tradeType, triggerPrice, preBid, preAsk,
                preAsk - preBid, position.EntryPrice, intendedStop,
                actualStop.HasValue ? actualStop.Value.ToString() : "null",
                stopPips, position.VolumeInUnits, rsiValue, rsiBarOpen,
                _rangeHigh, _rangeLow);

            if (protectionValid)
                return;

            Print("EXP014_PROTECTION_FAILED,date={0:yyyy-MM-dd},position={1},direction={2},entry={3},actual_stop={4}",
                _sessionDate, position.Id, tradeType, position.EntryPrice,
                actualStop.HasValue ? actualStop.Value.ToString() : "null");
            var closeResult = ClosePosition(position);
            Print("EXP014_PROTECTION_CLOSE,position={0},success={1},error={2}",
                position.Id, closeResult.IsSuccessful, closeResult.Error);
        }

        private void CloseManagedPositions(string reason)
        {
            foreach (var position in Positions.FindAll(PositionLabel, SymbolName))
            {
                var result = ClosePosition(position);
                Print("EXP014_CLOSE_REQUEST,position={0},reason={1},success={2},error={3}",
                    position.Id, reason, result.IsSuccessful, result.Error);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != PositionLabel || position.SymbolName != SymbolName)
                return;

            Print("EXP014_CLOSED,position={0},date={1:yyyy-MM-dd},direction={2},entry={3},reason={4},net_profit={5},pips={6}",
                position.Id, position.EntryTime, position.TradeType,
                position.EntryPrice, args.Reason, position.NetProfit,
                position.Pips);
        }
    }
}
