using cAlgo.API;
using cAlgo.Robots;
using System.Globalization;
static class CatalogFinalSignalChecks
{
    static int count;
    static DateTime U(string s)=>DateTime.Parse(s,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
    static readonly DateTime Start=U("2026-06-17T14:00:00Z");
    static void Check(bool ok,string reason){count++;if(!ok)throw new Exception(reason);}
    static void Set(Robot r,string name,object value)=>r.GetType().GetProperty(name).SetValue(r,value);
    static Robot Feature(Type type,string values,int minutes,string branch="Primary",DateTime? at=null,Action<Robot> configure=null)
    {
        var clock=at??Start;var r=(Robot)Activator.CreateInstance(type);Set(r,"RiskBudget",50.0);Set(r,"PositionLabel","final_signal");Set(r,"Variant",Enum.Parse(type.GetNestedType("TestBranch"),branch));Set(r,"FeatureRows",$"{clock:yyyy-MM-ddTHH:mm:ssZ}|{clock.AddMinutes(minutes):yyyy-MM-ddTHH:mm:ssZ}|10|{values}");configure?.Invoke(r);r.Start();return r;
    }
    public static int Run()
    {
        var band=Feature(typeof(XauUsdVwapBandReversion),"2025|2000|10|0|2010|10",30);band.Tick(Start.AddSeconds(1),2025);Check(band.Positions.Single().TradeType==TradeType.Sell,"T080 fades 2-to-3 sigma upper band");Check(Math.Abs(band.Positions.Single().TakeProfit.Value-2000)<1e-8,"T080 native profit target is entry-known VWAP");
        var near=Feature(typeof(XauUsdVwapBandReversion),"2019|2000|10|0|2010|10",30);near.Tick(Start.AddSeconds(1),2019);Check(near.Orders==0,"T080 excludes less-than-two-sigma deviation");var proxy=Feature(typeof(XauUsdVwapBandReversion),"2025|2000|10|1|2010|10",30);Check(proxy.Stopped,"T080 no silent tick-volume substitution");
        var stop=Feature(typeof(XauUsdMeasuredStopCascade),"1|100|50|10|100|1999|1|0.001|0|100",5);stop.Tick(Start.AddSeconds(1));Check(stop.Positions.Single().TradeType==TradeType.Buy,"T081 measured crossed stop inventory predicts same-side continuation");var proxyStop=Feature(typeof(XauUsdMeasuredStopCascade),"1|100|50|10|100|1999|0|0.001|0|100",5);Check(proxyStop.Stopped,"T081 public price levels cannot stand in for authorized stop inventory");
        var cojump=Feature(typeof(XauUsdCommonInformationCojump),"0.001|1|1|0|0|-300|0|100|0.1",5);cojump.Tick(Start.AddSeconds(1));Check(cojump.Orders==1,"T083 same completed interval confirmations qualify");var ownOnly=Feature(typeof(XauUsdCommonInformationCojump),"0.001|0|0|0|0|-300|0|100|0.1",5,"OwnJumpOnly");ownOnly.Tick(Start.AddSeconds(1));Check(ownOnly.Orders==1,"T083 own-jump-only control omits external confirmation");var future=Feature(typeof(XauUsdCommonInformationCojump),"0.001|1|1|1|1|300|0|100|0.1",5);Check(future.Stopped,"T083 shifted control cannot use a future interval");
        var gcAt=U("2026-06-17T12:20:00Z");
        var carry=Feature(typeof(XauUsdCarryCurvePremium),"2000|2010|0.1|0.2|-1|0|0.1|0.01|0.03|0.001|0|0|1",310,at:gcAt,configure:r=>{Set(r,"NearContract","NEAR_TEST");Set(r,"FarContract","FAR_TEST");});carry.Tick(gcAt.AddSeconds(1));Check(carry.Positions.Single().TradeType==TradeType.Sell,"T084 explicit sign convention prevents automatic contango-long assumption");
        var producer=Feature(typeof(XauUsdProducerHedgingPremium),"1000|86400|1|-0.002|0.00001|0.01|1|0.001|0.01",310,at:gcAt);producer.Tick(gcAt.AddSeconds(1));var omitted=Feature(typeof(XauUsdProducerHedgingPremium),"1000|86400|1|-0.002|0.00001|0.01|1|0.001|0.01",310,"PositionOmitted",gcAt);omitted.Tick(gcAt.AddSeconds(1));Check(producer.Positions.Single().TradeType==TradeType.Buy && omitted.Positions.Single().TradeType==TradeType.Sell,"T085 producer component is incremental to identical baseline controls");var unreleased=Feature(typeof(XauUsdProducerHedgingPremium),"1000|0|0|-0.002|0.00001|0.01|1|0.001|0.01",310,at:gcAt);Check(unreleased.Stopped,"T085 Tuesday position timestamp is not public availability");
        var monthAt=U("2026-06-30T18:00:00Z");var month=Feature(typeof(XauUsdMonthEndPortfolioDemand),"0.1|0.6|0.3|0|0.1|0|1|0|0",120,at:monthAt);month.Tick(monthAt.AddSeconds(1));Check(month.Positions.Single().TradeType==TradeType.Buy,"T086 equity outperformance induces target-weight gold purchase");
        var gains=Feature(typeof(XauUsdHolderGainsNewsDrift),"0.5|-0.002|1|0.001|0.01|1|86400|300",30);gains.Tick(Start.AddSeconds(1));Check(gains.Positions.Single().TradeType==TradeType.Buy,"T087 holder-gains component can overcome negative base forecast");var fakeGains=Feature(typeof(XauUsdHolderGainsNewsDrift),"0.5|-0.002|1|0.001|0.01|0|86400|300",30);Check(fakeGains.Stopped,"T087 recent-high proxy does not establish holder cost basis");
        var premium=Feature(typeof(XauUsdUpsideVariancePremium),"0.02|0.01|0.001|-0.5|30|30|20|0.1|0.01|0.001",120);premium.Tick(Start.AddSeconds(1));Check(premium.Positions.Single().TradeType==TradeType.Sell,"T090 prior-trained negative premium coefficient is allowed");var wrongHorizon=Feature(typeof(XauUsdUpsideVariancePremium),"0.02|0.01|0.001|-0.5|30|7|20|0.1|0.01|0.001",120);Check(wrongHorizon.Stopped,"T090 implied and physical variances must share a horizon");
        var oi=Feature(typeof(XauUsdOpenInterestContraction),"0.01|-100|86400|1|0.001|0.00002|0.01|1000|0|1",60,at:gcAt);oi.Tick(gcAt.AddSeconds(1));Check(oi.Positions.Single().TradeType==TradeType.Sell,"T098 contraction adds a negative component without claiming direct short-cover inventory");
        CheckFomc();CheckBounce();CheckClock();CheckQuoteAge();
        return count;
    }
    static void CheckFomc()
    {
        var at=U("2026-06-17T16:00:00Z");var r=Feature(typeof(XauUsdPreFomcDrift),"1|840|0",115,at:at,configure:r=>Set(r,"ReleaseReceiptsUtc","2026-06-17T17:00:00Z"));r.Tick(at.AddSeconds(1));Check(r.Orders==1,"T091 scheduled noon entry does not look ahead at unexpected actual early release");r.Tick(at.AddMinutes(30));Check(r.Positions.Count==1,"T091 future publication receipt remains unavailable");r.Tick(at.AddHours(1).AddSeconds(1));Check(r.Positions.Count==0 && r.Logs.Any(x=>x.Contains("T091_CONTAMINATED")),"T091 known early publication closes and marks contaminated fixed window");
        var shifted=Feature(typeof(XauUsdPreFomcDrift),"1|841|0",115,at:at,configure:r=>Set(r,"ReleaseReceiptsUtc","2026-06-17T18:01:00Z"));shifted.Tick(at.AddSeconds(1));Check(shifted.Orders==0,"T091 shifted schedule skipped without clock tuning");
    }
    static void CheckBounce()
    {
        const string rows="2026-06-17T14:00:00Z|2026-06-17T14:00:00Z|1|2000|2000|1|1|0.1|0.01;2026-06-17T14:00:01Z|2026-06-17T14:00:01Z|2|2000.2|2000|1|1|0.1|0.01;2026-06-17T14:00:02Z|2026-06-17T14:00:02Z|3|2000|2000|-1|1|0.1|0.01";
        var r=new XauUsdBidAskBounceNull{TradeRows=rows,ContractIdentifier="GC_TEST"};r.Start();r.Tick(Start.AddMilliseconds(500));r.Tick(Start.AddMilliseconds(1500));r.Tick(Start.AddMilliseconds(1600));r.Tick(Start.AddMilliseconds(2500));Check(r.Orders==0,"T088 execution null never creates a live fade strategy");Check(r.Logs.Any(x=>x.Contains("mid_cross_product=0")),"T088 transaction-price bounce can coexist with zero midpoint reversal");var result=r.Logs.First(x=>x.Contains("T088_HYPOTHETICAL_NEXT_QUOTE"));var value=result.Split(',').Single(x=>x.StartsWith("net_per_oz=")).Split('=')[1];Check(Math.Abs(double.Parse(value,CultureInfo.InvariantCulture)+0.32)<1e-8,"T088 next quote charges observed spread, commission and declared slippage");
    }
    static void CheckClock()
    {
        var at=U("2026-06-17T14:00:00Z");var past=new List<string>();
        for(int i=1;i<=20;i++)
        {
            var date=at.Date.AddDays(-i);past.Add($"{date.AddHours(14).AddMinutes(30):yyyy-MM-ddTHH:mm:ssZ}|{date:yyyy-MM-dd}|NY10|0.002");
        }
        // Current-session and future-session huge returns must never enter the predictor.
        past.Add("2026-06-17T14:30:00Z|2026-06-17|NY10|-0.9");past.Add("2026-06-18T14:30:00Z|2026-06-18|NY10|-0.9");
        var r=new XauUsdSameClockPersistence{BlockGrid="NY10|America/New_York|10:00",PastBlockReturns=string.Join(';',past),FeatureRows="2026-06-17T14:00:00Z|2026-06-17T14:30:00Z|10|NY10|0|0|0|0|-0.002|-0.002|1",RiskBudget=50,PositionLabel="same_clock"};r.Start();r.Tick(at.AddSeconds(1));Check(r.Orders==1 && r.Positions.Single().TradeType==TradeType.Buy,"T097 computes mean from exactly prior twenty sessions, excluding current/future returns");Check(r.Logs.Any(x=>x.Contains("prior20_mean=0.002")),"T097 logs actual rolling predictor rather than imported sign");
        var shortHistory=new XauUsdSameClockPersistence{BlockGrid="NY10|America/New_York|10:00",PastBlockReturns=string.Join(';',past.Take(19)),FeatureRows=r.FeatureRows,RiskBudget=50,PositionLabel="warmup"};shortHistory.Start();shortHistory.Tick(at.AddSeconds(1));Check(shortHistory.Orders==0 && shortHistory.Logs.Any(x=>x.Contains("T097_WARMUP")),"T097 nineteen prior sessions are not twenty");
    }
    static void CheckQuoteAge()
    {
        var at=Start;var r=new XauUsdQuoteAgeExecutionGate{DollarRisk=50,PositionLabel="age",Opportunities="2026-06-17T14:00:00Z|2026-06-17T14:20:00Z|1|1990|2020|1|1|2026-06-17T13:59:58Z|2026-06-17T13:59:59Z|1|0.001|1",Variant=XauUsdQuoteAgeExecutionGate.Branch.Gated};r.Start();r.Tick(at.AddSeconds(1));Check(r.Orders==0,"T099 old source quote triggers frozen age gate, not stale-price trading");
        var future=new XauUsdQuoteAgeExecutionGate{DollarRisk=50,PositionLabel="age_bad",Opportunities="2026-06-17T14:00:00Z|2026-06-17T14:20:00Z|1|1990|2020|0|1|2026-06-17T14:00:01Z|2026-06-17T14:00:02Z|1|0.001|1"};future.Start();Check(future.Stopped,"T099 quote cannot arrive after the stated decision");
    }
}
