using cAlgo.API;
using cAlgo.Robots;
using System.Globalization;
static class CatalogFlowChecks
{
    static int checks;
    static DateTime U(string text)=>DateTime.Parse(text,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
    static readonly DateTime Start=U("2026-06-17T14:00:00Z");
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static void Set(Robot r,string property,object value)=>r.GetType().GetProperty(property).SetValue(r,value);
    static Robot Feature(Type type,string values,int minutes,string branch="Primary",DateTime? at=null)
    {
        var now=at??Start;var r=(Robot)Activator.CreateInstance(type);
        if(type.GetProperty("RiskBudget")!=null)Set(r,"RiskBudget",50.0);
        Set(r,"PositionLabel","flow_test");Set(r,"Variant",Enum.Parse(type.GetNestedType("TestBranch"),branch));
        Set(r,"FeatureRows",$"{now:yyyy-MM-ddTHH:mm:ssZ}|{now.AddMinutes(minutes):yyyy-MM-ddTHH:mm:ssZ}|10|{values}");r.Start();return r;
    }
    static Robot Cost(Type type,string features,int state=1,string branch="Gated",DateTime? at=null,double stop=1990,double target=2020,string updates="")
    {
        var now=at??Start;var r=(Robot)Activator.CreateInstance(type);Set(r,"DollarRisk",50.0);Set(r,"PositionLabel","flow_cost");Set(r,"Variant",Enum.Parse(type.GetNestedType("Branch"),branch));
        if(type.GetProperty("FixedUnits")!=null)Set(r,"FixedUnits",1.0);
        if(type.GetProperty("StatusUpdates")!=null)Set(r,"StatusUpdates",updates);
        Set(r,"Opportunities",$"{now:yyyy-MM-ddTHH:mm:ssZ}|{now.AddMinutes(20):yyyy-MM-ddTHH:mm:ssZ}|+1|{stop}|{target}|{state}|1|{features}");r.Start();return r;
    }
    public static int Run()
    {
        var etfAt=Start.AddMinutes(15);
        var etf=Feature(typeof(XauUsdEtfPremiumDemand),"0.001|0.002|0.001|0.001|200|0",45,at:etfAt);etf.Tick(etfAt.AddSeconds(1));Check(etf.Positions.Single().TradeType==TradeType.Buy,"T059 requires sustained positive live-value premium");
        var nav=Feature(typeof(XauUsdEtfPremiumDemand),"0.001|0.002|0.001|0.001|200|1",45,at:etfAt);Check(nav.Stopped,"T059 rejects final NAV as intraday indicative value");
        var transient=Feature(typeof(XauUsdEtfPremiumDemand),"-0.001|0.002|0.001|0.001|200|0",45,at:etfAt);transient.Tick(etfAt.AddSeconds(1));Check(transient.Orders==0,"T059 positive final/mean premium does not replace sustained premium");
        var jan=U("2023-01-10T15:00:00Z");
        var reset=Feature(typeof(XauUsdAnnualIndexWeightReset),"0.2|0.1|10000|2000|0|0.001|0|1",30,at:jan);reset.Tick(jan.AddSeconds(1));Check(reset.Positions.Single().TradeType==TradeType.Buy,"T062 computes signed quantity from published target-minus-current weights");
        var placebo=Feature(typeof(XauUsdAnnualIndexWeightReset),"0.2|0.1|10000|2000|0|0.001|0|0",30,"PlaceboWindows",jan);placebo.Tick(jan.AddSeconds(1));Check(placebo.Orders==1,"T062 matching non-implementation windows are explicit placebo control");
        var disclosure=Feature(typeof(XauUsdReserveQuantitySurprise),"3|5|0.001|300",30);disclosure.Tick(Start.AddSeconds(1));Check(disclosure.Positions.Single().TradeType==TradeType.Sell,"T063 surprise uses reported quantity minus pre-release forecast");
        var early=Feature(typeof(XauUsdReserveQuantitySurprise),"3|5|0.001|299",30);Check(early.Stopped,"T063 never enters before five complete post-delivery minutes");
        var round=Feature(typeof(XauUsdPublishedAuctionImbalance),"100|40|0.1|0|0.001",1);round.Tick(Start.AddSeconds(1));Check(round.Positions.Single().TradeType==TradeType.Buy,"T064 normalizes completed bid-minus-offer imbalance");
        var final=Feature(typeof(XauUsdPublishedAuctionImbalance),"100|40|0.1|1|0.001",1);final.Tick(Start.AddSeconds(1));Check(final.Orders==0,"T064 excludes final auction rounds");
        var run=Feature(typeof(XauUsdPersistentTradeSignFlow),"0.8|100|10|0.6|0.001|0.2",5);run.Tick(Start.AddSeconds(1));var shuffled=Feature(typeof(XauUsdPersistentTradeSignFlow),"0.8|100|10|0.6|0.001|0.2",5,"RunShuffled");shuffled.Tick(Start.AddSeconds(1));Check(run.Orders==1 && shuffled.Orders==0,"T068 run control changes sequencing statistic, not net flow");
        var size=Feature(typeof(XauUsdRepeatedTradeSizeFlow),"0.8|0.5|-100|0.2|10|-0.001|0.3",5);size.Tick(Start.AddSeconds(1));Check(size.Positions.Single().TradeType==TradeType.Sell,"T069 uses clustering conditional on signed net volume");
        var refresh=Feature(typeof(XauUsdHiddenSupplyReplenishment),"1|100|2|0.001|0.002|0.001|-0.001|0.002|100",5);refresh.Tick(Start.AddSeconds(1));var flowOnly=Feature(typeof(XauUsdHiddenSupplyReplenishment),"1|100|2|0.001|0.002|0.001|-0.001|0.002|100",5,"FlowOnly");flowOnly.Tick(Start.AddSeconds(1));Check(refresh.Positions.Single().TradeType==TradeType.Sell && flowOnly.Positions.Single().TradeType==TradeType.Buy,"T070 adds a negative supply component without assuming all relative returns are outright negative");
        var fakeRefresh=Feature(typeof(XauUsdHiddenSupplyReplenishment),"0|100|2|0.001|0.002|0.001|-0.001|0.002|100",5);fakeRefresh.Tick(Start.AddSeconds(1));Check(fakeRefresh.Orders==0,"T070 requires confirmed native replenishment");
        var sweep=Feature(typeof(XauUsdAggressiveSweepReversal),"1|3|100|0.001|100|0.2",5);sweep.Tick(Start.AddSeconds(1));Check(sweep.Positions.Single().TradeType==TradeType.Sell,"T071 fades reconstructed multi-level sweep side");
        var oneLevel=Feature(typeof(XauUsdAggressiveSweepReversal),"1|1|100|0.001|100|0.2",5);oneLevel.Tick(Start.AddSeconds(1));Check(oneLevel.Orders==0,"T071 does not label single-level trade as multi-level sweep");
        var basisAt=U("2026-06-17T13:15:00Z");var basis=Cost(typeof(XauUsdDeliveryBasisExecutionGate),"2010|2000|5|0.001|0.001|100|900",at:basisAt);basis.Tick(basisAt.AddSeconds(1));Check(basis.Orders==0,"T060 carry-adjusted 09:00 snapshot identifies basis stress");var wrongAge=Cost(typeof(XauUsdDeliveryBasisExecutionGate),"2010|2000|5|0.001|0.001|100|0",at:basisAt);Check(wrongAge.Stopped,"T060 cannot replace 09:00 state with later snapshot");
        var buy=Cost(typeof(XauUsdLeaseRateExecutionAsymmetry),"0.05|0.03|0.001|0.002",branch:"BuyControl",stop:10,target:0);buy.Tick(Start.AddSeconds(1));var sell=Cost(typeof(XauUsdLeaseRateExecutionAsymmetry),"0.05|0.03|0.001|0.002",branch:"SellControl",stop:10,target:0);sell.Tick(Start.AddSeconds(1));Check(buy.Positions.Single().TradeType==TradeType.Buy && sell.Positions.Single().TradeType==TradeType.Sell,"T061 high lease state is a two-sided cost test, not bullish alpha");Check(buy.Positions.Single().VolumeInUnits==sell.Positions.Single().VolumeInUnits,"T061 matches actual order units across sides");
        var londonAt=U("2026-06-17T09:00:00Z");var holiday=Cost(typeof(XauUsdLondonHolidayExecutionGate),"1|1|1|0.001|0",at:londonAt);holiday.Tick(londonAt.AddSeconds(1));Check(holiday.Orders==0,"T073 excludes independently confirmed London participation absence");var auctionOnly=Cost(typeof(XauUsdLondonHolidayExecutionGate),"0|1|1|0.001|0",at:londonAt);Check(auctionOnly.Stopped,"T073 missing auction alone cannot establish closed desks");
        CheckAuction();CheckDealer();CheckLegacy();CheckCatchup();CheckFractionalReceipt();
        return checks;
    }
    static void CheckFractionalReceipt()
    {
        var r=new XauUsdPublishedAuctionImbalance{FeatureRows="2026-06-17T14:00:00.750Z|2026-06-17T14:01:00.750Z|10|100|40|0.1|0|0.001",RiskBudget=50,PositionLabel="fractional_receipt"};r.Start();
        Check(!r.Stopped,"Subsecond receipt parser retains original signal precision");
        r.Tick(Start.AddMilliseconds(500));Check(r.Orders==0,"Never floor actual feed receipt into an earlier second");
        r.Tick(Start.AddMilliseconds(750));Check(r.Orders==0,"Fractional receipt still requires a strictly later broker quote");
        r.Tick(Start.AddMilliseconds(751));Check(r.Orders==1,"First quote after exact fractional receipt enters");
        r.Tick(Start.AddMinutes(1).AddMilliseconds(500));Check(r.Positions.Count==1,"Fractional original exit is not rounded early");
        r.Tick(Start.AddMinutes(1).AddMilliseconds(750));Check(r.Positions.Count==0,"Fractional original deadline closes exactly");
    }
    static void CheckAuction()
    {
        var at=Start.AddMinutes(5);var updates="2026-06-17T14:06:00Z|2026-06-17T14:00:00Z|0";
        var r=Cost(typeof(XauUsdAuctionCompletionWait),"300|0|0.001|0.2",at:at,updates:updates);r.Tick(at.AddSeconds(1));r.Tick(at.AddSeconds(30));Check(r.Orders==0,"T065 cannot use future completion receipt");r.Tick(at.AddMinutes(1));Check(r.Orders==0,"T065 waits for broker quote strictly after status receipt");r.Tick(at.AddMinutes(1).AddSeconds(1));Check(r.Orders==1,"T065 completion receipt releases waiting opportunity");r.Tick(at.AddMinutes(20));Check(r.Positions.Count==0,"T065 retains original exit after waiting");
        var missing=Cost(typeof(XauUsdAuctionCompletionWait),"300|0|0.001|0.2",at:at);missing.Tick(at.AddSeconds(1));missing.Tick(at.AddMinutes(5).AddSeconds(1));Check(missing.Orders==0 && missing.Logs.Any(x=>x.Contains("gate_expired")),"T065 missing completion expires at fixed five-minute cap");
    }
    static void CheckDealer()
    {
        var quotes="2026-06-17T14:00:00Z|2026-06-17T14:00:00Z|1999.9|2000.1|1999.9|2000.1;2026-06-17T14:15:00Z|2026-06-17T14:15:00Z|1997.9|1998.1|1999.9|2000.1";
        var r=new XauUsdDealerInventoryQuoteSkew{InventoryEvents="2026-06-17T14:00:00Z|10|3|0.001|1",ReceivedQuotes=quotes,MaxSourceAgeSeconds=2};r.Start();r.Tick(Start.AddSeconds(1));r.Tick(Start.AddMinutes(5));Check(!r.Logs.Any(x=>x.Contains("T072_PRIMARY")),"T072 future provider quotes cannot create an early outcome");r.Tick(Start.AddMinutes(15));Check(r.Logs.Any(x=>x.Contains("T072_PRIMARY")&&x.Contains("dealer_relative_log_change=-")),"T072 observes dealer discount relative to actual composite");r.Tick(Start.AddMinutes(30),2001);Check(r.Logs.Any(x=>x.Contains("T072_FOLLOWUP")),"T072 separately measures subsequent broker response");Check(r.Orders==0,"T072 inventory quote skew alone does not invent broker alpha");
        var unauthorized=new XauUsdDealerInventoryQuoteSkew{InventoryEvents="2026-06-17T14:00:00Z|10|3|0.001|0",ReceivedQuotes=quotes,MaxSourceAgeSeconds=2};unauthorized.Start();Check(unauthorized.Stopped,"T072 requires actual authorized inventory data");
    }
    static void CheckLegacy()
    {
        var summer=U("2023-06-19T22:05:00Z");var r=new XauUsdOvernightDrift{CompleteDates="2023-06-19",PositionLabel="overnight"};r.Start();r.Tick(summer);Check(r.Positions.Count==1 && r.Positions.Single().VolumeInUnits==1,"T076 exact preknown 18:05 NY open and one ounce");Check(Math.Abs(r.Positions.Single().StopLoss.Value-r.Positions.Single().EntryPrice*0.99)<1e-8,"T076 preserves 1 percent native entry protection");r.Tick(U("2023-06-20T12:20:00Z"));Check(r.Positions.Count==0 && r.Logs.Any(x=>x.Contains("REFERENCE_COST_LEDGER")),"T076 exact next-day 08:20 NY exit with separate source fees");
        var winter=new XauUsdOvernightDrift{CompleteDates="2023-12-18",PositionLabel="winter"};winter.Start();winter.Tick(U("2023-12-18T22:05:00Z"));Check(winter.Orders==0,"T076 never hard-codes summer UTC open into winter");winter.Tick(U("2023-12-18T23:05:00Z"));Check(winter.Orders==1,"T076 historical local DST maps winter open correctly");
        var confirmation=new XauUsdOvernightDrift{CompleteDates="2024-06-17"};confirmation.Start();Check(confirmation.Stopped,"T076 failed development does not reopen confirmation");
        var night=U("2023-06-20T00:00:00Z");var asia=Feature(typeof(XauUsdAsiaReplenishment),"-0.01|92|20|72",360,at:night);asia.Tick(night.AddSeconds(1));Check(asia.Positions.Single().VolumeInUnits==1 && asia.Positions.Single().TradeType==TradeType.Buy,"T077 locked negative-US conditional long uses one ounce");var nonnegative=Feature(typeof(XauUsdAsiaReplenishment),"0.01|92|20|72",360,at:night);nonnegative.Tick(night.AddSeconds(1));Check(nonnegative.Orders==0,"T077 skips nonnegative US session");var unconditional=Feature(typeof(XauUsdAsiaReplenishment),"0.01|92|20|72",360,"UnconditionalLong",night);unconditional.Tick(night.AddSeconds(1));Check(unconditional.Orders==1,"T077 unconditional control retains same complete-date universe");
    }
    static void CheckCatchup()
    {
        var r=new XauUsdFuturesSpotCatchupDiagnostic{Observations="2026-06-17T14:00:00Z|0.001|0|0.05|0.01|60",ContractIdentifier="GC_TEST"};r.Start();r.Tick(Start.AddSeconds(1));for(int i=2;i<=300;i++)r.Tick(Start.AddSeconds(i));Check(r.Orders==0 && r.Logs.Any(x=>x.Contains("T078_CONVERGENCE")),"T078 excluded account-path hypothesis remains observation-only");
        var line=r.Logs.Single(x=>x.Contains("T078_CONVERGENCE"));var amount=line.Split(',').Single(x=>x.StartsWith("hypothetical_unit_price_pl_before_fees=")).Split('=')[1];Check(Math.Abs(double.Parse(amount,CultureInfo.InvariantCulture)+0.2)<1e-8,"T078 hypothetical ledger charges actual observed bid/ask, not midpoint free profit");
    }
}
