using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-007 / H2: New York 15-minute XAUUSD immediate stop-entry benchmark.
    /// Attach to an M1 chart. Times use US Eastern time and follow DST.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdNy15ImmediateStopOrb : Robot
    {
        private static readonly TimeSpan OpeningTime = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan RangeEndTime = new TimeSpan(9, 45, 0);
        private static readonly TimeSpan EntryDeadline = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(16, 0, 0);
        private const int RequiredOpeningBars = 15;
        private const double TargetR = 1.0;
        private const string BuildId = "EXP007_NY15_IMMEDIATE_STOP_1R_V1_20261101";

        public enum PositionSizingMode
        {
            PercentEquityRisk,
            FixedVolumeInUnits
        }

        [Parameter("Sizing Mode", Group = "Risk", DefaultValue = PositionSizingMode.PercentEquityRisk)]
        public PositionSizingMode SizingMode { get; set; }

        [Parameter("Risk % of Equity", Group = "Risk", DefaultValue = 0.5, MinValue = 0.01, MaxValue = 5.0, Step = 0.01)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Risk", DefaultValue = 1000.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_NY15_IMMEDIATE_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false,
            Description = "Keep false for research. Real-time instances stop immediately unless explicitly enabled.")]
        public bool AllowLiveTrading { get; set; }

        private DateTime _sessionDate;
        private double _rangeHigh;
        private double _rangeLow;
        private int _openingBarCount;
        private bool _rangeFinalized;
        private bool _rangeValid;
        private bool _tradeConsumed;
        private bool _deadlineProcessed;
        private bool _timeExitProcessed;

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute)
            {
                Print("ERROR: Attach this cBot to an M1 chart. Current timeframe: {0}", TimeFrame);
                Stop();
                return;
            }

            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading)
            {
                Print("SAFETY STOP: Allow Live Trading is false. Use Backtesting, or explicitly enable live trading.");
                Stop();
                return;
            }

            Positions.Closed += OnManagedPositionClosed;
            ResetSession(DateTime.MinValue.Date);

            Print("ORB15IS_START,build={0},symbol={1},timeframe={2},timezone={3},mode={4},target_r={5},entry_deadline={6},trigger_quote=Bid,label={7},digits={8},pip_size={9},tick_size={10},min_distance_type={11},min_sl_distance={12},min_tp_distance={13}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode,
                TargetR, EntryDeadline, PositionLabel, Symbol.Digits, Symbol.PipSize,
                Symbol.TickSize, Symbol.MinDistanceType, Symbol.MinStopLossDistance,
                Symbol.MinTakeProfitDistance);
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
                    CloseManagedPositions("16:00_time_exit");
                    _timeExitProcessed = true;
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
                    Print("ORB15IS_DEADLINE,date={0:yyyy-MM-dd},reason=no_boundary_trigger_before_deadline",
                        _sessionDate);
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

            if (Symbol.Bid > _rangeHigh)
                ConsumeTriggerAndEnter(TradeType.Buy);
            else if (Symbol.Bid < _rangeLow)
                ConsumeTriggerAndEnter(TradeType.Sell);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnManagedPositionClosed;
        }

        protected override void OnException(Exception exception)
        {
            Print("ORB15IS_EXCEPTION,type={0},message={1}", exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _rangeHigh = double.MinValue;
            _rangeLow = double.MaxValue;
            _openingBarCount = 0;
            _rangeFinalized = false;
            _rangeValid = false;
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
            _rangeValid = _openingBarCount == RequiredOpeningBars
                && _rangeHigh > _rangeLow
                && _rangeHigh != double.MinValue
                && _rangeLow != double.MaxValue;

            if (!_rangeValid)
            {
                _tradeConsumed = true;
                Print("ORB15IS_RANGE_INVALID,date={0:yyyy-MM-dd},bars={1},required={2}",
                    _sessionDate, _openingBarCount, RequiredOpeningBars);
                return;
            }

            Print("ORB15IS_RANGE,date={0:yyyy-MM-dd},high={1},low={2},width={3},bars={4},placement_bid={5},placement_ask={6},placement_spread={7}",
                _sessionDate, _rangeHigh, _rangeLow, _rangeHigh - _rangeLow,
                _openingBarCount, Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid);
        }

        private void ConsumeTriggerAndEnter(TradeType tradeType)
        {
            // The first boundary trigger is the OCO winner and consumes the session,
            // even if sizing or execution is rejected.
            _tradeConsumed = true;

            var triggerBid = Symbol.Bid;
            var triggerAsk = Symbol.Ask;
            var triggerSpread = triggerAsk - triggerBid;
            var expectedEntry = tradeType == TradeType.Buy ? triggerAsk : triggerBid;
            var triggerBoundary = tradeType == TradeType.Buy ? _rangeHigh : _rangeLow;
            var stopPrice = tradeType == TradeType.Buy ? _rangeLow : _rangeHigh;

            Print("ORB15IS_TRIGGER,date={0:yyyy-MM-dd},build={1},direction={2},boundary={3},bid={4},ask={5},spread={6},bar={7:O}",
                _sessionDate, BuildId, tradeType, triggerBoundary, triggerBid,
                triggerAsk, triggerSpread, Bars.LastBar.OpenTime);

            var executableGeometryValid = tradeType == TradeType.Buy
                ? stopPrice < triggerBid
                : stopPrice > triggerAsk;

            if (!executableGeometryValid)
            {
                Print("ORB15IS_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=stop_crossed_or_inside_spread,direction={1},bid={2},ask={3},spread={4},stop={5},range_high={6},range_low={7}",
                    _sessionDate, tradeType, triggerBid, triggerAsk, triggerSpread,
                    stopPrice, _rangeHigh, _rangeLow);
                return;
            }

            var stopPips = Math.Abs(expectedEntry - stopPrice) / Symbol.PipSize;
            if (stopPips <= 0 || double.IsNaN(stopPips) || double.IsInfinity(stopPips))
            {
                Print("ORB15IS_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=invalid_stop_distance",
                    _sessionDate);
                return;
            }

            var volume = ResolveVolume(stopPips);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("ORB15IS_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=volume_below_minimum,calculated={1},minimum={2}",
                    _sessionDate, volume, Symbol.VolumeInUnitsMin);
                return;
            }

            var targetPips = stopPips * TargetR;
            var comment = string.Format(
                "NY15 immediate stop-entry 1R; OR={0}-{1}; trigger_bid={2}; trigger_bar={3:O}",
                _rangeLow, _rangeHigh, triggerBid, Bars.LastBar.OpenTime);

            var result = ExecuteMarketOrder(tradeType, SymbolName, volume,
                PositionLabel, stopPips, targetPips, comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print("ORB15IS_ORDER_FAILED,date={0:yyyy-MM-dd},direction={1},error={2}",
                    _sessionDate, tradeType, result.Error);
                return;
            }

            var position = result.Position;
            var actualStop = position.StopLoss;
            var actualTarget = position.TakeProfit;
            var protectionPresent = actualStop.HasValue && actualTarget.HasValue;
            var protectionDirectionValid = protectionPresent
                && (tradeType == TradeType.Buy
                    ? actualStop.Value < position.EntryPrice && actualTarget.Value > position.EntryPrice
                    : actualStop.Value > position.EntryPrice && actualTarget.Value < position.EntryPrice);

            Print("ORB15IS_ENTRY,date={0:yyyy-MM-dd},build={1},direction={2},trigger_boundary={3},trigger_bid={4},trigger_ask={5},trigger_spread={6},entry={7},actual_stop={8},actual_target={9},stop_pips={10},target_pips={11},volume={12},range_high={13},range_low={14}",
                _sessionDate, BuildId, tradeType, triggerBoundary, triggerBid,
                triggerAsk, triggerSpread, position.EntryPrice,
                actualStop.HasValue ? actualStop.Value.ToString() : "null",
                actualTarget.HasValue ? actualTarget.Value.ToString() : "null",
                stopPips, targetPips, position.VolumeInUnits, _rangeHigh, _rangeLow);

            if (protectionDirectionValid)
                return;

            Print("ORB15IS_PROTECTION_FAILED,date={0:yyyy-MM-dd},build={1},position={2},direction={3},entry={4},actual_stop={5},actual_target={6},reason=missing_or_wrong_side_protection",
                _sessionDate, BuildId, position.Id, tradeType, position.EntryPrice,
                actualStop.HasValue ? actualStop.Value.ToString() : "null",
                actualTarget.HasValue ? actualTarget.Value.ToString() : "null");

            var closeResult = ClosePosition(position);
            Print("ORB15IS_PROTECTION_CLOSE,position={0},success={1},error={2}",
                position.Id, closeResult.IsSuccessful, closeResult.Error);
        }

        private double ResolveVolume(double stopLossPips)
        {
            double requestedVolume;
            if (SizingMode == PositionSizingMode.PercentEquityRisk)
            {
                var riskAmount = Account.Equity * RiskPercent / 100.0;
                requestedVolume = Symbol.VolumeForFixedRisk(
                    riskAmount, stopLossPips, RoundingMode.Down);
            }
            else
            {
                requestedVolume = FixedVolumeInUnits;
            }

            if (double.IsNaN(requestedVolume)
                || double.IsInfinity(requestedVolume)
                || requestedVolume <= 0)
                return 0;

            requestedVolume = Math.Min(requestedVolume, Symbol.VolumeInUnitsMax);
            return Symbol.NormalizeVolumeInUnits(requestedVolume, RoundingMode.Down);
        }

        private void CloseManagedPositions(string reason)
        {
            foreach (var position in Positions.FindAll(PositionLabel, SymbolName))
            {
                var result = ClosePosition(position);
                Print("ORB15IS_CLOSE_REQUEST,position={0},reason={1},success={2},error={3}",
                    position.Id, reason, result.IsSuccessful, result.Error);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != PositionLabel || position.SymbolName != SymbolName)
                return;

            Print("ORB15IS_CLOSED,position={0},date={1:yyyy-MM-dd},direction={2},entry={3},reason={4},net_profit={5},pips={6}",
                position.Id, position.EntryTime, position.TradeType,
                position.EntryPrice, args.Reason, position.NetProfit, position.Pips);
        }
    }
}
