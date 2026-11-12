using cAlgo.API;
using cAlgo.Robots;
using System.Globalization;
static class CatalogRiskChecks
{
    static int checks;
    static readonly DateTime Start=DateTime.Parse("2026-06-17T14:00:00Z",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
    static void Assert(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
    static void Set(Robot r,string name,object value)=>r.GetType().GetProperty(name).SetValue(r,value);
    static Robot Cost(Type type,string features,int state=1,string branch="Gated")
    {
        var r=(Robot)Activator.CreateInstance(type);Set(r,"DollarRisk",50.0);Set(r,"PositionLabel","risk_test");Set(r,"Variant",Enum.Parse(type.GetNestedType("Branch"),branch));
        Set(r,"Opportunities",$"{Start:yyyy-MM-ddTHH:mm:ssZ}|{Start.AddMinutes(20):yyyy-MM-ddTHH:mm:ssZ}|+1|1990|2020|{state}|1|{features}");r.Start();return r;
    }
    static Robot Feature(Type type,string features,int minutes,string branch="Primary")
    {
        var r=(Robot)Activator.CreateInstance(type);Set(r,"RiskBudget",50.0);Set(r,"PositionLabel","risk_feature");Set(r,"Variant",Enum.Parse(type.GetNestedType("TestBranch"),branch));Set(r,"FeatureRows",$"{Start:yyyy-MM-ddTHH:mm:ssZ}|{Start.AddMinutes(minutes):yyyy-MM-ddTHH:mm:ssZ}|10|{features}");r.Start();return r;
    }
    public static int Run()
    {
        var prebreak=Cost(typeof(XauUsdPreBreakExecutionGate),"600|0.01");prebreak.Tick(Start.AddSeconds(1));Assert(prebreak.Orders==0,"T047 skips actual last-15-minute broker window");
        var ordinary=Cost(typeof(XauUsdPreBreakExecutionGate),"1200|0.01",0);ordinary.Tick(Start.AddSeconds(1));Assert(ordinary.Orders==1,"T047 ordinary opportunities retained");
        var badCalendar=Cost(typeof(XauUsdPreBreakExecutionGate),"1200|0.01",1);Assert(badCalendar.Stopped,"T047 state must agree with broker schedule");
        var depth=Cost(typeof(XauUsdEquityDepthCostGate),"0.2|0.1|-0.01|0.001|0.1|0.2");depth.Tick(Start.AddSeconds(1));Assert(depth.Orders==0,"T048 depth-based high-cost gate");
        var control=Cost(typeof(XauUsdEquityDepthCostGate),"0.2|0.1|-0.01|0.001|0.1|0.2",1,"Baseline");control.Tick(Start.AddSeconds(1));Assert(control.Orders==1,"T048 baseline retains same distressed opportunities");control.Tick(Start.AddMinutes(20));Assert(control.Logs.Any(x=>x.Contains("T048_REALIZED_FILL")),"T048 records engine historical closing fill");
        var lowSentiment=Cost(typeof(XauUsdSentimentReleaseLiquidityGate),"-1|0|100|0.001|10");lowSentiment.Tick(Start.AddSeconds(1));var alternateSurprise=Cost(typeof(XauUsdSentimentReleaseLiquidityGate),"-1|0|-100|0.001|10");alternateSurprise.Tick(Start.AddSeconds(1));Assert(lowSentiment.Orders==0 && alternateSurprise.Orders==0,"T051 gate ignores realized surprise evaluation control");
        var halted=Cost(typeof(XauUsdFuturesInterruptionExecution),"1|10|0.01|0.001|0.2");halted.Tick(Start.AddSeconds(1));Assert(halted.Orders==0,"T058 confirmed interruption gate");
        var proximity=Cost(typeof(XauUsdFuturesInterruptionExecution),"0|10|0.01|0.001|0.2",1);Assert(proximity.Stopped,"T058 does not equate a non-halt with a confirmed interruption");
        var settlement=Cost(typeof(XauUsdSettlementWindowExecution),"2026-06-17T13:59:00Z|2026-06-17T14:01:00Z|GC_TEST");settlement.Tick(Start.AddSeconds(1));Assert(settlement.Orders==1,"T057 cost hypothesis does not invent directional exclusion");
        var wrongWindow=Cost(typeof(XauUsdSettlementWindowExecution),"2026-06-17T13:55:00Z|2026-06-17T13:59:00Z|GC_TEST");Assert(wrongWindow.Stopped,"T057 state checked against supplied historical procedure timestamps");
        var skew=Feature(typeof(XauUsdImpliedSkewDirection),"0.5|0.001|20|0.1|-0.002|0.01",120);skew.Tick(Start.AddSeconds(1));var noSkew=Feature(typeof(XauUsdImpliedSkewDirection),"0.5|0.001|20|0.1|-0.002|0.01",120,"GoldOnly");noSkew.Tick(Start.AddSeconds(1));Assert(skew.Positions.Single().TradeType==TradeType.Buy && noSkew.Positions.Single().TradeType==TradeType.Sell,"T049 skew addition changes prior-fit forecast, not control model");
        var tips=Feature(typeof(XauUsdRealYieldOpportunityCost),"0.001|-0.001|0|0|100|1|2",30);tips.Tick(Start.AddSeconds(1));Assert(tips.Positions.Single().TradeType==TradeType.Sell,"T053 uses real yield, not nominal move");
        var stale=Feature(typeof(XauUsdRealYieldOpportunityCost),"0.001|0.001|0|0|100|3|2",30);Assert(stale.Stopped,"T053 rejects stale TIPS marks");
        var hazard=new XauUsdNewsAttentionJumpHazard{Observations="2026-06-17T14:00:00Z|10|2|0.1|0.001|0.01|0.1|0.5|0.00000001|3"};hazard.Start();hazard.Tick(Start.AddSeconds(1));for(int i=2;i<=300;i++)hazard.Tick(Start.AddSeconds(i),i<60?2000:2002);Assert(hazard.Logs.Any(x=>x.Contains("T055_HAZARD")&&x.Contains("jump_test=1")),"T055 tests predictable variance-standardized jumps, not full-window future variance");
        return checks;
    }
}
