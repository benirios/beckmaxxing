using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// EXP-011: XAUUSD transfer test of the COMEX GC negative daytime-return
    /// anomaly. Attach to an M1 chart. Times use US Eastern time and follow DST.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdGcDaytimeShortV1 : Robot
    {
        private static readonly TimeSpan EntryTime = new TimeSpan(8, 20, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(13, 30, 0);
        private const int ExpectedHoldingBars = 310;
        private const string BuildId = "EXP011_GC_DAYTIME_SHORT_V1_20261102";

        public enum DaytimeDirectionMode
        {
            ShortDay,
            LongControl
        }

        public enum PositionSizingMode
        {
            EquityNotionalLeverage,
            FixedVolumeInUnits
        }

        [Parameter("Direction Mode", Group = "Experiment", DefaultValue = DaytimeDirectionMode.ShortDay)]
        public DaytimeDirectionMode DirectionMode { get; set; }

        [Parameter("Sizing Mode", Group = "Exposure", DefaultValue = PositionSizingMode.EquityNotionalLeverage)]
        public PositionSizingMode SizingMode { get; set; }

        [Parameter("Equity Notional (x)", Group = "Exposure", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0, Step = 0.1)]
        public double EquityNotionalLeverage { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Exposure", DefaultValue = 5.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_GC_DAY_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false,
            Description = "Keep false for research. Real-time instances stop immediately unless explicitly enabled.")]
        public bool AllowLiveTrading { get; set; }

        private DateTime _sessionDate;
        private bool _entryProcessed;
        private bool _exitProcessed;
        private int _holdingBarCount;
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
            ResetSession(DateTime.MinValue.Date);

            Print("GCDAY_START,build={0},symbol={1},timeframe={2},timezone={3},running_mode={4},direction_mode={5},entry_time={6},exit_time={7},expected_holding_bars={8},sizing_mode={9},equity_notional_x={10},fixed_volume={11},base_label={12},managed_label={13},digits={14},pip_size={15},tick_size={16}",
                BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, RunningMode,
                DirectionMode, EntryTime, ExitTime, ExpectedHoldingBars,
                SizingMode, EquityNotionalLeverage, FixedVolumeInUnits,
                PositionLabel, _managedLabel, Symbol.Digits, Symbol.PipSize,
                Symbol.TickSize);
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

            CountHoldingBar(closedBar);
            var currentTime = currentBar.OpenTime.TimeOfDay;

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
            Print("GCDAY_EXCEPTION,type={0},message={1}",
                exception.GetType().Name, exception.Message);
        }

        private void ResetSession(DateTime sessionDate)
        {
            _sessionDate = sessionDate;
            _entryProcessed = false;
            _exitProcessed = false;
            _holdingBarCount = 0;
        }

        private void CountHoldingBar(Bar closedBar)
        {
            if (closedBar.OpenTime.Date != _sessionDate)
                return;

            var barTime = closedBar.OpenTime.TimeOfDay;
            if (barTime >= EntryTime && barTime < ExitTime)
                _holdingBarCount++;
        }

        private void ProcessEntry(TimeSpan currentTime)
        {
            _entryProcessed = true;
            if (currentTime != EntryTime)
            {
                Print("GCDAY_ENTRY_SKIPPED,date={0:yyyy-MM-dd},mode={1},reason=exact_08:20_entry_unavailable,current_time={2}",
                    _sessionDate, DirectionMode, currentTime);
                return;
            }

            var tradeType = DirectionMode == DaytimeDirectionMode.ShortDay
                ? TradeType.Sell
                : TradeType.Buy;
            var expectedEntry = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var volume = ResolveVolume(expectedEntry);

            Print("GCDAY_SIGNAL,date={0:yyyy-MM-dd},build={1},mode={2},direction={3},pre_bid={4},pre_ask={5},pre_spread={6},equity={7}",
                _sessionDate, BuildId, DirectionMode, tradeType, Symbol.Bid,
                Symbol.Ask, Symbol.Ask - Symbol.Bid, Account.Equity);

            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("GCDAY_ENTRY_REJECTED,date={0:yyyy-MM-dd},mode={1},reason=volume_below_minimum,calculated={2},minimum={3},equity={4},expected_entry={5}",
                    _sessionDate, DirectionMode, volume, Symbol.VolumeInUnitsMin,
                    Account.Equity, expectedEntry);
                return;
            }

            var comment = string.Format(
                "EXP011 GC daytime anomaly; mode={0}; entry=08:20; exit=13:30",
                DirectionMode);
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
                Print("GCDAY_ORDER_FAILED,date={0:yyyy-MM-dd},mode={1},direction={2},error={3}",
                    _sessionDate, DirectionMode, tradeType, result.Error);
                return;
            }

            var position = result.Position;
            Print("GCDAY_ENTRY,date={0:yyyy-MM-dd},build={1},mode={2},position={3},direction={4},entry={5},volume={6},notional={7},stop=null,target=null",
                _sessionDate, BuildId, DirectionMode, position.Id, tradeType,
                position.EntryPrice, position.VolumeInUnits,
                position.EntryPrice * position.VolumeInUnits);
        }

        private void ProcessExit(TimeSpan currentTime)
        {
            _exitProcessed = true;
            var reason = currentTime == ExitTime
                ? "13:30_time_exit"
                : "delayed_time_exit";
            CloseManagedPositions(reason);
            Print("GCDAY_SESSION_AUDIT,date={0:yyyy-MM-dd},mode={1},exit_time={2},exact_exit={3},holding_bars={4},expected_holding_bars={5},complete_path={6}",
                _sessionDate, DirectionMode, currentTime, currentTime == ExitTime,
                _holdingBarCount, ExpectedHoldingBars,
                _holdingBarCount == ExpectedHoldingBars);
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

        private void CloseManagedPositions(string reason)
        {
            foreach (var position in Positions.FindAll(_managedLabel, SymbolName))
            {
                var result = ClosePosition(position);
                Print("GCDAY_CLOSE_REQUEST,position={0},mode={1},reason={2},success={3},error={4},holding_bars={5}",
                    position.Id, DirectionMode, reason, result.IsSuccessful,
                    result.Error, _holdingBarCount);
            }
        }

        private void OnManagedPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            if (position.Label != _managedLabel || position.SymbolName != SymbolName)
                return;

            Print("GCDAY_CLOSED,position={0},date={1:yyyy-MM-dd},mode={2},direction={3},entry={4},reason={5},net_profit={6},pips={7}",
                position.Id, position.EntryTime, DirectionMode, position.TradeType,
                position.EntryPrice, args.Reason, position.NetProfit, position.Pips);
        }
    }
}
