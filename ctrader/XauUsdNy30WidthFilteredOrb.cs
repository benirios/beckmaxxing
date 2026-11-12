using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-005 / H1: New York 30-minute XAUUSD ORB filtered by opening-range width.
    /// Attach to an M1 chart. Times use US Eastern time and follow DST.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdNy30WidthFilteredOrb : Robot
    {
        private static readonly TimeSpan OpeningTime = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan RangeEndTime = new TimeSpan(10, 0, 0);
        private static readonly TimeSpan EntryDeadline = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(16, 0, 0);
        private const int RequiredOpeningBars = 30;
        private const int WidthLookbackSessions = 60;
        private const double WidthPercentile = 0.70;
        private const double TargetR = 1.0;
        private const string BuildId = "EXP005_NY30_WIDTH_P70_L60_1R_V1_20261030";

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

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_NY30_WIDTH_P70_V1")]
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
        private bool _activityFilterPassed;
        private double _activityThreshold;
        private bool _tradeTaken;
        private bool _timeExitProcessed;
        private readonly Queue<double> _priorRangeWidths = new Queue<double>();

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

            Print("ORB30VF_START,build={0},symbol={1},timeframe={2},timezone={3},mode={4},target_r={5},width_lookback={6},width_percentile={7},label={8},digits={9},pip_size={10},tick_size={11},min_distance_type={12},min_sl_distance={13},min_tp_distance={14}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode, TargetR,
                WidthLookbackSessions, WidthPercentile, PositionLabel,
                Symbol.Digits, Symbol.PipSize, Symbol.TickSize,
                Symbol.MinDistanceType, Symbol.MinStopLossDistance, Symbol.MinTakeProfitDistance);
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

            if (!_rangeValid || !_activityFilterPassed || _tradeTaken
                || currentTime < RangeEndTime || currentTime > EntryDeadline)
                return;

            var closedTime = closedBar.OpenTime.TimeOfDay;
            if (closedBar.OpenTime.Date != _sessionDate || closedTime < RangeEndTime || closedTime >= EntryDeadline)
                return;

            if (closedBar.Close > _rangeHigh)
                ConsumeSignalAndEnter(TradeType.Buy, closedBar);
            else if (closedBar.Close < _rangeLow)
                ConsumeSignalAndEnter(TradeType.Sell, closedBar);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnManagedPositionClosed;
        }

        protected override void OnException(Exception exception)
        {
            Print("ORB30VF_EXCEPTION,type={0},message={1}", exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _rangeHigh = double.MinValue;
            _rangeLow = double.MaxValue;
            _openingBarCount = 0;
            _rangeFinalized = false;
            _rangeValid = false;
            _activityFilterPassed = false;
            _activityThreshold = double.NaN;
            _tradeTaken = false;
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
                Print("ORB30VF_RANGE_INVALID,date={0:yyyy-MM-dd},bars={1},required={2}",
                    _sessionDate, _openingBarCount, RequiredOpeningBars);
                return;
            }

            var currentWidth = _rangeHigh - _rangeLow;
            Print("ORB30VF_RANGE,date={0:yyyy-MM-dd},high={1},low={2},width={3},bars={4}",
                _sessionDate, _rangeHigh, _rangeLow, currentWidth, _openingBarCount);

            if (_priorRangeWidths.Count < WidthLookbackSessions)
            {
                Print("ORB30VF_FILTER_WARMUP,date={0:yyyy-MM-dd},width={1},history_count={2},required={3},pass=False",
                    _sessionDate, currentWidth, _priorRangeWidths.Count, WidthLookbackSessions);
                AddCurrentWidthAfterDecision(currentWidth);
                return;
            }

            _activityThreshold = CalculateNearestRankPercentile(
                _priorRangeWidths, WidthPercentile);
            _activityFilterPassed = currentWidth >= _activityThreshold;

            Print("ORB30VF_FILTER,date={0:yyyy-MM-dd},width={1},history_count={2},percentile={3},nearest_rank={4},threshold={5},pass={6}",
                _sessionDate, currentWidth, _priorRangeWidths.Count, WidthPercentile,
                (int)Math.Ceiling(WidthPercentile * _priorRangeWidths.Count),
                _activityThreshold, _activityFilterPassed);

            AddCurrentWidthAfterDecision(currentWidth);
        }

        private static double CalculateNearestRankPercentile(
            IEnumerable<double> values, double percentile)
        {
            var sorted = new List<double>(values);
            sorted.Sort();

            if (sorted.Count == 0)
                return double.NaN;

            var oneBasedRank = (int)Math.Ceiling(percentile * sorted.Count);
            oneBasedRank = Math.Max(1, Math.Min(oneBasedRank, sorted.Count));
            return sorted[oneBasedRank - 1];
        }

        private void AddCurrentWidthAfterDecision(double currentWidth)
        {
            _priorRangeWidths.Enqueue(currentWidth);
            while (_priorRangeWidths.Count > WidthLookbackSessions)
                _priorRangeWidths.Dequeue();
        }

        private void ConsumeSignalAndEnter(TradeType tradeType, Bar confirmationBar)
        {
            // The first eligible signal consumes the session even if execution is rejected.
            _tradeTaken = true;

            var entryBid = Symbol.Bid;
            var entryAsk = Symbol.Ask;
            var entrySpread = entryAsk - entryBid;
            var expectedEntry = tradeType == TradeType.Buy ? entryAsk : entryBid;
            var stopPrice = tradeType == TradeType.Buy ? _rangeLow : _rangeHigh;

            var executableGeometryValid = tradeType == TradeType.Buy
                ? stopPrice < entryBid
                : stopPrice > entryAsk;

            if (!executableGeometryValid)
            {
                Print("ORB30VF_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=stop_crossed_or_inside_spread,direction={1},bid={2},ask={3},spread={4},stop={5},range_high={6},range_low={7}",
                    _sessionDate, tradeType, entryBid, entryAsk, entrySpread,
                    stopPrice, _rangeHigh, _rangeLow);
                return;
            }

            var stopPips = Math.Abs(expectedEntry - stopPrice) / Symbol.PipSize;
            if (stopPips <= 0 || double.IsNaN(stopPips) || double.IsInfinity(stopPips))
            {
                Print("ORB30VF_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=invalid_stop_distance", _sessionDate);
                return;
            }

            var volume = ResolveVolume(stopPips);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("ORB30VF_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=volume_below_minimum,calculated={1},minimum={2}",
                    _sessionDate, volume, Symbol.VolumeInUnitsMin);
                return;
            }

            var targetPips = stopPips * TargetR;
            var comment = string.Format("NY30 P70/L60 width-filtered 1R; OR={0}-{1}; threshold={2}; signal={3:O}",
                _rangeLow, _rangeHigh, _activityThreshold, confirmationBar.OpenTime);

            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, PositionLabel,
                stopPips, targetPips, comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print("ORB30VF_ORDER_FAILED,date={0:yyyy-MM-dd},direction={1},error={2}",
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

            Print("ORB30VF_ENTRY,date={0:yyyy-MM-dd},build={1},direction={2},signal_close={3},pre_bid={4},pre_ask={5},pre_spread={6},entry={7},actual_stop={8},actual_target={9},stop_pips={10},target_pips={11},volume={12},range_high={13},range_low={14},range_width={15},filter_threshold={16}",
                _sessionDate, BuildId, tradeType, confirmationBar.Close,
                entryBid, entryAsk, entrySpread, position.EntryPrice,
                actualStop.HasValue ? actualStop.Value.ToString() : "null",
                actualTarget.HasValue ? actualTarget.Value.ToString() : "null",
                stopPips, targetPips, position.VolumeInUnits, _rangeHigh, _rangeLow,
                _rangeHigh - _rangeLow, _activityThreshold);

            if (protectionDirectionValid)
                return;

            Print("ORB30VF_PROTECTION_FAILED,date={0:yyyy-MM-dd},build={1},position={2},direction={3},entry={4},actual_stop={5},actual_target={6},reason=missing_or_wrong_side_protection",
                _sessionDate, BuildId, position.Id, tradeType, position.EntryPrice,
                actualStop.HasValue ? actualStop.Value.ToString() : "null",
                actualTarget.HasValue ? actualTarget.Value.ToString() : "null");

            var closeResult = ClosePosition(position);
            Print("ORB30VF_PROTECTION_CLOSE,position={0},success={1},error={2}",
                position.Id, closeResult.IsSuccessful, closeResult.Error);
        }

        private double ResolveVolume(double stopLossPips)
        {
            double requestedVolume;
            if (SizingMode == PositionSizingMode.PercentEquityRisk)
            {
                var riskAmount = Account.Equity * RiskPercent / 100.0;
                requestedVolume = Symbol.VolumeForFixedRisk(riskAmount, stopLossPips, RoundingMode.Down);
            }
            else
            {
                requestedVolume = FixedVolumeInUnits;
            }

            if (double.IsNaN(requestedVolume) || double.IsInfinity(requestedVolume) || requestedVolume <= 0)
                return 0;

            requestedVolume = Math.Min(requestedVolume, Symbol.VolumeInUnitsMax);
            return Symbol.NormalizeVolumeInUnits(requestedVolume, RoundingMode.Down);
        }

        private void CloseManagedPositions(string reason)
        {
            foreach (var position in Positions.FindAll(PositionLabel, SymbolName))
            {
                var result = ClosePosition(position);
                Print("ORB30VF_CLOSE_REQUEST,position={0},reason={1},success={2},error={3}",
                    position.Id, reason, result.IsSuccessful, result.Error);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != PositionLabel || position.SymbolName != SymbolName)
                return;

            Print("ORB30VF_CLOSED,position={0},date={1:yyyy-MM-dd},direction={2},entry={3},reason={4},net_profit={5},pips={6}",
                position.Id, position.EntryTime, position.TradeType, position.EntryPrice,
                args.Reason, position.NetProfit, position.Pips);
        }
    }
}
