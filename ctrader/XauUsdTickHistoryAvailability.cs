using System;
using System.Globalization;
using cAlgo.API;

namespace cAlgo.Robots
{
    // Read-only history metadata diagnostic. No orders or file access.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdTickHistoryAvailability : Robot
    {
        [Parameter("Fixed Volume (unused)", DefaultValue = 1.0)]
        public double FixedVolumeInUnits { get; set; }
        [Parameter("Position Label (unused)", DefaultValue = "DATA_PROBE")]
        public string PositionLabel { get; set; }
        [Parameter("Allow Live Trading", DefaultValue = false)]
        public bool AllowLiveTrading { get; set; }
        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime) { Stop(); return; }
            Print("HISTORY_PROBE_START,symbol={0},orders=none", SymbolName);
            try
            {
                var ticks = MarketData.GetTicks(SymbolName);
                var first = ticks.GetServerFirstTime();
                Print("HISTORY_PROBE_FIRST,utc={0},loaded_count={1}",
                    DateTime.SpecifyKind(first, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture), ticks.Count);
            }
            catch (Exception error) { Print("HISTORY_PROBE_UNAVAILABLE,error={0}", error.Message); }
            Stop();
        }
    }
}
