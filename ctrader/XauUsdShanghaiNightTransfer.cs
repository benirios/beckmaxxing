using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T022: XAUUSD 15:00-to-21:30 China midpoint sign predicts the next
    /// eligible SHFE day's 09:00-to-09:30 XAUUSD direction. Tick-data research.
    /// Supply verified TARGET=PREDICTOR date pairs from the SHFE calendar.
    /// Research only; real-time trading is disabled.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdShanghaiNightTransfer : Robot
    {
        private const string BuildId = "T022_SHANGHAI_NIGHT_TRANSFER_V1_20261104";
        private static readonly TimeSpan PredictorStart = new TimeSpan(15, 0, 0);
        private static readonly TimeSpan PredictorEnd = new TimeSpan(21, 30, 0);
        private static readonly TimeSpan EntryTime = new TimeSpan(9, 0, 0);
        private static readonly TimeSpan ExitTime = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan MaxQuoteAge = TimeSpan.FromSeconds(2);

        public enum TestVariant { FollowSign, OppositeSign, AlwaysLong, RandomDirection }

        [Parameter("Test Variant", DefaultValue = TestVariant.FollowSign)]
        public TestVariant Variant { get; set; }

        [Parameter("SHFE Dates TARGET=PREDICTOR (comma separated)", DefaultValue = "")]
        public string SessionPairs { get; set; }

        [Parameter("Fixed Volume (Units)", DefaultValue = 1.0, MinValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Position Label", DefaultValue = "XAU_T022_SH_NIGHT")]
        public string PositionLabel { get; set; }

        private readonly Dictionary<DateTime, DateTime> _targetToPredictor = new Dictionary<DateTime, DateTime>();
        private readonly Dictionary<DateTime, DateTime> _predictorToTarget = new Dictionary<DateTime, DateTime>();
        private readonly Dictionary<DateTime, int> _signalSigns = new Dictionary<DateTime, int>();
        private TimeZoneInfo _shanghai;
        private DateTime _date = DateTime.MinValue;
        private DateTime _openQuoteTime = DateTime.MinValue;
        private DateTime _closeQuoteTime = DateTime.MinValue;
        private double _openMid;
        private double _closeMid;
        private bool _predictorDone;
        private bool _entryDone;
        private bool _exitDone;
        private string _label;

        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime)
            {
                Print("T022_STOP,reason=research_only_no_live_trading");
                Stop();
                return;
            }
            try
            {
                try { _shanghai = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
                catch (TimeZoneNotFoundException) { _shanghai = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
                foreach (var token in (SessionPairs ?? "").Split(
                    new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = token.Trim().Split('=');
                    DateTime target, predictor;
                    if (parts.Length != 2
                        || !DateTime.TryParseExact(parts[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out target)
                        || !DateTime.TryParseExact(parts[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out predictor)
                        || predictor >= target || !_targetToPredictor.TryAdd(target.Date, predictor.Date)
                        || !_predictorToTarget.TryAdd(predictor.Date, target.Date))
                        throw new FormatException("Invalid, duplicate, or nonchronological TARGET=PREDICTOR pair: " + token);
                }
                if (_targetToPredictor.Count == 0)
                    throw new FormatException("Verified SHFE target/predictor day pairs are required.");
            }
            catch (Exception error)
            {
                Print("T022_STOP,reason=configuration,error={0}", error.Message);
                Stop();
                return;
            }
            _label = PositionLabel + "_" + Variant;
            Positions.Closed += OnPositionClosed;
            Print("T022_START,build={0},variant={1},pairs={2},timezone=Asia/Shanghai,predictor=15:00-21:30,entry=09:00,exit=09:30,volume={3},tick_size={4}",
                BuildId, Variant, _targetToPredictor.Count, FixedVolumeInUnits, Symbol.TickSize);
        }

        protected override void OnTick()
        {
            var now = DateTime.SpecifyKind(Server.Time, DateTimeKind.Utc);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, _shanghai);
            var date = localNow.Date;
            if (date != _date)
            {
                if (_date != DateTime.MinValue && !_predictorDone)
                    FinalizePredictor();
                CloseManaged("new_day_cleanup");
                _date = date;
                _openQuoteTime = _closeQuoteTime = DateTime.MinValue;
                _openMid = _closeMid = 0;
                _predictorDone = _entryDone = _exitDone = false;
            }
            var time = localNow.TimeOfDay;
            if (Symbol.Bid > 0 && Symbol.Ask > Symbol.Bid && _predictorToTarget.ContainsKey(date))
            {
                var mid = (Symbol.Bid + Symbol.Ask) / 2.0;
                if (time >= PredictorStart.Subtract(MaxQuoteAge) && time <= PredictorStart)
                {
                    _openQuoteTime = now;
                    _openMid = mid;
                }
                if (time >= PredictorEnd.Subtract(MaxQuoteAge) && time <= PredictorEnd)
                {
                    _closeQuoteTime = now;
                    _closeMid = mid;
                }
                if (!_predictorDone && time > PredictorEnd)
                {
                    _predictorDone = true;
                    FinalizePredictor();
                }
            }
            if (!_targetToPredictor.ContainsKey(date)) return;
            if (!_entryDone && time >= EntryTime)
            {
                _entryDone = true;
                Enter(now, time);
            }
            if (!_exitDone && time >= ExitTime)
            {
                _exitDone = true;
                CloseManaged("09:30_time_exit");
            }
        }

        private void FinalizePredictor()
        {
            DateTime target;
            if (!_predictorToTarget.TryGetValue(_date, out target)) return;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(_date.Add(PredictorStart), DateTimeKind.Unspecified), _shanghai);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(_date.Add(PredictorEnd), DateTimeKind.Unspecified), _shanghai);
            if (_openQuoteTime == DateTime.MinValue || _closeQuoteTime == DateTime.MinValue
                || startUtc - _openQuoteTime > MaxQuoteAge || endUtc - _closeQuoteTime > MaxQuoteAge)
            {
                Print("T022_PREDICTOR_SKIP,predictor={0:yyyy-MM-dd},target={1:yyyy-MM-dd},reason=missing_or_stale_boundary_quotes", _date, target);
                return;
            }
            var sign = Math.Sign(_closeMid - _openMid);
            _signalSigns[target] = sign;
            Print("T022_PREDICTOR,predictor={0:yyyy-MM-dd},target={1:yyyy-MM-dd},open_mid={2},close_mid={3},sign={4},open_quote={5:O},close_quote={6:O}",
                _date, target, _openMid, _closeMid, sign, _openQuoteTime, _closeQuoteTime);
        }

        private void Enter(DateTime now, TimeSpan time)
        {
            var entryUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(_date.Add(EntryTime), DateTimeKind.Unspecified), _shanghai);
            if (now - entryUtc > MaxQuoteAge || time >= ExitTime || Symbol.Bid <= 0 || Symbol.Ask <= Symbol.Bid)
            {
                Print("T022_SKIP,date={0:yyyy-MM-dd},reason=late_or_invalid_entry_quote,quote={1:O}", _date, now);
                return;
            }
            int predictorSign;
            if (!_signalSigns.TryGetValue(_date, out predictorSign) || predictorSign == 0)
            {
                Print("T022_SKIP,date={0:yyyy-MM-dd},reason=no_nonzero_prior_predictor", _date);
                return;
            }
            var sign = Variant == TestVariant.FollowSign ? predictorSign
                : Variant == TestVariant.OppositeSign ? -predictorSign
                : Variant == TestVariant.AlwaysLong ? 1 : StableSign(_date);
            var direction = sign > 0 ? TradeType.Buy : TradeType.Sell;
            var volume = Symbol.NormalizeVolumeInUnits(
                Math.Min(FixedVolumeInUnits, Symbol.VolumeInUnitsMax), RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("T022_SKIP,date={0:yyyy-MM-dd},reason=volume_below_minimum", _date);
                return;
            }
            Print("T022_SIGNAL,date={0:yyyy-MM-dd},variant={1},predictor_date={2:yyyy-MM-dd},predictor_sign={3},direction={4},quote_utc={5:O},bid={6},ask={7},spread={8}",
                _date, Variant, _targetToPredictor[_date], predictorSign, direction, now,
                Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid);
            var result = ExecuteMarketOrder(direction, SymbolName, volume, _label,
                (double?)null, (double?)null, BuildId);
            if (!result.IsSuccessful || result.Position == null)
                Print("T022_ORDER_FAILED,date={0:yyyy-MM-dd},error={1}", _date, result.Error);
            else
                Print("T022_ENTRY,date={0:yyyy-MM-dd},id={1},price={2},volume={3}",
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
                Print("T022_EXIT,date={0:yyyy-MM-dd},id={1},reason={2},quote_utc={3:O},success={4},error={5}",
                    _date, position.Id, reason, Server.Time, result.IsSuccessful, result.Error);
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label == _label && args.Position.SymbolName == SymbolName)
                Print("T022_CLOSED,date={0:yyyy-MM-dd},id={1},reason={2},net={3},direction={4}",
                    _date, args.Position.Id, args.Reason, args.Position.NetProfit, args.Position.TradeType);
        }

        protected override void OnStop() { Positions.Closed -= OnPositionClosed; }
    }
}
