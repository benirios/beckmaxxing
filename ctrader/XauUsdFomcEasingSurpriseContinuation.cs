using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T016: FOMC easing-surprise continuation. Backtest with broker tick data.
    /// Event dates must be classified beforehand from timestamped Fed funds futures,
    /// with actual 14:00 releases and overlapping releases checked externally.
    /// Empty event inputs stop the bot rather than infer surprises from gold prices.
    /// Run each control separately with the same verified event lists and costs.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdFomcEasingSurpriseContinuation : Robot
    {
        private const string BuildId = "T016_FOMC_EASING_V1_20261104";
        private const int StopLookback = 20;
        private static readonly TimeSpan EntryTime = new TimeSpan(14, 5, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(14, 10, 0);
        private static readonly TimeSpan MaxQuoteAge = TimeSpan.FromSeconds(2);

        public enum TestVariant { EasingLong, EasingShortControl, EasingRandomControl, TighteningShort }

        [Parameter("Test Variant", DefaultValue = TestVariant.EasingLong)]
        public TestVariant Variant { get; set; }

        [Parameter("Verified Easing Dates (YYYY-MM-DD, comma separated)", DefaultValue = "")]
        public string EasingDates { get; set; }

        [Parameter("Verified Tightening Dates (YYYY-MM-DD, comma separated)", DefaultValue = "")]
        public string TighteningDates { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T016_FOMC")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", DefaultValue = false)]
        public bool AllowLiveTrading { get; set; }

        private readonly HashSet<DateTime> _easing = new HashSet<DateTime>();
        private readonly HashSet<DateTime> _tightening = new HashSet<DateTime>();
        private readonly Queue<double> _priorFiveMinuteMoves = new Queue<double>();
        private TimeZoneInfo _newYork;
        private DateTime _date = DateTime.MinValue;
        private DateTime _startQuoteTime = DateTime.MinValue;
        private DateTime _endQuoteTime = DateTime.MinValue;
        private DateTime _lastWindowQuoteTime = DateTime.MinValue;
        private double _startMid;
        private double _endMid;
        private bool _eventDay;
        private bool _sampleDone;
        private bool _entryDone;
        private bool _exitDone;
        private bool _quoteGap;
        private string _label;

        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading)
            {
                Print("T016_STOP,reason=live_trading_disabled");
                Stop();
                return;
            }
            try
            {
                try { _newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
                catch (TimeZoneNotFoundException) { _newYork = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                ParseDates(EasingDates, _easing);
                ParseDates(TighteningDates, _tightening);
                if (_easing.Count == 0 && _tightening.Count == 0)
                    throw new FormatException("Verified policy-futures surprise dates are required.");
                if (Variant != TestVariant.TighteningShort && _easing.Count == 0)
                    throw new FormatException("Easing variants require verified easing dates.");
                if (Variant == TestVariant.TighteningShort && _tightening.Count == 0)
                    throw new FormatException("Tightening control requires verified tightening dates.");
                if (_easing.Overlaps(_tightening))
                    throw new FormatException("A date cannot be both easing and tightening.");
            }
            catch (Exception error)
            {
                Print("T016_STOP,reason=configuration,error={0}", error.Message);
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Variant;
            Positions.Closed += OnPositionClosed;
            Print("T016_START,build={0},variant={1},easing_dates={2},tightening_dates={3},timezone=America/New_York,entry=14:05:00+,exit=14:10:00,stop=prior20_non_event_median_5m,volume={4},tick_size={5}",
                BuildId, Variant, _easing.Count, _tightening.Count, FixedVolumeInUnits, Symbol.TickSize);
        }

        private static void ParseDates(string text, HashSet<DateTime> destination)
        {
            foreach (var token in (text ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                DateTime date;
                if (!DateTime.TryParseExact(token.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out date) || !destination.Add(date.Date))
                    throw new FormatException("Invalid or duplicate date: " + token);
            }
        }

        protected override void OnTick()
        {
            var now = Server.Time;
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), _newYork);
            var date = localNow.Date;
            if (date != _date)
            {
                CloseManaged("new_day_cleanup");
                _date = date;
                _eventDay = _easing.Contains(date) || _tightening.Contains(date);
                _startQuoteTime = _endQuoteTime = _lastWindowQuoteTime = DateTime.MinValue;
                _startMid = _endMid = 0;
                _sampleDone = _entryDone = _exitDone = _quoteGap = false;
            }
            var time = localNow.TimeOfDay;
            if (time < EntryTime.Subtract(MaxQuoteAge) || time > ExitTime.Add(MaxQuoteAge)) return;

            if (!_eventDay && time >= EntryTime && time <= ExitTime)
            {
                if (_lastWindowQuoteTime != DateTime.MinValue && now - _lastWindowQuoteTime > MaxQuoteAge)
                    _quoteGap = true;
                _lastWindowQuoteTime = now;
            }
            if (Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid) return;
            var mid = (Symbol.Bid + Symbol.Ask) / 2.0;
            if (!_eventDay && time >= EntryTime.Subtract(MaxQuoteAge) && time <= EntryTime)
            {
                _startQuoteTime = now;
                _startMid = mid;
            }
            if (!_eventDay && time >= ExitTime.Subtract(MaxQuoteAge) && time <= ExitTime)
            {
                _endQuoteTime = now;
                _endMid = mid;
            }
            if (!_eventDay && !_sampleDone && time > ExitTime)
            {
                _sampleDone = true;
                RecordNonEventSample();
            }

            if (_eventDay && !_entryDone && time > EntryTime)
            {
                _entryDone = true;
                EnterEvent(now, time);
            }
            if (_eventDay && !_exitDone && time >= ExitTime)
            {
                _exitDone = true;
                CloseManaged("14:10_time_exit");
            }
        }

        private void RecordNonEventSample()
        {
            if (_date.DayOfWeek == DayOfWeek.Saturday || _date.DayOfWeek == DayOfWeek.Sunday
                || _startQuoteTime == DateTime.MinValue || _endQuoteTime == DateTime.MinValue || _quoteGap)
                return;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(_date.Add(EntryTime), DateTimeKind.Unspecified), _newYork);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(_date.Add(ExitTime), DateTimeKind.Unspecified), _newYork);
            if (startUtc - _startQuoteTime > MaxQuoteAge || endUtc - _endQuoteTime > MaxQuoteAge)
                return;
            _priorFiveMinuteMoves.Enqueue(Math.Abs(_endMid - _startMid));
            if (_priorFiveMinuteMoves.Count > StopLookback) _priorFiveMinuteMoves.Dequeue();
            Print("T016_STOP_SAMPLE,date={0:yyyy-MM-dd},abs_move={1},sample_count={2}",
                _date, Math.Abs(_endMid - _startMid), _priorFiveMinuteMoves.Count);
        }

        private void EnterEvent(DateTime now, TimeSpan time)
        {
            var selected = Variant == TestVariant.TighteningShort ? _tightening.Contains(_date) : _easing.Contains(_date);
            if (!selected) return;
            var entryUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(_date.Add(EntryTime), DateTimeKind.Unspecified), _newYork);
            if (now - entryUtc > MaxQuoteAge || time >= ExitTime)
            {
                Print("T016_SKIP,date={0:yyyy-MM-dd},reason=late_or_missing_entry_tick,quote={1:O}", _date, now);
                return;
            }
            if (_priorFiveMinuteMoves.Count < StopLookback)
            {
                Print("T016_SKIP,date={0:yyyy-MM-dd},reason=stop_warmup,count={1}", _date, _priorFiveMinuteMoves.Count);
                return;
            }
            var direction = Variant == TestVariant.EasingLong ? TradeType.Buy
                : Variant == TestVariant.EasingRandomControl ? (StableSign(_date) > 0 ? TradeType.Buy : TradeType.Sell)
                : TradeType.Sell;
            var stopDistance = Median(_priorFiveMinuteMoves);
            var entryPrice = direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var stopPrice = direction == TradeType.Buy ? entryPrice - stopDistance : entryPrice + stopDistance;
            var executableDistance = direction == TradeType.Buy ? Symbol.Bid - stopPrice : stopPrice - Symbol.Ask;
            var minDistance = Symbol.MinDistanceType.ToString().IndexOf("Pip", StringComparison.OrdinalIgnoreCase) >= 0
                ? Symbol.MinStopLossDistance * Symbol.PipSize
                : entryPrice * Symbol.MinStopLossDistance / 100.0;
            if (stopDistance <= 0 || executableDistance <= 0 || executableDistance < minDistance)
            {
                Print("T016_SKIP,date={0:yyyy-MM-dd},reason=stop_too_close,stop_distance={1},broker_min={2},spread={3}",
                    _date, executableDistance, minDistance, Symbol.Ask - Symbol.Bid);
                return;
            }
            var volume = Symbol.NormalizeVolumeInUnits(Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("T016_SKIP,date={0:yyyy-MM-dd},reason=volume_below_minimum", _date);
                return;
            }
            var stopPips = stopDistance / Symbol.PipSize;
            Print("T016_SIGNAL,date={0:yyyy-MM-dd},variant={1},direction={2},quote_utc={3:O},bid={4},ask={5},spread={6},stop_distance={7},stop_pips={8}",
                _date, Variant, direction, now, Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid, stopDistance, stopPips);
            var result = ExecuteMarketOrder(direction, SymbolName, volume, _label, stopPips, null, BuildId);
            if (!result.IsSuccessful || result.Position == null)
                Print("T016_ORDER_FAILED,date={0:yyyy-MM-dd},error={1}", _date, result.Error);
            else
                Print("T016_ENTRY,date={0:yyyy-MM-dd},id={1},price={2},stop={3},volume={4}",
                    _date, result.Position.Id, result.Position.EntryPrice, result.Position.StopLoss, volume);
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            var half = sorted.Length / 2;
            return sorted.Length % 2 == 0 ? (sorted[half - 1] + sorted[half]) / 2.0 : sorted[half];
        }

        private static int StableSign(DateTime date)
        {
            unchecked
            {
                uint x = (uint)(date.Year * 10000 + date.Month * 100 + date.Day);
                x ^= x << 13; x ^= x >> 17; x ^= x << 5;
                return (x & 1) == 0 ? 1 : -1;
            }
        }

        private void CloseManaged(string reason)
        {
            if (_label == null) return;
            foreach (var position in Positions.FindAll(_label, SymbolName))
            {
                var result = ClosePosition(position);
                Print("T016_EXIT,date={0:yyyy-MM-dd},id={1},reason={2},quote_utc={3:O},success={4},error={5}",
                    _date, position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T016_CLOSED,date={0:yyyy-MM-dd},id={1},reason={2},net={3},direction={4}",
                    _date, args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }
    }
}
