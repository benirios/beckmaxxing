using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T006: an extreme negative five-minute S&P 500 move may precede a positive
    /// XAUUSD response. Inference from flight-to-gold research, not a validated edge.
    /// Attach to XAUUSD M1. Reference symbol must be the broker's S&P 500 index CFD.
    /// </summary>
    [Robot(TimeZone = TimeZones.EasternStandardTime, AccessRights = AccessRights.None)]
    public class XauUsdEquityDrawdownFlightToGold : Robot
    {
        private const int LookbackSessions = 60;
        private const int RegularSessionBars = 78;
        private const int HoldMinutes = 30;
        private const double TailPercentile = 0.01;
        private const string BuildId = "T006_SP500_DRAWDOWN_FLIGHT_TO_GOLD_V1";

        [Parameter("S&P 500 Symbol", Group = "Signal", DefaultValue = "US500")]
        public string ReferenceSymbol { get; set; }

        [Parameter("Position Label", Group = "Operations", DefaultValue = "XAU_T006_FLIGHT_GOLD_V1")]
        public string PositionLabel { get; set; }

        [Parameter("Fixed Volume (Units)", Group = "Exposure", DefaultValue = 1.0, MinValue = 1.0, Step = 1.0)]
        public double FixedVolumeInUnits { get; set; }

        [Parameter("Allow Live Trading", Group = "Safety", DefaultValue = false)]
        public bool AllowLiveTrading { get; set; }

        private Bars _referenceBars;
        private readonly Queue<double> _priorReturns = new Queue<double>();
        private readonly Dictionary<DateTime, int> _barsPerSession = new Dictionary<DateTime, int>();
        private int _lastProcessedIndex = -1;
        private DateTime _pendingSignalTime = DateTime.MinValue;
        private double _pendingReturn;
        private double _pendingThreshold;
        private DateTime _positionEntryTime = DateTime.MinValue;

        protected override void OnStart()
        {
            if (TimeFrame != TimeFrame.Minute)
            {
                Print("T006_ERROR,reason=requires_xauusd_m1,timeframe={0}", TimeFrame);
                Stop();
                return;
            }
            if (RunningMode == RunningMode.RealTime && !AllowLiveTrading)
            {
                Print("T006_ERROR,reason=live_trading_disabled");
                Stop();
                return;
            }
            _referenceBars = MarketData.GetBars(TimeFrame.Minute5, ReferenceSymbol);
            Print("T006_START,build={0},symbol={1},reference={2},tail={3:P2},prior_reference_bars={4},hold_minutes={5},timezone={6}",
                BuildId, SymbolName, ReferenceSymbol, TailPercentile,
                LookbackSessions * RegularSessionBars, HoldMinutes, TimeZone.DisplayName);
        }

        protected override void OnBar()
        {
            ProcessCompletedReferenceBars();

            var position = Positions.Find(PositionLabel, SymbolName);
            if (position != null && _positionEntryTime != DateTime.MinValue
                && Server.Time - _positionEntryTime >= TimeSpan.FromMinutes(HoldMinutes))
            {
                var close = ClosePosition(position);
                Print("T006_TIME_EXIT,position={0},time={1:O},success={2},error={3}",
                    position.Id, Server.Time, close.IsSuccessful, close.Error);
            }
        }

        protected override void OnTick()
        {
            if (_pendingSignalTime == DateTime.MinValue || Server.Time < _pendingSignalTime)
                return;
            var signalTime = _pendingSignalTime;
            _pendingSignalTime = DateTime.MinValue;
            if (Positions.Find(PositionLabel, SymbolName) != null)
            {
                Print("T006_SIGNAL_SKIPPED,time={0:O},reason=position_already_open", signalTime);
                return;
            }
            var volume = Symbol.NormalizeVolumeInUnits(FixedVolumeInUnits, RoundingMode.Down);
            if (volume < Symbol.VolumeInUnitsMin)
            {
                Print("T006_SIGNAL_SKIPPED,time={0:O},reason=volume_below_minimum,volume={1}", signalTime, volume);
                return;
            }
            var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, PositionLabel,
                (double?)null, (double?)null, "T006 S&P 500 extreme drawdown; fixed 30-minute hold");
            if (!result.IsSuccessful || result.Position == null)
            {
                Print("T006_ORDER_FAILED,time={0:O},error={1}", Server.Time, result.Error);
                return;
            }
            _positionEntryTime = result.Position.EntryTime;
            Print("T006_ENTRY,signal_time={0:O},entry_time={1:O},sp500_return={2},prior_p01={3},bid={4},ask={5},spread={6},volume={7}",
                signalTime, result.Position.EntryTime, _pendingReturn, _pendingThreshold,
                Symbol.Bid, Symbol.Ask, Symbol.Ask - Symbol.Bid, result.Position.VolumeInUnits);
        }

        protected override void OnStop()
        {
            Print("T006_STOP,reference_bars={0},retained_returns={1}", _lastProcessedIndex + 1, _priorReturns.Count);
        }

        private void ProcessCompletedReferenceBars()
        {
            if (_referenceBars == null || _referenceBars.Count < 2)
                return;
            var lastClosed = _referenceBars.Count - 2;
            for (var i = _lastProcessedIndex + 1; i <= lastClosed; i++)
            {
                _lastProcessedIndex = i;
                var openTime = _referenceBars.OpenTimes[i];
                var closeTime = openTime.AddMinutes(5);
                var localTime = openTime.TimeOfDay;
                if (openTime.DayOfWeek == DayOfWeek.Saturday || openTime.DayOfWeek == DayOfWeek.Sunday
                    || localTime < new TimeSpan(9, 30, 0) || localTime >= new TimeSpan(16, 0, 0))
                    continue;

                var sessionDate = openTime.Date;
                int count;
                _barsPerSession.TryGetValue(sessionDate, out count);
                _barsPerSession[sessionDate] = count + 1;

                var open = _referenceBars.OpenPrices[i];
                var close = _referenceBars.ClosePrices[i];
                if (open <= 0 || close <= 0)
                    continue;
                var change = close / open - 1.0;

                // Threshold uses only retained bars that closed before this candidate bar.
                if (_priorReturns.Count == LookbackSessions * RegularSessionBars)
                {
                    var threshold = NearestRank(_priorReturns, TailPercentile);
                    if (change < 0 && change <= threshold && closeTime <= Server.Time)
                    {
                        _pendingSignalTime = closeTime;
                        _pendingReturn = change;
                        _pendingThreshold = threshold;
                        Print("T006_SIGNAL,reference_bar={0:O},reference_close={1},return={2},prior_p01={3},session_bars={4}",
                            openTime, close, change, threshold, _barsPerSession[sessionDate]);
                    }
                }

                _priorReturns.Enqueue(change);
                while (_priorReturns.Count > LookbackSessions * RegularSessionBars)
                    _priorReturns.Dequeue();
            }
        }

        private static double NearestRank(IEnumerable<double> source, double percentile)
        {
            var sorted = source.OrderBy(x => x).ToArray();
            var index = Math.Max(0, (int)Math.Ceiling(percentile * sorted.Length) - 1);
            return sorted[index];
        }
    }
}
