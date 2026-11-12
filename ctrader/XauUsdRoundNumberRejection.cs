using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T015 V1: fade the first tick crossing of a previously untouched $100 level.
    /// Frozen exploratory choices: 08:00-12:00 New York entry, untouched since
    /// 00:00 New York, next-tick entry, prior 15 complete M1-bar range as stop,
    /// 15-minute exit. Placebo uses levels 100*n+37.5 with identical rules.
    /// Attach to M1 and backtest with broker bid/ask ticks, not M1 OHLC fills.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdRoundNumberRejection : Robot
    {
        private const string BuildId = "T015_ROUND_NUMBER_REJECTION_V1_20261104";
        private const int LookbackBars = 15;
        private const double Grid = 100.0;
        private const double PlaceboOffset = 37.5;
        private static readonly TimeSpan SessionStart = new TimeSpan(8, 0, 0);
        private static readonly TimeSpan SessionEnd = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan MaxQuoteGap = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan HoldingTime = TimeSpan.FromMinutes(15);

        public enum LevelSet { RoundHundreds, NonRoundPlacebo }

        [Parameter("Level Set", DefaultValue = LevelSet.RoundHundreds)]
        public LevelSet Levels { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T015_ROUND_REJECT")]
        public string PositionLabel { get; set; }

        [Parameter("Allow Live Trading", DefaultValue = false)]
        public bool AllowLiveTrading { get; set; }

        private readonly HashSet<int> _touchedLevels = new HashSet<int>();
        private DateTime _date = DateTime.MinValue;
        private DateTime _lastQuoteTime = DateTime.MinValue;
        private DateTime _exitAt = DateTime.MinValue;
        private double _lastMid;
        private bool _dayComplete;
        private bool _validDayStart;
        private bool _entryPending;
        private bool _entryAttempted;
        private int _pendingLevelIndex;
        private int _approachSign;
        private DateTime _touchTime;
        private double _stopDistance;
        private string _label;

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute)
            {
                Print("T015_STOP,reason=requires_m1,timeframe={0}", TimeFrame);
                Stop();
                return;
            }
            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading)
            {
                Print("T015_STOP,reason=live_trading_disabled");
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Levels;
            Positions.Closed += OnPositionClosed;
            Print("T015_START,build={0},levels={1},session=08:00-12:00_Eastern,stop=prior15m_range,exit=15m,volume={2},tick_size={3}",
                BuildId, Levels, FixedVolumeInUnits, Symbol.TickSize);
        }

        protected override void OnTick()
        {
            var now = Server.Time;
            var date = now.Date;
            if (date != _date)
            {
                CloseManaged("new_day_cleanup");
                _date = date;
                _touchedLevels.Clear();
                _lastQuoteTime = _touchTime = _exitAt = DateTime.MinValue;
                _lastMid = _stopDistance = 0;
                _dayComplete = _entryPending = _entryAttempted = false;
                _validDayStart = now.TimeOfDay < TimeSpan.FromMinutes(1);
                if (!_validDayStart)
                    Print("T015_SKIP,date={0:yyyy-MM-dd},reason=no_full_day_touch_history,start={1:O}", date, now);
            }
            if (_exitAt != DateTime.MinValue && now >= _exitAt)
            {
                CloseManaged("15m_time_exit");
                _exitAt = DateTime.MinValue;
            }
            if (!_validDayStart || _dayComplete || Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid)
                return;
            var mid = (Symbol.Bid + Symbol.Ask) / 2.0;
            if (_lastQuoteTime != DateTime.MinValue)
            {
                if (now - _lastQuoteTime > MaxQuoteGap && _entryPending)
                {
                    Print("T015_SKIP,date={0:yyyy-MM-dd},reason=stale_next_tick,touch={1:O},entry_quote={2:O}",
                        _date, _touchTime, now);
                    _entryPending = false;
                    _dayComplete = true;
                }
                if (_entryPending && !_dayComplete && now > _touchTime)
                {
                    _entryPending = false;
                    _entryAttempted = true;
                    Enter(mid, now);
                }
                MarkCrossings(_lastMid, mid, now);
            }
            _lastMid = mid;
            _lastQuoteTime = now;
        }

        private void MarkCrossings(double from, double to, DateTime now)
        {
            if (from == to || _dayComplete) return;
            var offset = Levels == LevelSet.RoundHundreds ? 0.0 : PlaceboOffset;
            var lo = Math.Min(from, to);
            var hi = Math.Max(from, to);
            var first = (int)Math.Ceiling((lo - offset) / Grid);
            var last = (int)Math.Floor((hi - offset) / Grid);
            for (var index = first; index <= last; index++)
            {
                if (!_touchedLevels.Add(index)) continue;
                if (now.TimeOfDay < SessionStart || now.TimeOfDay >= SessionEnd
                    || _entryAttempted || _entryPending || _date.DayOfWeek == DayOfWeek.Saturday
                    || _date.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                if (now - _lastQuoteTime > MaxQuoteGap)
                {
                    Print("T015_SKIP,date={0:yyyy-MM-dd},reason=stale_touch_quote,level={1}", _date, index * Grid + offset);
                    continue;
                }
                double range;
                if (!TryPriorRange(now, out range))
                {
                    Print("T015_SKIP,date={0:yyyy-MM-dd},reason=incomplete_prior15m,level={1}", _date, index * Grid + offset);
                    continue;
                }
                _pendingLevelIndex = index;
                _approachSign = to > from ? 1 : -1;
                _stopDistance = range;
                _touchTime = now;
                _entryPending = true;
                Print("T015_TOUCH,date={0:yyyy-MM-dd},level={1},direction={2},quote={3:O},prior15m_range={4}",
                    _date, index * Grid + offset, _approachSign, now, range);
                break;
            }
        }

        private bool TryPriorRange(DateTime now, out double range)
        {
            range = 0;
            if (Bars.Count < LookbackBars + 2) return false;
            var high = double.MinValue;
            var low = double.MaxValue;
            var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            for (var i = 1; i <= LookbackBars; i++)
            {
                var bar = Bars.Last(i);
                if (bar.OpenTime != currentMinute.AddMinutes(-i)) return false;
                high = Math.Max(high, bar.High);
                low = Math.Min(low, bar.Low);
            }
            range = high - low;
            return range > 0;
        }

        private void Enter(double mid, DateTime now)
        {
            _dayComplete = true;
            var direction = _approachSign > 0 ? TradeType.Sell : TradeType.Buy;
            var entry = direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            var stopPrice = direction == TradeType.Buy ? entry - _stopDistance : entry + _stopDistance;
            var executableDistance = direction == TradeType.Buy ? Symbol.Bid - stopPrice : stopPrice - Symbol.Ask;
            var minDistance = Symbol.MinDistanceType.ToString().IndexOf("Pip", StringComparison.OrdinalIgnoreCase) >= 0
                ? Symbol.MinStopLossDistance * Symbol.PipSize
                : entry * Symbol.MinStopLossDistance / 100.0;
            if (executableDistance <= 0 || executableDistance < minDistance)
            {
                Print("T015_SKIP,date={0:yyyy-MM-dd},reason=stop_too_close,stop_distance={1},broker_min={2}",
                    _date, executableDistance, minDistance);
                return;
            }
            var volume = Symbol.NormalizeVolumeInUnits(Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("T015_SKIP,date={0:yyyy-MM-dd},reason=volume_below_minimum", _date);
                return;
            }
            Print("T015_SIGNAL,date={0:yyyy-MM-dd},level={1},variant={2},direction={3},touch={4:O},entry_quote={5:O},bid={6},ask={7},mid={8},stop_distance={9}",
                _date, _pendingLevelIndex * Grid + (Levels == LevelSet.RoundHundreds ? 0.0 : PlaceboOffset),
                Levels, direction, _touchTime, now, Symbol.Bid, Symbol.Ask, mid, _stopDistance);
            var result = ExecuteMarketOrder(direction, SymbolName, volume, _label,
                _stopDistance / Symbol.PipSize, null, BuildId);
            if (!result.IsSuccessful || result.Position == null)
                Print("T015_ORDER_FAILED,date={0:yyyy-MM-dd},error={1}", _date, result.Error);
            else
            {
                _exitAt = _touchTime.Add(HoldingTime);
                Print("T015_ENTRY,date={0:yyyy-MM-dd},id={1},price={2},stop={3},exit_at={4:O}",
                    _date, result.Position.Id, result.Position.EntryPrice, result.Position.StopLoss, _exitAt);
            }
        }

        private void CloseManaged(string reason)
        {
            if (_label == null) return;
            foreach (var position in Positions.FindAll(_label, SymbolName))
            {
                var result = ClosePosition(position);
                Print("T015_EXIT,date={0:yyyy-MM-dd},id={1},reason={2},quote={3:O},success={4},error={5}",
                    _date, position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T015_CLOSED,date={0:yyyy-MM-dd},id={1},reason={2},net={3},direction={4}",
                    _date, args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }
    }
}
