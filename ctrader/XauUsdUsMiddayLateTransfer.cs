using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T023: XAUUSD 11:30-12:00 midpoint sign predicts the 15:30-16:00
    /// XAUUSD return on verified full NYSE/GLD trading days. Tick-data research.
    /// Calendar input must exclude holidays and early closes. No live trading.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdUsMiddayLateTransfer : Robot
    {
        private const string BuildId = "T023_US_MIDDAY_LATE_TRANSFER_V1_20261104";
        private static readonly TimeSpan PredictorStart = new TimeSpan(11, 30, 0);
        private static readonly TimeSpan PredictorEnd = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan EntryTime = new TimeSpan(15, 30, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(16, 0, 0);
        private static readonly TimeSpan MaxQuoteAge = TimeSpan.FromSeconds(2);

        public enum TestVariant { FollowSign, OppositeSign, AlwaysLong, RandomDirection }

        [Parameter("Test Variant", DefaultValue = TestVariant.FollowSign)]
        public TestVariant Variant { get; set; }

        [Parameter("Verified Full NYSE Dates (YYYY-MM-DD, comma separated)", DefaultValue = "")]
        public string NyseDates { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T023_US_MIDDAY")]
        public string PositionLabel { get; set; }

        private readonly HashSet<DateTime> _dates = new HashSet<DateTime>();
        private TimeZoneInfo _newYork;
        private DateTime _date = DateTime.MinValue;
        private DateTime _startQuoteTime = DateTime.MinValue;
        private DateTime _endQuoteTime = DateTime.MinValue;
        private double _startMid;
        private double _endMid;
        private int _predictorSign;
        private bool _predictorDone;
        private bool _entryDone;
        private bool _exitDone;
        private string _label;

        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime)
            {
                Print("T023_STOP,reason=research_only_no_live_trading");
                Stop();
                return;
            }
            try
            {
                try { _newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
                catch (TimeZoneNotFoundException) { _newYork = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
                foreach (var token in (NyseDates ?? "").Split(
                    new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    DateTime date;
                    if (!DateTime.TryParseExact(token.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out date) || !_dates.Add(date.Date))
                        throw new FormatException("Invalid or duplicate NYSE date: " + token);
                }
                if (_dates.Count == 0)
                    throw new FormatException("Verified full NYSE dates are required.");
            }
            catch (Exception error)
            {
                Print("T023_STOP,reason=configuration,error={0}", error.Message);
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Variant;
            Positions.Closed += OnPositionClosed;
            Print("T023_START,build={0},variant={1},dates={2},timezone=America/New_York,predictor=11:30-12:00,entry=15:30,exit=16:00,volume={3},tick_size={4}",
                BuildId, Variant, _dates.Count, FixedVolumeInUnits, Symbol.TickSize);
        }

        protected override void OnTick()
        {
            var now = DateTime.SpecifyKind(Server.Time, DateTimeKind.Utc);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, _newYork);
            var date = localNow.Date;
            if (date != _date)
            {
                CloseManaged("new_day_cleanup");
                _date = date;
                _startQuoteTime = _endQuoteTime = DateTime.MinValue;
                _startMid = _endMid = 0;
                _predictorSign = 0;
                _predictorDone = _entryDone = _exitDone = false;
            }
            if (!_dates.Contains(date)) return;
            var time = localNow.TimeOfDay;
            if (Symbol.Bid > 0 && Symbol.Ask > Symbol.Bid)
            {
                var mid = (Symbol.Bid + Symbol.Ask) / 2.0;
                if (time >= PredictorStart.Subtract(MaxQuoteAge) && time <= PredictorStart)
                {
                    _startQuoteTime = now;
                    _startMid = mid;
                }
                if (time >= PredictorEnd.Subtract(MaxQuoteAge) && time <= PredictorEnd)
                {
                    _endQuoteTime = now;
                    _endMid = mid;
                }
            }
            if (!_predictorDone && time > PredictorEnd)
            {
                _predictorDone = true;
                FinalizePredictor();
            }
            if (!_entryDone && time >= EntryTime)
            {
                _entryDone = true;
                Enter(now, time);
            }
            if (!_exitDone && time >= ExitTime)
            {
                _exitDone = true;
                CloseManaged("16:00_time_exit");
            }
        }

        private void FinalizePredictor()
        {
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(_date.Add(PredictorStart), DateTimeKind.Unspecified), _newYork);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(_date.Add(PredictorEnd), DateTimeKind.Unspecified), _newYork);
            if (_startQuoteTime == DateTime.MinValue || _endQuoteTime == DateTime.MinValue
                || startUtc - _startQuoteTime > MaxQuoteAge || endUtc - _endQuoteTime > MaxQuoteAge)
            {
                Print("T023_PREDICTOR_SKIP,date={0:yyyy-MM-dd},reason=missing_or_stale_boundary_quotes", _date);
                return;
            }
            _predictorSign = Math.Sign(_endMid - _startMid);
            Print("T023_PREDICTOR,date={0:yyyy-MM-dd},start_mid={1},end_mid={2},sign={3},start_quote={4:O},end_quote={5:O}",
                _date, _startMid, _endMid, _predictorSign, _startQuoteTime, _endQuoteTime);
        }

        private void Enter(DateTime now, TimeSpan time)
        {
            var entryUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(_date.Add(EntryTime), DateTimeKind.Unspecified), _newYork);
            if (now - entryUtc > MaxQuoteAge || time >= ExitTime || Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid)
            {
                Print("T023_SKIP,date={0:yyyy-MM-dd},reason=late_or_invalid_entry_quote,quote={1:O}", _date, now);
                return;
            }
            if (_predictorSign == 0)
            {
                Print("T023_SKIP,date={0:yyyy-MM-dd},reason=no_nonzero_midday_predictor", _date);
                return;
            }
            var sign = Variant == TestVariant.FollowSign ? _predictorSign
                : Variant == TestVariant.OppositeSign ? -_predictorSign
                : Variant == TestVariant.AlwaysLong ? 1 : StableSign(_date);
            var direction = sign > 0 ? TradeType.Buy : TradeType.Sell;
            var volume = Symbol.NormalizeVolumeInUnits(
                Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("T023_SKIP,date={0:yyyy-MM-dd},reason=volume_below_minimum", _date);
                return;
            }
            Print("T023_SIGNAL,date={0:yyyy-MM-dd},variant={1},predictor_sign={2},direction={3},quote_utc={4:O},bid={5},ask={6},spread={7}",
                _date, Variant, _predictorSign, direction, now,
                Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid);
            var result = ExecuteMarketOrder(direction, SymbolName, volume, _label,
                (double?)null, (double?)null, BuildId);
            if (!result.IsSuccessful || result.Position == null)
                Print("T023_ORDER_FAILED,date={0:yyyy-MM-dd},error={1}", _date, result.Error);
            else
                Print("T023_ENTRY,date={0:yyyy-MM-dd},id={1},price={2},volume={3}",
                    _date, result.Position.Id, result.Position.EntryPrice, volume);
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
                Print("T023_EXIT,date={0:yyyy-MM-dd},id={1},reason={2},quote_utc={3:O},success={4},error={5}",
                    _date, position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T023_CLOSED,date={0:yyyy-MM-dd},id={1},reason={2},net={3},direction={4}",
                    _date, args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }
    }
}
