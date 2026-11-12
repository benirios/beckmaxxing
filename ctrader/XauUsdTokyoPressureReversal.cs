using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// Candidate D: fade unusually large XAUUSD moves in the first 30 minutes
    /// of the Tokyo session. Research cBot; attach to M1 and keep live disabled.
    /// </summary>
    [Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdTokyoPressureReversal : Robot
    {
        private const int OpeningBars = 30;
        private const int ReturnLookback = 60;
        private const int StopLookback = 20;
        private const double SignalPercentile = 0.90;
        private const string BuildId = "EXP017_TOKYO_PRESSURE_REVERSAL_V2";
        private static readonly TimeSpan SessionOpen = new TimeSpan(9, 0, 0);
        private static readonly TimeSpan EntryTime = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(10, 0, 0);

        public enum TestMode { Fade, Continuation, UnconditionalFade, RandomDirection }

        [Parameter("Test Mode", Group = "Experiment", DefaultValue = TestMode.Fade)]
        public TestMode Mode { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_EXP017_TOKYO_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Exposure", DefaultValue = 1.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false)]
        public bool AllowLiveTrading { get; set; }

        private readonly Queue<double> _priorAbsReturns = new Queue<double>();
        private readonly Queue<double> _priorRanges = new Queue<double>();
        private DateTime _date = DateTime.MinValue;
        private double _high = double.MinValue;
        private double _low = double.MaxValue;
        private int _openingCount;
        private bool _openingComplete;
        private bool _eligible;
        private bool _entryProcessed;
        private bool _exitProcessed;
        private double _openMid;
        private double _closeMid;
        private double _range;
        private double _threshold;
        private double _stopDistance;
        private string _label;

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute)
            {
                Print("EXP017_ERROR,reason=requires_m1,timeframe={0}", TimeFrame);
                Stop();
                return;
            }
            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading)
            {
                Print("EXP017_ERROR,reason=live_trading_disabled");
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Mode;
            Positions.Closed += OnPositionClosed;
            Print("EXP017_START,build={0},mode={1},symbol={2},timezone={3},session=09:00-10:00,threshold=prior60_p90,stop=prior20_median_range,volume={4}",
                BuildId, Mode, SymbolName, TimeZone.DisplayName, FixedVolumeInUnits);
        }

        protected override void OnBar()
        {
            if (Bars.Count < 2) return;
            var current = Bars.LastBar;
            var t = current.OpenTime.TimeOfDay;
            RefreshOpeningBars();
            if (t >= ExitTime && !_exitProcessed)
            {
                CloseManaged("10:00_time_exit");
                _exitProcessed = true;
            }
        }

        protected override void OnTick()
        {
            if (Bars.Count == 0) return;
            var t = Bars.LastBar.OpenTime.TimeOfDay;
            RefreshOpeningBars();
            if (t >= SessionOpen && t < EntryTime && _openMid == 0)
                _openMid = (Symbol.Bid + Symbol.Ask) / 2;
            if (t >= EntryTime && t < ExitTime && !_openingComplete)
                CompleteOpeningWindow();
            if (!_eligible || _entryProcessed || t < EntryTime || t >= ExitTime) return;
            _entryProcessed = true;

            var absReturn = Math.Abs(_closeMid / _openMid - 1.0);
            var signal = Mode == TestMode.UnconditionalFade || absReturn >= _threshold;
            if (!signal)
            {
                Print("EXP017_NO_SIGNAL,date={0:yyyy-MM-dd},return={1},threshold={2}", _date, _closeMid / _openMid - 1.0, _threshold);
                return;
            }

            var moveSign = Math.Sign(_closeMid - _openMid);
            var direction = Mode == TestMode.Continuation ? moveSign : -moveSign;
            if (Mode == TestMode.RandomDirection) direction = StableSign(_date);
            if (direction == 0) return;

            var tradeType = direction > 0 ? TradeType.Buy : TradeType.Sell;
            var entry = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var stop = tradeType == TradeType.Buy ? entry - _stopDistance : entry + _stopDistance;
            var geometryValid = tradeType == TradeType.Buy ? stop < Symbol.Bid : stop > Symbol.Ask;
            if (!geometryValid)
            {
                Print("EXP017_REJECT,date={0:yyyy-MM-dd},reason=invalid_stop_geometry,entry={1},stop={2}", _date, entry, stop);
                return;
            }
            var stopPips = Math.Abs(entry - stop) / Symbol.PipSize;
            var volume = Symbol.NormalizeVolumeInUnits(FixedVolumeInUnits, RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("EXP017_REJECT,date={0:yyyy-MM-dd},reason=volume_below_minimum,volume={1},minimum={2}", _date, volume, Symbol.VolumeInUnitsMin);
                return;
            }

            Print("EXP017_SIGNAL,date={0:yyyy-MM-dd},mode={1},direction={2},return={3},threshold={4},stop_distance={5},bid={6},ask={7}",
                _date, Mode, tradeType, _closeMid / _openMid - 1.0, _threshold, _stopDistance, Symbol.Bid, Symbol.Ask);
            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, _label, stopPips, null,
                string.Format("EXP017 Tokyo pressure; mode={0}; stop={1}", Mode, stop));
            if (!result.IsSuccessful || result.Position == null)
                Print("EXP017_ORDER_FAILED,date={0:yyyy-MM-dd},error={1}", _date, result.Error);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }

        private void Reset(DateTime date)
        {
            _date = date;
            _high = double.MinValue; _low = double.MaxValue;
            _openingCount = 0; _openingComplete = false; _eligible = false;
            _entryProcessed = false; _exitProcessed = false;
            _openMid = 0; _closeMid = 0; _range = 0;
            _threshold = double.NaN; _stopDistance = double.NaN;
        }

        // Both callbacks may see a new bar first. Read completed bars directly,
        // rather than relying on OnBar having counted 09:29 before OnTick.
        private DateTime _lastWindowRefresh = DateTime.MinValue;
        private bool _openingContiguous;

        private void RefreshOpeningBars()
        {
            var currentTime = Bars.LastBar.OpenTime;
            if (currentTime == _lastWindowRefresh) return;
            _lastWindowRefresh = currentTime;
            if (_date != currentTime.Date)
            {
                if (_date != DateTime.MinValue && !_openingComplete)
                    Print("EXP017_SESSION_SKIP,date={0:yyyy-MM-dd},reason=incomplete_opening_window,bars={1}", _date, _openingCount);
                Reset(currentTime.Date);
            }
            if (_openingComplete) return;
            _high = double.MinValue;
            _low = double.MaxValue;
            _openingCount = 0;
            _openingContiguous = true;
            for (var i = Math.Max(0, Bars.Count - OpeningBars - 1); i < Bars.Count - 1; i++)
            {
                var bar = Bars[i];
                if (bar.OpenTime.Date != _date || bar.OpenTime.TimeOfDay < SessionOpen
                    || bar.OpenTime.TimeOfDay >= EntryTime) continue;
                if (bar.OpenTime != _date.Add(SessionOpen).AddMinutes(_openingCount))
                    _openingContiguous = false;
                _high = Math.Max(_high, bar.High);
                _low = Math.Min(_low, bar.Low);
                _openingCount++;
            }
        }

        private void CompleteOpeningWindow()
        {
            _openingComplete = true;
            var valid = _date.DayOfWeek != DayOfWeek.Saturday && _date.DayOfWeek != DayOfWeek.Sunday
                && _openingCount == OpeningBars && _openingContiguous && _high > _low && _openMid > 0;
            if (!valid)
            {
                Print("EXP017_SESSION_SKIP,date={0:yyyy-MM-dd},reason=incomplete_opening_window,bars={1}", _date, _openingCount);
                return;
            }
            _closeMid = (Symbol.Bid + Symbol.Ask) / 2;
            _range = _high - _low;
            if (_priorAbsReturns.Count < ReturnLookback || _priorRanges.Count < StopLookback)
            {
                Print("EXP017_WARMUP,date={0:yyyy-MM-dd},return_history={1},range_history={2}", _date, _priorAbsReturns.Count, _priorRanges.Count);
                AddHistory();
                return;
            }
            _threshold = NearestRank(_priorAbsReturns.ToList(), SignalPercentile);
            _stopDistance = Median(_priorRanges);
            _eligible = true;
            Print("EXP017_WINDOW,date={0:yyyy-MM-dd},bars={1},open_mid={2},close_mid={3},abs_return={4},prior60_p90={5},range={6},prior20_median_range={7}",
                _date, _openingCount, _openMid, _closeMid, Math.Abs(_closeMid / _openMid - 1.0), _threshold, _range, _stopDistance);
            AddHistory();
        }

        private void AddHistory()
        {
            if (_openingCount != OpeningBars || _openMid <= 0 || _high <= _low) return;
            _priorAbsReturns.Enqueue(Math.Abs(_closeMid / _openMid - 1.0));
            _priorRanges.Enqueue(_range);
            while (_priorAbsReturns.Count > ReturnLookback) _priorAbsReturns.Dequeue();
            while (_priorRanges.Count > StopLookback) _priorRanges.Dequeue();
        }

        private void CloseManaged(string reason)
        {
            foreach (var p in Positions.FindAll(_label, SymbolName))
            {
                var result = ClosePosition(p);
                Print("EXP017_CLOSE,date={0:yyyy-MM-dd},position={1},reason={2},success={3},error={4}",
                    _date, p.Id, reason, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var p = args.Position;
            if (p.Label != _label || p.SymbolName != SymbolName) return;
            Print("EXP017_CLOSED,position={0},entry_time={1:O},direction={2},reason={3},net={4}",
                p.Id, p.EntryTime, p.TradeType, args.Reason, p.NetProfit);
        }

        private static double NearestRank(List<double> values, double percentile)
        {
            values.Sort();
            var index = Math.Max(0, (int)Math.Ceiling(percentile * values.Count) - 1);
            return values[index];
        }

        private static double Median(IEnumerable<double> source)
        {
            var values = source.OrderBy(x => x).ToArray();
            var mid = values.Length / 2;
            return values.Length % 2 == 0 ? (values[mid - 1] + values[mid]) / 2 : values[mid];
        }

        private static int StableSign(DateTime date)
        {
            var hash = date.Year * 10000 + date.Month * 100 + date.Day;
            return (hash & 1) == 0 ? 1 : -1;
        }
    }
}
