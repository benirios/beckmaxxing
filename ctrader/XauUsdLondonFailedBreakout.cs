using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-002: London opening-range failed-breakout reversal.
    /// Attach to an M1 XAUUSD chart. GMT Standard Time follows London daylight saving.
    /// Research only; real-time trading is disabled unless explicitly enabled.
    /// </summary>
    [Robot(TimeZone = TimeZones.GMTStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdLondonFailedBreakout : Robot
    {
        private static readonly TimeSpan OpeningTime = new TimeSpan(8, 0, 0);
        private static readonly TimeSpan RangeEndTime = new TimeSpan(8, 30, 0);
        private static readonly TimeSpan SignalDeadline = new TimeSpan(11, 0, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(12, 0, 0);
        private const int RequiredOpeningBars = 30;
        private const string BuildId = "EXP002_LFB_PROTECTION_AUDIT_V2_20261030";

        public enum PositionSizingMode
        {
            PercentEquityRisk,
            FixedVolumeInUnits
        }

        [Parameter("Risk % of Equity", Group = "Risk", DefaultValue = 0.5, MinValue = 0.01, MaxValue = 5.0, Step = 0.01)]
        public double RiskPercent { get; set; }

        [Parameter("Sizing Mode", Group = "Risk", DefaultValue = PositionSizingMode.PercentEquityRisk)]
        public PositionSizingMode SizingMode { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Risk", DefaultValue = 1000.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_LDN_FAIL_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false,
            Description = "Keep false for research. Real-time instances stop immediately unless explicitly enabled.")]
        public bool AllowLiveTrading { get; set; }

        private DateTime _sessionDate;
        private double _rangeHigh;
        private double _rangeLow;
        private double _excursionExtreme;
        private int _openingBarCount;
        private bool _rangeFinalized;
        private bool _rangeValid;
        private bool _breakoutSeen;
        private bool _breakoutAbove;
        private bool _attemptConsumed;
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
            Print("LFB_START,build={0},symbol={1},timeframe={2},timezone={3},mode={4},label={5},digits={6},pip_size={7},tick_size={8},min_distance_type={9},min_sl_distance={10},min_tp_distance={11},bid={12},ask={13},spread={14}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode, PositionLabel,
                Symbol.Digits, Symbol.PipSize, Symbol.TickSize, Symbol.MinDistanceType,
                Symbol.MinStopLossDistance, Symbol.MinTakeProfitDistance,
                Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid);
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
                    CloseManagedPositions("12:00_London_time_exit");
                    _timeExitProcessed = true;
                }
                return;
            }

            ProcessOpeningRangeBar(closedBar);
            FinalizeOpeningRangeIfDue(currentTime);
            if (!_rangeValid || _attemptConsumed || currentTime < RangeEndTime)
                return;

            var closedTime = closedBar.OpenTime.TimeOfDay;
            if (closedBar.OpenTime.Date != _sessionDate || closedTime < RangeEndTime)
                return;

            if (!_breakoutSeen)
            {
                if (closedTime >= SignalDeadline)
                    return;

                if (closedBar.Close > _rangeHigh)
                {
                    _breakoutSeen = true;
                    _breakoutAbove = true;
                    _excursionExtreme = closedBar.High;
                    Print("LFB_BREAKOUT,date={0:yyyy-MM-dd},side=above,close={1},extreme={2},range_high={3},range_low={4}",
                        _sessionDate, closedBar.Close, _excursionExtreme, _rangeHigh, _rangeLow);
                }
                else if (closedBar.Close < _rangeLow)
                {
                    _breakoutSeen = true;
                    _breakoutAbove = false;
                    _excursionExtreme = closedBar.Low;
                    Print("LFB_BREAKOUT,date={0:yyyy-MM-dd},side=below,close={1},extreme={2},range_high={3},range_low={4}",
                        _sessionDate, closedBar.Close, _excursionExtreme, _rangeHigh, _rangeLow);
                }
                return;
            }

            _excursionExtreme = _breakoutAbove
                ? Math.Max(_excursionExtreme, closedBar.High)
                : Math.Min(_excursionExtreme, closedBar.Low);

            var closedBackInside = closedBar.Close < _rangeHigh && closedBar.Close > _rangeLow;
            if (!closedBackInside)
                return;

            _attemptConsumed = true;
            if (currentTime > SignalDeadline)
            {
                Print("LFB_FAILURE_IGNORED,date={0:yyyy-MM-dd},reason=entry_deadline,return_close={1}",
                    _sessionDate, closedBar.Close);
                return;
            }

            var tradeType = _breakoutAbove ? TradeType.Sell : TradeType.Buy;
            EnterReversal(tradeType, closedBar);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnManagedPositionClosed;
        }

        protected override void OnException(Exception exception)
        {
            Print("LFB_EXCEPTION,type={0},message={1}", exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _rangeHigh = double.MinValue;
            _rangeLow = double.MaxValue;
            _excursionExtreme = double.NaN;
            _openingBarCount = 0;
            _rangeFinalized = false;
            _rangeValid = false;
            _breakoutSeen = false;
            _breakoutAbove = false;
            _attemptConsumed = false;
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
                Print("LFB_RANGE_INVALID,date={0:yyyy-MM-dd},bars={1},required={2}",
                    _sessionDate, _openingBarCount, RequiredOpeningBars);
                return;
            }

            Print("LFB_RANGE,date={0:yyyy-MM-dd},high={1},low={2},bars={3}",
                _sessionDate, _rangeHigh, _rangeLow, _openingBarCount);
        }

        private void EnterReversal(TradeType tradeType, Bar failureBar)
        {
            var entryBid = Symbol.Bid;
            var entryAsk = Symbol.Ask;
            var entrySpread = entryAsk - entryBid;
            var expectedEntry = tradeType == TradeType.Buy ? entryAsk : entryBid;
            var stopPrice = _excursionExtreme;
            var targetPrice = tradeType == TradeType.Buy ? _rangeHigh : _rangeLow;
            var riskPrice = Math.Abs(expectedEntry - stopPrice);

            if (riskPrice <= 0
                || (tradeType == TradeType.Buy && (stopPrice >= entryBid || targetPrice <= entryAsk))
                || (tradeType == TradeType.Sell && (stopPrice <= entryAsk || targetPrice >= entryBid)))
            {
                Print("LFB_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=invalid_executable_protection_geometry,direction={1},bid={2},ask={3},spread={4},entry={5},stop={6},target={7},range_high={8},range_low={9},excursion={10}",
                    _sessionDate, tradeType, entryBid, entryAsk, entrySpread, expectedEntry,
                    stopPrice, targetPrice, _rangeHigh, _rangeLow, _excursionExtreme);
                return;
            }

            var stopPips = riskPrice / Symbol.PipSize;
            var targetPips = Math.Abs(targetPrice - expectedEntry) / Symbol.PipSize;
            var volume = ResolveVolume(stopPips);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("LFB_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=volume_below_minimum,calculated={1},minimum={2}",
                    _sessionDate, volume, Symbol.VolumeInUnitsMin);
                return;
            }

            var comment = string.Format("London failed breakout; return={0:O}; breakout={1}",
                failureBar.OpenTime, _breakoutAbove ? "above" : "below");
            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, PositionLabel,
                stopPips, targetPips, comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print("LFB_ORDER_FAILED,date={0:yyyy-MM-dd},direction={1},error={2}",
                    _sessionDate, tradeType, result.Error);
                return;
            }

            var position = result.Position;
            var actualEntry = position.EntryPrice;
            var actualStop = position.StopLoss;
            var actualTarget = position.TakeProfit;
            var protectionPresent = actualStop.HasValue && actualTarget.HasValue;
            var protectionDirectionValid = protectionPresent
                && (tradeType == TradeType.Buy
                    ? actualStop.Value < actualEntry && actualTarget.Value > actualEntry
                    : actualStop.Value > actualEntry && actualTarget.Value < actualEntry);

            Print("LFB_ENTRY,date={0:yyyy-MM-dd},build={1},direction={2},return_close={3},pre_bid={4},pre_ask={5},pre_spread={6},entry={7},intended_stop={8},intended_target={9},actual_stop={10},actual_target={11},requested_stop_pips={12},requested_target_pips={13},actual_risk={14},volume={15},range_high={16},range_low={17},excursion={18}",
                _sessionDate, BuildId, tradeType, failureBar.Close, entryBid, entryAsk, entrySpread,
                actualEntry, stopPrice, targetPrice,
                actualStop.HasValue ? actualStop.Value.ToString() : "null",
                actualTarget.HasValue ? actualTarget.Value.ToString() : "null",
                stopPips, targetPips,
                actualStop.HasValue ? Math.Abs(actualEntry - actualStop.Value).ToString() : "null",
                position.VolumeInUnits, _rangeHigh, _rangeLow, _excursionExtreme);

            if (protectionDirectionValid)
                return;

            Print("LFB_PROTECTION_FAILED,date={0:yyyy-MM-dd},build={1},position={2},direction={3},entry={4},actual_stop={5},actual_target={6},reason=missing_or_wrong_side_protection",
                _sessionDate, BuildId, position.Id, tradeType, actualEntry,
                actualStop.HasValue ? actualStop.Value.ToString() : "null",
                actualTarget.HasValue ? actualTarget.Value.ToString() : "null");

            var closeResult = ClosePosition(position);
            Print("LFB_PROTECTION_CLOSE,position={0},success={1},error={2}",
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
                Print("LFB_CLOSE_REQUEST,position={0},reason={1},success={2},error={3}",
                    position.Id, reason, result.IsSuccessful, result.Error);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != PositionLabel || position.SymbolName != SymbolName)
                return;

            Print("LFB_CLOSED,position={0},date={1:yyyy-MM-dd},direction={2},entry={3},reason={4},net_profit={5},pips={6}",
                position.Id, position.EntryTime, position.TradeType, position.EntryPrice,
                args.Reason, position.NetProfit, position.Pips);
        }
    }
}
