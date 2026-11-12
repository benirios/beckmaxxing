using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-009: matched 13:00-13:30 New York direction controls for EXP-008.
    /// Attach to an M1 chart. Select one Direction Mode and run each mode with
    /// identical dates, data, costs, and exposure.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdGcCloseControlsV1 : Robot
    {
        private static readonly TimeSpan SessionOpenTime = new TimeSpan(8, 20, 0);
        private static readonly TimeSpan SignalTime = new TimeSpan(13, 0, 0);
        private static readonly TimeSpan CloseTime = new TimeSpan(13, 30, 0);
        private static readonly TimeSpan LastSessionBarTime = new TimeSpan(13, 29, 0);
        private static readonly TimeSpan LastPredictorBarTime = new TimeSpan(12, 59, 0);
        private const int RequiredPredictorBars = 280;
        private const string BuildId = "EXP009_GC_CLOSE_CONTROLS_V1_20261102";

        public enum DirectionSelectionMode
        {
            Exp008Momentum,
            AlwaysLong,
            AlwaysShort,
            DeterministicRandom
        }

        public enum PositionSizingMode
        {
            EquityNotionalLeverage,
            FixedVolumeInUnits
        }

        [Parameter("Direction Mode", Group = "Experiment", DefaultValue = DirectionSelectionMode.AlwaysLong)]
        public DirectionSelectionMode DirectionMode { get; set; }

        [Parameter("Random Seed", Group = "Experiment", DefaultValue = 20261102,
            Description = "Fixed seed for the deterministic-random control. Do not optimize it.")]
        public int RandomSeed { get; set; }

        [Parameter("Sizing Mode", Group = "Exposure", DefaultValue = PositionSizingMode.EquityNotionalLeverage)]
        public PositionSizingMode SizingMode { get; set; }

        [Parameter("Equity Notional (x)", Group = "Exposure", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0, Step = 0.1)]
        public double EquityNotionalLeverage { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Exposure", DefaultValue = 5.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_GC_CTRL_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false,
            Description = "Keep false for research. Real-time instances stop immediately unless explicitly enabled.")]
        public bool AllowLiveTrading { get; set; }

        private DateTime _sessionDate;
        private int _predictorBarCount;
        private bool _signalProcessed;
        private bool _closeProcessed;
        private bool _sessionActivated;
        private double _previousSessionClose;
        private DateTime _previousSessionCloseDate;
        private string _managedLabel;

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

            _managedLabel = string.Format("{0}_{1}", PositionLabel, DirectionMode);
            Positions.Closed += OnManagedPositionClosed;
            _previousSessionClose = double.NaN;
            _previousSessionCloseDate = DateTime.MinValue.Date;
            ResetSession(DateTime.MinValue.Date);

            Print("GCCCTRL_START,build={0},symbol={1},timeframe={2},timezone={3},running_mode={4},direction_mode={5},random_seed={6},session_open={7},signal_time={8},close_time={9},required_predictor_bars={10},sizing_mode={11},equity_notional_x={12},fixed_volume={13},base_label={14},managed_label={15},digits={16},pip_size={17},tick_size={18}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode,
                DirectionMode, RandomSeed, SessionOpenTime, SignalTime, CloseTime,
                RequiredPredictorBars, SizingMode, EquityNotionalLeverage,
                FixedVolumeInUnits, PositionLabel, _managedLabel, Symbol.Digits,
                Symbol.PipSize, Symbol.TickSize);
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
                if (_sessionDate != DateTime.MinValue.Date
                    && _sessionActivated
                    && !_closeProcessed)
                    InvalidatePreviousClose("prior_session_missing_13:30_close");
                ResetSession(currentDate);
            }

            ProcessPredictorBar(closedBar);

            // Ignore Sunday-evening reopen dates and any other calendar dates
            // without activity in the defined GC predictor session.
            if (!_sessionActivated)
                return;

            var currentTime = currentBar.OpenTime.TimeOfDay;
            if (!_signalProcessed && currentTime >= SignalTime)
                ProcessSignal(currentTime, closedBar);

            if (!_closeProcessed && currentTime >= CloseTime)
            {
                CloseManagedPositions("13:30_time_exit");
                CaptureSessionClose(closedBar);
                _closeProcessed = true;
            }
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnManagedPositionClosed;
        }

        protected override void OnException(Exception exception)
        {
            Print("GCCCTRL_EXCEPTION,type={0},message={1}",
                exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _predictorBarCount = 0;
            _signalProcessed = false;
            _closeProcessed = false;
            _sessionActivated = false;
        }

        private void ProcessPredictorBar(Bar bar)
        {
            if (_signalProcessed || bar.OpenTime.Date != _sessionDate)
                return;

            var barTime = bar.OpenTime.TimeOfDay;
            if (barTime < SessionOpenTime || barTime >= SignalTime)
                return;

            _sessionActivated = true;
            _predictorBarCount++;
        }

        private void ProcessSignal(TimeSpan currentTime, Bar predictorCloseBar)
        {
            _signalProcessed = true;

            if (currentTime != SignalTime
                || predictorCloseBar.OpenTime.Date != _sessionDate
                || predictorCloseBar.OpenTime.TimeOfDay != LastPredictorBarTime)
            {
                Print("GCCCTRL_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},mode={1},reason=exact_13:00_entry_unavailable,current_time={2},closed_bar={3:O}",
                    _sessionDate, DirectionMode, currentTime, predictorCloseBar.OpenTime);
                return;
            }

            if (_predictorBarCount != RequiredPredictorBars)
            {
                Print("GCCCTRL_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},mode={1},reason=incomplete_predictor_session,bars={2},required={3}",
                    _sessionDate, DirectionMode, _predictorBarCount, RequiredPredictorBars);
                return;
            }

            // Controls deliberately retain this requirement so every mode uses
            // the same eligible session set as the corrected EXP-008 strategy.
            if (double.IsNaN(_previousSessionClose)
                || _previousSessionClose <= 0
                || _previousSessionCloseDate >= _sessionDate)
            {
                Print("GCCCTRL_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},mode={1},reason=no_prior_valid_13:30_close,prior_date={2:yyyy-MM-dd},prior_close={3}",
                    _sessionDate, DirectionMode, _previousSessionCloseDate, _previousSessionClose);
                return;
            }

            var signalClose = predictorCloseBar.Close;
            var predictorReturn = signalClose / _previousSessionClose - 1.0;

            // EXP-008 skipped an exactly zero return. All control modes do the
            // same to keep the eligible dates strictly matched.
            if (predictorReturn == 0)
            {
                Print("GCCCTRL_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},mode={1},reason=zero_predictor_return,prior_date={2:yyyy-MM-dd},prior_close={3},signal_close={4}",
                    _sessionDate, DirectionMode, _previousSessionCloseDate,
                    _previousSessionClose, signalClose);
                return;
            }

            uint randomKey;
            var tradeType = SelectTradeType(_sessionDate, predictorReturn, out randomKey);
            var expectedEntry = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var volume = ResolveVolume(expectedEntry);

            Print("GCCCTRL_SIGNAL,date={0:yyyy-MM-dd},build={1},mode={2},random_seed={3},random_key={4},prior_close_date={5:yyyy-MM-dd},prior_close={6},signal_close={7},predictor_return={8},direction={9},pre_bid={10},pre_ask={11},pre_spread={12},bars={13}",
                _sessionDate, BuildId, DirectionMode, RandomSeed, randomKey,
                _previousSessionCloseDate, _previousSessionClose, signalClose,
                predictorReturn, tradeType, Symbol.Bid, Symbol.Ask,
                Symbol.Ask - Symbol.Bid, _predictorBarCount);

            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("GCCCTRL_SIGNAL_REJECTED,date={0:yyyy-MM-dd},mode={1},reason=volume_below_minimum,calculated={2},minimum={3},equity={4},expected_entry={5}",
                    _sessionDate, DirectionMode, volume, Symbol.VolumeInUnitsMin,
                    Account.Equity, expectedEntry);
                return;
            }

            var comment = string.Format(
                "EXP009 {0}; prior={1:yyyy-MM-dd}@{2}; signal={3}; return={4}; random_key={5}",
                DirectionMode, _previousSessionCloseDate, _previousSessionClose,
                signalClose, predictorReturn, randomKey);

            // These are unprotected timing positions by design. Their purpose
            // is comparison with EXP-008, not production risk management.
            var result = ExecuteMarketOrder(
                tradeType,
                SymbolName,
                volume,
                _managedLabel,
                (double?)null,
                (double?)null,
                comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print("GCCCTRL_ORDER_FAILED,date={0:yyyy-MM-dd},mode={1},direction={2},error={3}",
                    _sessionDate, DirectionMode, tradeType, result.Error);
                return;
            }

            var position = result.Position;
            Print("GCCCTRL_ENTRY,date={0:yyyy-MM-dd},build={1},mode={2},position={3},direction={4},predictor_return={5},random_key={6},entry={7},volume={8},notional={9},stop=null,target=null",
                _sessionDate, BuildId, DirectionMode, position.Id, tradeType,
                predictorReturn, randomKey, position.EntryPrice,
                position.VolumeInUnits, position.EntryPrice * position.VolumeInUnits);
        }

        private TradeType SelectTradeType(DateTime sessionDate, double predictorReturn, out uint randomKey)
        {
            randomKey = StableRandomKey(sessionDate, RandomSeed);

            switch (DirectionMode)
            {
                case DirectionSelectionMode.AlwaysLong:
                    return TradeType.Buy;
                case DirectionSelectionMode.AlwaysShort:
                    return TradeType.Sell;
                case DirectionSelectionMode.DeterministicRandom:
                    return (randomKey & 1u) == 0u ? TradeType.Buy : TradeType.Sell;
                case DirectionSelectionMode.Exp008Momentum:
                default:
                    return predictorReturn > 0 ? TradeType.Buy : TradeType.Sell;
            }
        }

        private static uint StableRandomKey(DateTime sessionDate, int seed)
        {
            unchecked
            {
                uint value = (uint)(sessionDate.Year * 10000
                    + sessionDate.Month * 100
                    + sessionDate.Day);
                value ^= (uint)seed;
                value ^= value >> 16;
                value *= 0x7feb352dU;
                value ^= value >> 15;
                value *= 0x846ca68bU;
                value ^= value >> 16;
                return value;
            }
        }

        private double ResolveVolume(double expectedEntry)
        {
            double requestedVolume;
            if (SizingMode == PositionSizingMode.EquityNotionalLeverage)
            {
                if (expectedEntry <= 0 || double.IsNaN(expectedEntry) || double.IsInfinity(expectedEntry))
                    return 0;

                requestedVolume = Account.Equity * EquityNotionalLeverage / expectedEntry;
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

        private void CaptureSessionClose(Bar closedBar)
        {
            if (closedBar.OpenTime.Date != _sessionDate
                || closedBar.OpenTime.TimeOfDay != LastSessionBarTime
                || closedBar.Close <= 0)
            {
                Print("GCCCTRL_CLOSE_INVALID,date={0:yyyy-MM-dd},reason=exact_13:30_close_unavailable,closed_bar={1:O},close={2}",
                    _sessionDate, closedBar.OpenTime, closedBar.Close);
                InvalidatePreviousClose("exact_13:30_close_unavailable");
                return;
            }

            _previousSessionClose = closedBar.Close;
            _previousSessionCloseDate = _sessionDate;
            Print("GCCCTRL_SESSION_CLOSE,date={0:yyyy-MM-dd},close={1},bar={2:O}",
                _sessionDate, _previousSessionClose, closedBar.OpenTime);
        }

        private void InvalidatePreviousClose(string reason)
        {
            _previousSessionClose = double.NaN;
            _previousSessionCloseDate = DateTime.MinValue.Date;
            Print("GCCCTRL_PREVIOUS_CLOSE_INVALIDATED,date={0:yyyy-MM-dd},reason={1}",
                _sessionDate, reason);
        }

        private void CloseManagedPositions(string reason)
        {
            foreach (var position in Positions.FindAll(_managedLabel, SymbolName))
            {
                var result = ClosePosition(position);
                Print("GCCCTRL_CLOSE_REQUEST,position={0},mode={1},reason={2},success={3},error={4}",
                    position.Id, DirectionMode, reason, result.IsSuccessful, result.Error);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != _managedLabel || position.SymbolName != SymbolName)
                return;

            Print("GCCCTRL_CLOSED,position={0},date={1:yyyy-MM-dd},mode={2},direction={3},entry={4},reason={5},net_profit={6},pips={7}",
                position.Id, position.EntryTime, DirectionMode, position.TradeType,
                position.EntryPrice, args.Reason, position.NetProfit, position.Pips);
        }
    }
}
