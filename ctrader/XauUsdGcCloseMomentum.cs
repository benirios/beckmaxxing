using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-008: XAUUSD replication of COMEX gold last-half-hour momentum.
    /// Attach to an M1 chart. Times use US Eastern time and follow DST.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdGcCloseMomentumV2 : Robot
    {
        private static readonly TimeSpan SessionOpenTime = new TimeSpan(8, 20, 0);
        private static readonly TimeSpan SignalTime = new TimeSpan(13, 0, 0);
        private static readonly TimeSpan CloseTime = new TimeSpan(13, 30, 0);
        private static readonly TimeSpan LastSessionBarTime = new TimeSpan(13, 29, 0);
        private static readonly TimeSpan LastPredictorBarTime = new TimeSpan(12, 59, 0);
        private const int RequiredPredictorBars = 280;
        private const string BuildId = "EXP008_GC_CLOSE_MOMENTUM_1X_V2_20261101";

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

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_GC_CLOSE_MOM_V1")]
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
            _previousSessionClose = double.NaN;
            _previousSessionCloseDate = DateTime.MinValue.Date;
            ResetSession(DateTime.MinValue.Date);

            Print("GCCM_START,build={0},symbol={1},timeframe={2},timezone={3},mode={4},session_open={5},signal_time={6},close_time={7},required_predictor_bars={8},sizing_mode={9},equity_notional_x={10},fixed_volume={11},label={12},digits={13},pip_size={14},tick_size={15}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode,
                SessionOpenTime, SignalTime, CloseTime, RequiredPredictorBars,
                SizingMode, EquityNotionalLeverage, FixedVolumeInUnits,
                PositionLabel, Symbol.Digits, Symbol.PipSize, Symbol.TickSize);
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

            // Ignore calendar dates that only contain the Sunday evening reopen
            // or other bars outside the defined 08:20-13:30 GC session.
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
            Print("GCCM_EXCEPTION,type={0},message={1}",
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
                Print("GCCM_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},reason=exact_13:00_entry_unavailable,current_time={1},closed_bar={2:O}",
                    _sessionDate, currentTime, predictorCloseBar.OpenTime);
                return;
            }

            if (_predictorBarCount != RequiredPredictorBars)
            {
                Print("GCCM_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},reason=incomplete_predictor_session,bars={1},required={2}",
                    _sessionDate, _predictorBarCount, RequiredPredictorBars);
                return;
            }

            if (double.IsNaN(_previousSessionClose)
                || _previousSessionClose <= 0
                || _previousSessionCloseDate >= _sessionDate)
            {
                Print("GCCM_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},reason=no_prior_valid_13:30_close,prior_date={1:yyyy-MM-dd},prior_close={2}",
                    _sessionDate, _previousSessionCloseDate, _previousSessionClose);
                return;
            }

            var signalClose = predictorCloseBar.Close;
            var predictorReturn = signalClose / _previousSessionClose - 1.0;

            if (predictorReturn == 0)
            {
                Print("GCCM_SIGNAL_SKIPPED,date={0:yyyy-MM-dd},reason=zero_predictor_return,prior_date={1:yyyy-MM-dd},prior_close={2},signal_close={3}",
                    _sessionDate, _previousSessionCloseDate,
                    _previousSessionClose, signalClose);
                return;
            }

            var tradeType = predictorReturn > 0 ? TradeType.Buy : TradeType.Sell;
            var expectedEntry = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var volume = ResolveVolume(expectedEntry);

            Print("GCCM_SIGNAL,date={0:yyyy-MM-dd},build={1},prior_close_date={2:yyyy-MM-dd},prior_close={3},signal_close={4},predictor_return={5},direction={6},pre_bid={7},pre_ask={8},pre_spread={9},bars={10}",
                _sessionDate, BuildId, _previousSessionCloseDate,
                _previousSessionClose, signalClose, predictorReturn, tradeType,
                Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid,
                _predictorBarCount);

            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("GCCM_SIGNAL_REJECTED,date={0:yyyy-MM-dd},reason=volume_below_minimum,calculated={1},minimum={2},equity={3},expected_entry={4}",
                    _sessionDate, volume, Symbol.VolumeInUnitsMin,
                    Account.Equity, expectedEntry);
                return;
            }

            var comment = string.Format(
                "GC close momentum; prior={0:yyyy-MM-dd}@{1}; signal={2}; return={3}",
                _previousSessionCloseDate, _previousSessionClose,
                signalClose, predictorReturn);

            // The source paper's strategy is an unprotected 30-minute timing
            // position. Do not add a stop or target after observing results.
            var result = ExecuteMarketOrder(
                tradeType,
                SymbolName,
                volume,
                PositionLabel,
                (double?)null,
                (double?)null,
                comment);

            if (!result.IsSuccessful || result.Position == null)
            {
                Print("GCCM_ORDER_FAILED,date={0:yyyy-MM-dd},direction={1},error={2}",
                    _sessionDate, tradeType, result.Error);
                return;
            }

            var position = result.Position;
            Print("GCCM_ENTRY,date={0:yyyy-MM-dd},build={1},position={2},direction={3},predictor_return={4},entry={5},volume={6},notional={7},stop=null,target=null",
                _sessionDate, BuildId, position.Id, tradeType, predictorReturn,
                position.EntryPrice, position.VolumeInUnits,
                position.EntryPrice * position.VolumeInUnits);
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
                Print("GCCM_CLOSE_INVALID,date={0:yyyy-MM-dd},reason=exact_13:30_close_unavailable,closed_bar={1:O},close={2}",
                    _sessionDate, closedBar.OpenTime, closedBar.Close);
                InvalidatePreviousClose("exact_13:30_close_unavailable");
                return;
            }

            _previousSessionClose = closedBar.Close;
            _previousSessionCloseDate = _sessionDate;
            Print("GCCM_SESSION_CLOSE,date={0:yyyy-MM-dd},close={1},bar={2:O}",
                _sessionDate, _previousSessionClose, closedBar.OpenTime);
        }

        private void InvalidatePreviousClose(string reason)
        {
            _previousSessionClose = double.NaN;
            _previousSessionCloseDate = DateTime.MinValue.Date;
            Print("GCCM_PREVIOUS_CLOSE_INVALIDATED,date={0:yyyy-MM-dd},reason={1}",
                _sessionDate, reason);
        }

        private void CloseManagedPositions(string reason)
        {
            foreach (var position in Positions.FindAll(PositionLabel, SymbolName))
            {
                var result = ClosePosition(position);
                Print("GCCM_CLOSE_REQUEST,position={0},reason={1},success={2},error={3}",
                    position.Id, reason, result.IsSuccessful, result.Error);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != PositionLabel || position.SymbolName != SymbolName)
                return;

            Print("GCCM_CLOSED,position={0},date={1:yyyy-MM-dd},direction={2},entry={3},reason={4},net_profit={5},pips={6}",
                position.Id, position.EntryTime, position.TradeType,
                position.EntryPrice, args.Reason, position.NetProfit, position.Pips);
        }
    }
}
