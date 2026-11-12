using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-012: XAUUSD transfer test of GLD fifth-half-hour momentum into the
    /// final NYSE half-hour. Attach to M1. Times use US Eastern time and DST.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdGldR5CloseMomentumV1 : Robot
    {
        private static readonly TimeSpan PredictorStartTime = new TimeSpan(11, 30, 0);
        private static readonly TimeSpan PredictorReadyTime = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan PredictorLastBarTime = new TimeSpan(11, 59, 0);
        private static readonly TimeSpan EntryTime = new TimeSpan(15, 30, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(16, 0, 0);
        private const int RequiredPredictorBars = 30;
        private const int ExpectedHoldingBars = 30;
        private const string ModeName = "R5Momentum";
        private const string BuildId = "EXP012_GLD_R5_CLOSE_MOMENTUM_V1_20261102";

        public enum PositionSizingMode
        {
            EquityNotionalLeverage,
            FixedVolumeInUnits
        }

        [Parameter("Sizing Mode", Group = "Exposure", DefaultValue = PositionSizingMode.EquityNotionalLeverage)]
        public PositionSizingMode SizingMode { get; set; }

        [Parameter("Equity Notional (x)", Group = "Exposure", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0, Step = 0.1)]
        public double EquityNotionalLeverage { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Exposure", DefaultValue = 5.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_GLD_R5_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false,
            Description = "Keep false for research. Real-time instances stop immediately unless explicitly enabled.")]
        public bool AllowLiveTrading { get; set; }

        private DateTime _sessionDate;
        private bool _predictorProcessed;
        private bool _entryProcessed;
        private bool _exitProcessed;
        private bool _signalReady;
        private bool _hasPredictorStart;
        private bool _hasPredictorEnd;
        private int _predictorBarCount;
        private int _holdingBarCount;
        private double _predictorOpen;
        private double _predictorClose;
        private double _predictorReturn;
        private TradeType _signalTradeType;

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

            Print("GLDR5_START,build={0},symbol={1},timeframe={2},timezone={3},running_mode={4},mode={5},predictor_start={6},predictor_ready={7},entry_time={8},exit_time={9},required_predictor_bars={10},expected_holding_bars={11},sizing_mode={12},equity_notional_x={13},fixed_volume={14},label={15},digits={16},pip_size={17},tick_size={18}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode,
                ModeName, PredictorStartTime, PredictorReadyTime, EntryTime,
                ExitTime, RequiredPredictorBars, ExpectedHoldingBars, SizingMode,
                EquityNotionalLeverage, FixedVolumeInUnits, PositionLabel,
                Symbol.Digits, Symbol.PipSize, Symbol.TickSize);
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

            CountPredictorBar(closedBar);
            CountHoldingBar(closedBar);
            var currentTime = currentBar.OpenTime.TimeOfDay;

            if (!_predictorProcessed && currentTime >= PredictorReadyTime)
                ProcessPredictor(currentTime);

            if (!_entryProcessed && currentTime >= EntryTime)
                ProcessEntry(currentTime);

            if (!_exitProcessed && currentTime >= ExitTime)
                ProcessExit(currentTime);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnManagedPositionClosed;
        }

        protected override void OnException(Exception exception)
        {
            Print("GLDR5_EXCEPTION,type={0},message={1}",
                exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _predictorProcessed = false;
            _entryProcessed = false;
            _exitProcessed = false;
            _signalReady = false;
            _hasPredictorStart = false;
            _hasPredictorEnd = false;
            _predictorBarCount = 0;
            _holdingBarCount = 0;
            _predictorOpen = double.NaN;
            _predictorClose = double.NaN;
            _predictorReturn = double.NaN;
        }

        private void CountPredictorBar(Bar closedBar)
        {
            if (closedBar.OpenTime.Date != _sessionDate)
                return;

            var barTime = closedBar.OpenTime.TimeOfDay;
            if (barTime < PredictorStartTime || barTime >= PredictorReadyTime)
                return;

            _predictorBarCount++;
            if (barTime == PredictorStartTime)
            {
                _predictorOpen = closedBar.Open;
                _hasPredictorStart = true;
            }

            if (barTime == PredictorLastBarTime)
            {
                _predictorClose = closedBar.Close;
                _hasPredictorEnd = true;
            }
        }

        private void CountHoldingBar(Bar closedBar)
        {
            if (closedBar.OpenTime.Date != _sessionDate)
                return;

            var barTime = closedBar.OpenTime.TimeOfDay;
            if (barTime >= EntryTime && barTime < ExitTime)
                _holdingBarCount++;
        }

        private void ProcessPredictor(TimeSpan currentTime)
        {
            _predictorProcessed = true;
            if (currentTime != PredictorReadyTime)
            {
                Print("GLDR5_PREDICTOR_SKIPPED,date={0:yyyy-MM-dd},reason=exact_12:00_evaluation_unavailable,current_time={1},bars={2}",
                    _sessionDate, currentTime, _predictorBarCount);
                return;
            }

            if (_predictorBarCount != RequiredPredictorBars
                || !_hasPredictorStart
                || !_hasPredictorEnd
                || double.IsNaN(_predictorOpen)
                || double.IsNaN(_predictorClose)
                || _predictorOpen <= 0)
            {
                Print("GLDR5_PREDICTOR_SKIPPED,date={0:yyyy-MM-dd},reason=incomplete_11:30_12:00_window,bars={1},required={2},has_start={3},has_end={4},open={5},close={6}",
                    _sessionDate, _predictorBarCount, RequiredPredictorBars,
                    _hasPredictorStart, _hasPredictorEnd, _predictorOpen,
                    _predictorClose);
                return;
            }

            _predictorReturn = _predictorClose / _predictorOpen - 1.0;
            if (_predictorReturn == 0.0)
            {
                Print("GLDR5_PREDICTOR_SKIPPED,date={0:yyyy-MM-dd},reason=zero_return,open={1},close={2},bars={3}",
                    _sessionDate, _predictorOpen, _predictorClose,
                    _predictorBarCount);
                return;
            }

            _signalTradeType = _predictorReturn > 0
                ? TradeType.Buy
                : TradeType.Sell;
            _signalReady = true;
            Print("GLDR5_PREDICTOR,date={0:yyyy-MM-dd},build={1},mode={2},open={3},close={4},return={5},bars={6},direction={7}",
                _sessionDate, BuildId, ModeName, _predictorOpen,
                _predictorClose, _predictorReturn, _predictorBarCount,
                _signalTradeType);
        }

        private void ProcessEntry(TimeSpan currentTime)
        {
            _entryProcessed = true;
            if (currentTime != EntryTime)
            {
                Print("GLDR5_ENTRY_SKIPPED,date={0:yyyy-MM-dd},reason=exact_15:30_entry_unavailable,current_time={1},signal_ready={2}",
                    _sessionDate, currentTime, _signalReady);
                return;
            }

            if (!_signalReady)
            {
                Print("GLDR5_ENTRY_SKIPPED,date={0:yyyy-MM-dd},reason=no_valid_predictor,current_time={1}",
                    _sessionDate, currentTime);
                return;
            }

            var expectedEntry = _signalTradeType == TradeType.Buy
                ? Symbol.Ask
                : Symbol.Bid;
            var volume = ResolveVolume(expectedEntry);

            Print("GLDR5_SIGNAL,date={0:yyyy-MM-dd},build={1},mode={2},predictor_return={3},direction={4},pre_bid={5},pre_ask={6},pre_spread={7},equity={8}",
                _sessionDate, BuildId, ModeName, _predictorReturn,
                _signalTradeType, Symbol.Bid, Symbol.Ask,
                Symbol.Ask - Symbol.Bid, Account.Equity);

            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("GLDR5_ENTRY_REJECTED,date={0:yyyy-MM-dd},reason=volume_below_minimum,calculated={1},minimum={2},equity={3},expected_entry={4}",
                    _sessionDate, volume, Symbol.VolumeInUnitsMin,
                    Account.Equity, expectedEntry);
                return;
            }

            const string comment =
                "EXP012 GLD r5 close momentum; predictor=11:30-12:00; hold=15:30-16:00";
            var result = ExecuteMarketOrder(
                _signalTradeType,
                SymbolName,
                volume,
                PositionLabel,
                (double?)null,
                (double?)null,
                comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print("GLDR5_ORDER_FAILED,date={0:yyyy-MM-dd},direction={1},error={2}",
                    _sessionDate, _signalTradeType, result.Error);
                return;
            }

            var position = result.Position;
            Print("GLDR5_ENTRY,date={0:yyyy-MM-dd},build={1},mode={2},position={3},predictor_return={4},direction={5},entry={6},volume={7},notional={8},stop=null,target=null",
                _sessionDate, BuildId, ModeName, position.Id,
                _predictorReturn, _signalTradeType, position.EntryPrice,
                position.VolumeInUnits,
                position.EntryPrice * position.VolumeInUnits);
        }

        private void ProcessExit(TimeSpan currentTime)
        {
            _exitProcessed = true;
            var reason = currentTime == ExitTime
                ? "16:00_time_exit"
                : "delayed_time_exit";
            CloseManagedPositions(reason);
            Print("GLDR5_SESSION_AUDIT,date={0:yyyy-MM-dd},mode={1},predictor_bars={2},signal_ready={3},exit_time={4},exact_exit={5},holding_bars={6},expected_holding_bars={7},complete_path={8}",
                _sessionDate, ModeName, _predictorBarCount, _signalReady,
                currentTime, currentTime == ExitTime, _holdingBarCount,
                ExpectedHoldingBars,
                _holdingBarCount == ExpectedHoldingBars);
        }

        private double ResolveVolume(double expectedEntry)
        {
            double requestedVolume;
            if (SizingMode == PositionSizingMode.EquityNotionalLeverage)
            {
                if (expectedEntry <= 0
                    || double.IsNaN(expectedEntry)
                    || double.IsInfinity(expectedEntry))
                    return 0;

                requestedVolume = Account.Equity
                    * EquityNotionalLeverage
                    / expectedEntry;
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
            return Symbol.NormalizeVolumeInUnits(
                requestedVolume,
                RoundingMode.Down);
        }

        private void CloseManagedPositions(string reason)
        {
            foreach (var position in Positions.FindAll(PositionLabel, SymbolName))
            {
                var result = ClosePosition(position);
                Print("GLDR5_CLOSE_REQUEST,position={0},reason={1},success={2},error={3},holding_bars={4}",
                    position.Id, reason, result.IsSuccessful, result.Error,
                    _holdingBarCount);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != PositionLabel
                || position.SymbolName != SymbolName)
                return;

            Print("GLDR5_CLOSED,position={0},date={1:yyyy-MM-dd},mode={2},direction={3},entry={4},reason={5},net_profit={6},pips={7}",
                position.Id, position.EntryTime, ModeName,
                position.TradeType, position.EntryPrice, args.Reason,
                position.NetProfit, position.Pips);
        }
    }
}
