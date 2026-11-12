using cAlgo.API;
using cAlgo.Robots;
using System.Globalization;

class Program
{
    static int checks;
    static DateTime Utc(string value)=>DateTime.Parse(value,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal);
    static void Assert(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static void Set(Robot r,string name,object value)=>r.GetType().GetProperty(name).SetValue(r,value);
    static Robot Diagnostic(Type type,string row){var r=(Robot)Activator.CreateInstance(type);Set(r,"Observations",row);r.Start();return r;}
    static Robot Gate(Type type,string row,string variant)
    {
        var r=(Robot)Activator.CreateInstance(type);
        Set(r,"Opportunities",row);Set(r,"DollarRisk",50.0);Set(r,"PositionLabel","gate_test");
        Set(r,"Variant",Enum.Parse(type.GetNestedType("Branch"),variant));
        return r;
    }
    static void CheckGates()
    {
        const string wide="2026-06-17T12:00:00Z|2026-06-17T12:20:00Z|+1|1990|2020|1|0.1";
        var start=Utc("2026-06-17T12:00:00Z");
        var gate=Gate(typeof(XauUsdPostJumpSpreadRecovery),wide,"Gated");gate.Start();gate.Tick(start.AddSeconds(1));
        Assert(gate.Orders==0,"Wide spread waits");
        gate.Symbol.Ask=gate.Symbol.Bid+0.05;
        // Direct callback preserves a deliberately narrowed synthetic quote.
        typeof(XauUsdPostJumpSpreadRecovery).GetMethod("OnTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(gate,null);
        Assert(gate.Orders==1,"Recovery enters first eligible quote");
        gate.Tick(start.AddMinutes(20));Assert(gate.Positions.Count==0,"Gate retains original exit");
        var expiry=Gate(typeof(XauUsdPostJumpSpreadRecovery),wide,"Gated");expiry.Start();expiry.Tick(start.AddSeconds(1));expiry.Tick(start.AddMinutes(10).AddSeconds(1));
        Assert(expiry.Orders==0 && expiry.Logs.Any(x=>x.Contains("gate_expired")),"Ten-minute wait expires");
        var baseline=Gate(typeof(XauUsdPostJumpSpreadRecovery),wide,"Baseline");baseline.Start();baseline.Tick(start.AddSeconds(1));Assert(baseline.Orders==1,"Baseline does not await recovery");
        var news=Gate(typeof(XauUsdMacroCalendarGate),wide,"Gated");news.Start();news.Tick(start.AddSeconds(1));Assert(news.Orders==0,"Calendar exclusion skips");
        var neutral=wide.Replace("|1|0.1","|0|0.1");
        var europe=Gate(typeof(XauUsdEuropeanOverlapGate),neutral,"Gated");europe.Start();europe.Tick(start.AddSeconds(1));Assert(europe.Orders==0,"Europe gate excludes outside window");
        var weekly=Gate(typeof(XauUsdWeeklyBoundaryQuoteGate),wide,"Gated");weekly.Start();weekly.Tick(start.AddSeconds(1));weekly.Tick(start.AddMinutes(20));Assert(weekly.Orders==0,"Weekly gate cannot extend original exit");
        var cut=Gate(typeof(XauUsdSurveyDispersionExposureGate),neutral,"Gated");
        Set(cut,"SurveyDecisions","2026-06-17T12:05:00Z|2026-06-17T12:06:00Z|2|1");cut.Start();cut.Tick(start.AddSeconds(1));Assert(cut.Orders==1,"Survey strategy entry unchanged");
        cut.Tick(start.AddMinutes(5).AddSeconds(1));Assert(cut.Positions.Count==0,"High forecast dispersion closes exposure before release");
        var cold=Gate(typeof(XauUsdSurveyDispersionExposureGate),neutral,"Baseline");Set(cold,"SurveyDecisions","2026-06-17T12:05:00Z|2026-06-17T12:06:00Z|2|1");cold.Start();cold.Tick(start.AddSeconds(1));cold.Tick(start.AddMinutes(5).AddSeconds(1));Assert(cold.Positions.Count==1,"Survey baseline retains exposure");
    }
    static void CheckRiskSizing()
    {
        foreach(var spec in new[]{
            (typeof(XauUsdClockVolatilityStopRisk),"2026-06-17T12:00:00Z|2026-06-17T12:20:00Z|+1|1990|2020|1|1980|0.001"),
            (typeof(XauUsdSemivarianceStopRisk),"2026-06-17T12:00:00Z|2026-06-17T12:20:00Z|+1|1990|2020|1|1980|0.003|0.001")})
        {
            var start=Utc("2026-06-17T12:00:00Z");
            var baseline=Gate(spec.Item1,spec.Item2,"Baseline");baseline.Start();baseline.Tick(start.AddSeconds(1));
            var scaled=Gate(spec.Item1,spec.Item2,"Gated");scaled.Start();scaled.Tick(start.AddSeconds(1));
            Assert(baseline.Orders==1 && scaled.Orders==1,"Risk variants use identical signal opportunities");
            Assert(baseline.Positions[0].StopLoss==1990 && scaled.Positions[0].StopLoss==1980,"Each variant preserves chosen absolute stop");
            Assert(scaled.Positions[0].VolumeInUnits<baseline.Positions[0].VolumeInUnits,"Wider stop reduces volume at fixed risk budget");
            Assert(scaled.Positions[0].TakeProfit==baseline.Positions[0].TakeProfit,"Stop scaling keeps original target");
            scaled.Tick(start.AddMinutes(20));baseline.Tick(start.AddMinutes(20));
            Assert(scaled.Positions.Count==0 && baseline.Positions.Count==0,"Both risk variants preserve original deadline");
        }
    }
    static void Main()
    {
        foreach(var spec in new[]{
            (typeof(XauUsdFomcUncertaintyResolution),"2026-06-17T18:10:00Z|-0.2|0.001|0.001|0.00001|0.00002|1",15,"T027"),
            (typeof(XauUsdShanghaiSegmentedVariance),"2026-06-17T06:30:00Z|0.1|0.2|0.3|0.01|0.02|0.03|0.04",30,"T028"),
            (typeof(XauUsdCentralBankInformationShock),"2026-06-17T18:05:00Z|0.001|-0.001|0.0001",15,"T029"),
            (typeof(XauUsdRealizedSkewQuantile),"2026-06-17T15:00:00Z|0.1|0.001|0.002|0.1|0.001|0.05|-0.01|-0.005",60,"T040"),
            (typeof(XauUsdTransactionDurationVariance),"2026-06-17T12:00:00Z|0.1|0.001|0.002|1000|0.001|0.002",5,"T043"),
            (typeof(XauUsdOilGoldCovarianceTail),"2026-06-17T12:00:00Z|0.01|0.02|0.001|0.001|0.002|0.1|0.001|0.1|0.2",30,"T050"),
            (typeof(XauUsdCashStressFlightComparison),"2026-06-17T12:00:00Z|-0.02|0.01|0.2|0.001|0.01|0.005|0.1",15,"T052"),
            (typeof(XauUsdPayrollComponentConflict),"2026-06-17T12:35:00Z|1|-1|0.1|0.001|0.01|300",15,"T054"),
            (typeof(XauUsdNewsAttentionJumpHazard),"2026-06-17T12:00:00Z|10|2|0.1|0.001|0.01|0.1|0.2|0.00000001|3",5,"T055"),
            (typeof(XauUsdSelfExcitingJumpHazard),"2026-06-17T12:00:00Z|2|0.01|0.001|0.1|0.2|0.3|0.00000001|3",15,"T056"),
            (typeof(XauUsdCryptoLiquidationTail),"2026-06-17T12:00:00Z|100000|-0.01|0.002|0.001|0.001|0.001|0.1|0.3|3|1",30,"T066"),
            (typeof(XauUsdGeopoliticalThreatHazard),"2026-06-17T12:00:00Z|100|86400|3600|120|0.001|0.1|0.2|0.00000001|3|0",5,"T067"),
            (typeof(XauUsdSlowVarianceMemory),"2026-06-17T12:00:00Z|0.001|0.01|0.03|0.0005|0.1|1|0.001|0.002|86400|604800|1",30,"T082"),
            (typeof(XauUsdOptionTermStructureHazard),"2026-06-17T14:00:00Z|24|20|0.1|0.001|0|0.1|0.2|0.00000001|3|1|2|1|0.01|0.1",60,"T089"),
            (typeof(XauUsdGvzIncrementalVariance),"2026-06-17T14:00:00Z|20|0.001|0.00001|0.00002",30,"T031"),
            (typeof(XauUsdJumpShareTailRisk),"2026-06-17T12:00:00Z|0.01|0.001|0.004|0.0001|0.2|0.3",30,"T032"),
            (typeof(XauUsdPolicyUncertaintyJumpHazard),"2026-06-17T12:00:00Z|120|86400|0.001|0.2|0|0.00000001|3|0.1|0.2",30,"T033"),
            (typeof(XauUsdDealerGammaFeedback),"2026-06-17T14:00:00Z|-10|0.001|2100|0.001",30,"T036"),
            (typeof(XauUsdQuoteWithdrawalTailRisk),"2026-06-17T12:00:00Z|2|-0.2|0.001|100|0.001|0.002",5,"T030")})
        {
            var start=Utc(spec.Item2.Split('|')[0]);
            var r=Diagnostic(spec.Item1,spec.Item2);
            Assert(!r.Stopped,spec.Item4+" valid inputs rejected");
            r.Tick(start);Assert(!r.Logs.Any(x=>x.Contains("_BEGIN")),"No same-receipt quote entry");
            r.Tick(start.AddSeconds(1));
            for(int second=2;second<=spec.Item3*60;second++)r.Tick(start.AddSeconds(second),2000+second*0.001);
            Assert(r.Logs.Count(x=>x.StartsWith(spec.Item4+"_OUTCOME"))==1,"Complete outcome exactly once");
            Assert(r.Orders==0,"Observation cBot must not invent directional orders");
            var stale=Diagnostic(spec.Item1,spec.Item2);stale.Tick(start.AddSeconds(3));
            Assert(stale.Logs.Any(x=>x.Contains("_SKIP")),"Stale start must skip");
            var gap=Diagnostic(spec.Item1,spec.Item2);gap.Tick(start.AddSeconds(1));gap.Tick(start.AddMinutes(spec.Item3));
            Assert(gap.Logs.Any(x=>x.Contains("_INVALID")),"Missing interior quotes must invalidate");
            var duplicate=Diagnostic(spec.Item1,spec.Item2+";"+spec.Item2);Assert(duplicate.Stopped,"Duplicate receipt must stop");
            var missing=Diagnostic(spec.Item1,"");Assert(missing.Stopped,"Missing inputs must stop");
            var censored=Diagnostic(spec.Item1,spec.Item2);censored.Tick(start.AddSeconds(1));censored.End();
            Assert(censored.Logs.Any(x=>x.Contains("_CENSORED")),"Partial window must be censored");
            var live=Diagnostic(spec.Item1,spec.Item2);live.RunningMode=RunningMode.RealTime;live.Start();Assert(live.Stopped,"Live observation guard");
        }
        foreach(var spec in new[]{(typeof(XauUsdLeveragedFundRebalance),"2026-06-17T19:00:00Z=+1"),(typeof(XauUsdMarginIncreaseLiquidation),"2026-06-17T12:00:00Z=-1")})
        {
            var r=(Robot)Activator.CreateInstance(spec.Item1);Set(r,"SignedReceiptsUtc",spec.Item2);Set(r,"FixedVolumeInUnits",1.0);Set(r,"PositionLabel","test");r.Start();
            Assert(!r.Stopped,"Trading configuration valid");var start=Utc(spec.Item2.Split('=')[0]);
            r.Tick(start);Assert(r.Orders==0,"Wait until receipt passed");r.Tick(start.AddSeconds(1));Assert(r.Orders==1,"Entry on next quote");
            r.FailClose=true;r.Tick(start.AddMinutes(60));Assert(r.Positions.Count==1,"Synthetic failed close retains position");
            r.FailClose=false;r.Tick(start.AddMinutes(60).AddSeconds(1));Assert(r.Positions.Count==0,"Failed close must retry on next tick");
            r.Tick(start.AddMinutes(61));Assert(r.Orders==1,"Event consumed once");
        }
        var invalid=Diagnostic(typeof(XauUsdFomcUncertaintyResolution),"2026-06-17T18:10:00Z|-0.2|0.001|0.001|NaN|0.00002|1");Assert(invalid.Stopped,"Nonfinite forecast rejected");
        checks+=TokyoWindowChecks.Run();
        CheckGates();
        CheckRiskSizing();
        checks+=CatalogExecutionChecks.Run();
        checks+=CatalogRiskChecks.Run();
        checks+=CatalogFlowChecks.Run();
        checks+=CatalogFinalSignalChecks.Run();
        checks+=PortfolioPolicyChecks.Run();
        checks+=ProtectionChecks.Run();
        Console.WriteLine($"PASS {checks} synthetic event checks; not market-performance evidence.");
    }
}
