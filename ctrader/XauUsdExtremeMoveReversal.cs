using System;
using cAlgo.API;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdExtremeMoveReversalV2 : Robot
    {
        private static readonly TimeSpan RangeStartTime = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan SignalTime = new TimeSpan(10, 0, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(10, 30, 0);
        private const int AtrLookback = 20;
        private const double ExtremeMultiple = 2.0;
        private const string BuildId = "EXP013_EXTREME_MOVE_REVERSAL_V2_20261103";
        public enum Sizing { EquityNotional, FixedUnits }
        [Parameter("Sizing", DefaultValue = Sizing.EquityNotional)] public Sizing SizingMode { get; set; }
        [Parameter("Equity Notional (x)", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0)] public double Notional { get; set; }
        [Parameter("Fixed Volume (Units)", DefaultValue = 5.0, MinValue = 1.0)] public double FixedUnits { get; set; }
        [Parameter("Position Label", DefaultValue = "XAU_EXP013_REV_V2")] public string PositionLabel { get; set; }
        [Parameter("Allow Live Trading", DefaultValue = false)] public bool AllowLiveTrading { get; set; }

        private DateTime _date = DateTime.MinValue.Date;
        private bool _signalDone, _exitDone;
        private double _range, _threshold;

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute) { Print("EXP013 requires M1."); Stop(); return; }
            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading) { Print("Live trading disabled."); Stop(); return; }
            Positions.Closed += OnClosed;
            Print("EXP013_START,build={0},symbol={1},timeframe={2},timezone={3},signal=10:00,exit=10:30,atr_lookback={4},extreme_multiple={5},sizing={6},notional={7}", BuildId, SymbolName, TimeFrame, TimeZone.DisplayName, AtrLookback, ExtremeMultiple, SizingMode, Notional);
        }

        protected override void OnBar()
        {
            if (Bars.Count < 52) return;
            var current = Bars.LastBar;
            if (current.OpenTime.Date != _date)
            {
                ClosePositions("new_session_cleanup");
                _date = current.OpenTime.Date; _signalDone = false; _exitDone = false; _range = 0; _threshold = 0;
            }
            var t = current.OpenTime.TimeOfDay;
            if (!_signalDone && t >= SignalTime) ProcessSignal(t);
            if (!_exitDone && t >= ExitTime) ProcessExit(t);
        }

        private void ProcessSignal(TimeSpan time)
        {
            _signalDone = true;
            var signalBars = CountExactSignalWindowBars();
            if (time != SignalTime || signalBars != 30) { Print("EXP013_SKIP,date={0:yyyy-MM-dd},reason=missing_exact_30m_window,bars={1},time={2}", _date, signalBars, time); return; }
            double atr;
            if (!TryCalculatePreWindowAtr(out atr)) { Print("EXP013_SKIP,date={0:yyyy-MM-dd},reason=missing_exact_atr_window,time={1}", _date, time); return; }
            var rangeOpenBar = Bars.Last(30);
            var rangeCloseBar = Bars.Last(1);
            _range = rangeCloseBar.Close - rangeOpenBar.Open;
            _threshold = ExtremeMultiple * atr;
            Print("EXP013_SIGNAL,date={0:yyyy-MM-dd},build={1},range_open={2},range_close={3},range={4},atr20={5},threshold={6}", _date, BuildId, rangeOpenBar.Open, rangeCloseBar.Close, _range, atr, _threshold);
            if (Math.Abs(_range) < _threshold) return;
            var direction = _range > 0 ? TradeType.Sell : TradeType.Buy;
            var entry = direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var units = SizingMode == Sizing.EquityNotional ? Account.Equity * Notional / entry : FixedUnits;
            units = Symbol.NormalizeVolumeInUnits(Math.Min(units, Symbol.VolumeInUnitsMax), RoundingMode.Down);
            if (units < Symbol.VolumeInUnitsMin) { Print("EXP013_REJECT,reason=volume_below_minimum,units={0}", units); return; }
            var result = ExecuteMarketOrder(direction, SymbolName, units, PositionLabel, (double?)null, (double?)null, "EXP013 2x ATR 30m reversal");
            Print("EXP013_ORDER,date={0:yyyy-MM-dd},direction={1},success={2},error={3},range={4},threshold={5},units={6}", _date, direction, result.IsSuccessful, result.Error, _range, _threshold, units);
        }

        private int CountExactSignalWindowBars()
        {
            var matched = 0;
            var rangeStart = _date.Add(RangeStartTime);
            for (var i = 1; i <= 30; i++)
            {
                var expected = rangeStart.AddMinutes(30 - i);
                if (IsBarAt(Bars.Last(i), expected)) matched++;
            }
            return matched;
        }

        private bool TryCalculatePreWindowAtr(out double atr)
        {
            atr = 0;
            var rangeStart = _date.Add(RangeStartTime);
            for (var i = 1; i <= AtrLookback; i++)
            {
                var index = 30 + i;
                var bar = Bars.Last(index);
                var previous = Bars.Last(index + 1);
                var expected = rangeStart.AddMinutes(-i);
                if (!IsBarAt(bar, expected) || !IsBarAt(previous, expected.AddMinutes(-1))) return false;
                atr += Math.Max(bar.High - bar.Low, Math.Max(Math.Abs(bar.High - previous.Close), Math.Abs(bar.Low - previous.Close)));
            }
            atr /= AtrLookback;
            return atr > 0;
        }

        private static bool IsBarAt(Bar bar, DateTime expected)
        {
            return bar.OpenTime.Date == expected.Date && bar.OpenTime.TimeOfDay == expected.TimeOfDay;
        }

        private void ProcessExit(TimeSpan time)
        {
            _exitDone = true; ClosePositions(time == ExitTime ? "10:30_exit" : "delayed_exit");
            Print("EXP013_AUDIT,date={0:yyyy-MM-dd},exit={1},exact={2},range={3},threshold={4}", _date, time, time == ExitTime, _range, _threshold);
        }
        private void ClosePositions(string reason) { foreach (var p in Positions.FindAll(PositionLabel, SymbolName)) ClosePosition(p); }
        private void OnClosed(PositionClosedEventArgs e) { if (e.Position.Label == PositionLabel && e.Position.SymbolName == SymbolName) Print("EXP013_CLOSED,id={0},net={1},direction={2},reason={3}", e.Position.Id, e.Position.NetProfit, e.Position.TradeType, e.Reason); }
        protected override void OnStop() { Positions.Closed -= OnClosed; }
    }
}
