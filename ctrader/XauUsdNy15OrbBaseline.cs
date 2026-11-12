using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    public enum OrbPositionSizingMode
    {
        PercentEquityRisk,
        FixedVolumeInUnits
    }

    /// <summary>
    /// Research cBot for EXP-001 / H2: New York 15-minute XAUUSD ORB.
    /// Attach to an M1 chart. Times use US Eastern time and follow DST.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdNy15OrbBaseline : Robot
    {
        private static readonly TimeSpan OpeningTime = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan RangeEndTime = new TimeSpan(9, 45, 0);
        private static readonly TimeSpan EntryDeadline = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(16, 0, 0);
        private const int RequiredOpeningBars = 15;

        [Parameter("Target (R)", Group = "Strategy", DefaultValue = 1.0, MinValue = 0.25, MaxValue = 5.0, Step = 0.25)]
        public double TargetR { get; set; }

        [Parameter("Sizing Mode", Group = "Risk", DefaultValue = OrbPositionSizingMode.PercentEquityRisk)]
        public OrbPositionSizingMode SizingMode { get; set; }

        [Parameter("Risk % of Equity", Group = "Risk", DefaultValue = 0.5, MinValue = 0.01, MaxValue = 5.0, Step = 0.01)]
        public double RiskPercent { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Risk", DefaultValue = 1000.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_NY15_ORB_V1")]
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
        private bool _tradeTaken;
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
                Print("SAFETY STOP: Allow Live Trading is false. Use Backtesting/Optimization, or explicitly enable live trading.");
                Stop();
                return;
            }

            Positions.Closed += OnManagedPositionClosed;
            ResetSession(DateTime.MinValue.Date);

            Print(
                "ORB_START,symbol={0},timeframe={1},timezone={2},mode={3},target_r={4},label={5}",
                SymbolName,
                TimeFrame,
                TimeZone.DisplayName,
                RunningMode,
                TargetR,
                PositionLabel);
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

            if (!_rangeValid || _tradeTaken || currentTime < RangeEndTime || currentTime > EntryDeadline)
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
            Print("ORB_EXCEPTION,type={0},message={1}", exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _rangeHigh = double.MinValue;
            _rangeLow = double.MaxValue;
            _openingBarCount = 0;
            _rangeFinalized = false;
            _rangeValid = false;
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
                Print(
                    "ORB_RANGE_INVALID,date={0:yyyy-MM-dd},bars={1},required={2}",
                    _sessionDate,
                    _openingBarCount,
                    RequiredOpeningBars);
                return;
            }

            Print(
                "ORB_RANGE,date={0:yyyy-MM-dd},high={1},low={2},bars={3}",
                _sessionDate,
                _rangeHigh,
                _rangeLow,
                _openingBarCount);
        }

        private void ConsumeSignalAndEnter(TradeType tradeType, Bar confirmationBar)
        {
            // The first eligible signal consumes the session even if the broker rejects
            // the order. This prevents later bars from silently changing the experiment.
            _tradeTaken = true;

            var expectedEntry = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var stopPrice = tradeType == TradeType.Buy ? _rangeLow : _rangeHigh;
            var estimatedStopPips = Math.Abs(expectedEntry - stopPrice) / Symbol.PipSize;

            if (estimatedStopPips <= 0 || double.IsNaN(estimatedStopPips) || double.IsInfinity(estimatedStopPips))
            {
                Print("ORB_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=invalid_stop_distance", _sessionDate);
                return;
            }

            if ((tradeType == TradeType.Buy && expectedEntry <= stopPrice)
                || (tradeType == TradeType.Sell && expectedEntry >= stopPrice))
            {
                Print("ORB_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=gap_beyond_stop", _sessionDate);
                return;
            }

            var volume = ResolveVolume(estimatedStopPips);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print(
                    "ORB_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=volume_below_minimum,calculated={1},minimum={2}",
                    _sessionDate,
                    volume,
                    Symbol.VolumeInUnitsMin);
                return;
            }

            var comment = string.Format(
                "NY15 close-confirmed; OR={0}-{1}; signal={2:O}",
                _rangeLow,
                _rangeHigh,
                confirmationBar.OpenTime);

            var estimatedTargetPips = estimatedStopPips * TargetR;
            var result = ExecuteMarketOrder(
                tradeType,
                SymbolName,
                volume,
                PositionLabel,
                estimatedStopPips,
                estimatedTargetPips,
                comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print(
                    "ORB_ORDER_FAILED,date={0:yyyy-MM-dd},direction={1},error={2}",
                    _sessionDate,
                    tradeType,
                    result.Error);
                return;
            }

            var position = result.Position;
            var exactStop = Math.Round(stopPrice, Symbol.Digits);
            var actualRisk = Math.Abs(position.EntryPrice - exactStop);

            if (actualRisk <= 0
                || (tradeType == TradeType.Buy && position.EntryPrice <= exactStop)
                || (tradeType == TradeType.Sell && position.EntryPrice >= exactStop))
            {
                Print("ORB_PROTECTION_FAILED,position={0},reason=fill_beyond_stop", position.Id);
                ClosePosition(position);
                return;
            }

            var exactTarget = tradeType == TradeType.Buy
                ? position.EntryPrice + TargetR * actualRisk
                : position.EntryPrice - TargetR * actualRisk;
            exactTarget = Math.Round(exactTarget, Symbol.Digits);

            var stopResult = position.ModifyStopLossPrice(exactStop);
            if (!stopResult.IsSuccessful)
            {
                Print("ORB_PROTECTION_FAILED,position={0},leg=stop,error={1}", position.Id, stopResult.Error);
                ClosePosition(position);
                return;
            }

            var targetResult = position.ModifyTakeProfitPrice(exactTarget);
            if (!targetResult.IsSuccessful)
            {
                Print("ORB_PROTECTION_FAILED,position={0},leg=target,error={1}", position.Id, targetResult.Error);
                ClosePosition(position);
                return;
            }

            Print(
                "ORB_ENTRY,date={0:yyyy-MM-dd},direction={1},signal_close={2},entry={3},stop={4},target={5},target_r={6},volume={7},range_high={8},range_low={9}",
                _sessionDate,
                tradeType,
                confirmationBar.Close,
                position.EntryPrice,
                exactStop,
                exactTarget,
                TargetR,
                position.VolumeInUnits,
                _rangeHigh,
                _rangeLow);
        }

        private double ResolveVolume(double stopLossPips)
        {
            double requestedVolume;

            if (SizingMode == OrbPositionSizingMode.PercentEquityRisk)
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
                Print(
                    "ORB_CLOSE_REQUEST,position={0},reason={1},success={2},error={3}",
                    position.Id,
                    reason,
                    result.IsSuccessful,
                    result.Error);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != PositionLabel || position.SymbolName != SymbolName)
                return;

            Print(
                "ORB_CLOSED,position={0},date={1:yyyy-MM-dd},direction={2},entry={3},reason={4},net_profit={5},pips={6}",
                position.Id,
                position.EntryTime,
                position.TradeType,
                position.EntryPrice,
                args.Reason,
                position.NetProfit,
                position.Pips);
        }
    }
}
