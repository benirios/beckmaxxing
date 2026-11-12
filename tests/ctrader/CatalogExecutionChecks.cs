using cAlgo.API;
using cAlgo.Robots;
using System.Globalization;

static class CatalogExecutionChecks
{
    static int count;
    static readonly DateTime Start=DateTime.Parse("2026-06-17T15:00:00Z",CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal);
    static void Check(bool ok,string reason){count++;if(!ok)throw new Exception(reason);}
    static void Set(Robot bot,string field,object value)=>bot.GetType().GetProperty(field).SetValue(bot,value);
    static T Feature<T>(string features,int minutes=5,string variant="Primary") where T:Robot,new()
    {
        var bot=new T();Set(bot,"FeatureRows",$"{Start:yyyy-MM-ddTHH:mm:ssZ}|{Start.AddMinutes(minutes):yyyy-MM-ddTHH:mm:ssZ}|10|{features}");
        Set(bot,"RiskBudget",50.0);Set(bot,"PositionLabel","test_features");Set(bot,"Variant",Enum.Parse(typeof(T).GetNestedType("TestBranch"),variant));return bot;
    }
    static Robot Scheduled(Type type,string variant)
    {
        var bot=(Robot)Activator.CreateInstance(type);
        var property=type==typeof(XauUsdSplitExecution)?"Entries":"Opportunities";
        Set(bot,property,$"{Start:yyyy-MM-ddTHH:mm:ssZ}|{Start.AddMinutes(20):yyyy-MM-ddTHH:mm:ssZ}|+1|{(type==typeof(XauUsdSplitExecution)?5:1)}|1990|0");
        Set(bot,"RiskBudget",100.0);Set(bot,"PositionLabel","test_execution");Set(bot,"Variant",Enum.Parse(type.GetNestedType("Branch"),variant));bot.Start();return bot;
    }
    public static int Run()
    {
        var dollar=Feature<XauUsdDollarShockResidualShort>("0.001|0.002|0.001");dollar.Start();dollar.Tick(Start);
        Check(dollar.Orders==0,"T042 cannot enter on receipt quote");dollar.Tick(Start.AddSeconds(1));Check(dollar.Positions.Single().TradeType==TradeType.Sell,"T042 positive dollar shock shorts gold");
        dollar.FailClose=true;dollar.Tick(Start.AddMinutes(5));Check(dollar.Positions.Count==1,"T042 failed exit retains exposure");dollar.FailClose=false;dollar.Tick(Start.AddMinutes(5).AddSeconds(1));Check(dollar.Positions.Count==0,"T042 retries deadline exit");
        var negative=Feature<XauUsdDollarShockResidualShort>("-0.001|0.002|0.001");negative.Start();negative.Tick(Start.AddSeconds(1));Check(negative.Orders==0,"T042 does not invent symmetric negative-dollar trade");
        var stale=Feature<XauUsdDollarShockResidualShort>("0.001|0.002|0.001");stale.Start();stale.Tick(Start.AddSeconds(3));Check(stale.Orders==0,"Feature trader rejects stale quote");
        var depth=Feature<XauUsdStandingDepthImbalance>("200|100|0.1|0.2|0.001",1);depth.Start();depth.Tick(Start.AddSeconds(1));Check(depth.Positions.Single().TradeType==TradeType.Buy,"T044 thin ask predicts positive sign");
        var invalidDepth=Feature<XauUsdStandingDepthImbalance>("0|0|0.1|0.2|0.001",1);invalidDepth.Start();Check(invalidDepth.Stopped,"T044 rejects undefined depth ratio");
        var entropy=Feature<XauUsdEntropySignPersistence>("-0.001|0.2|0.3|0.01|0.2|0.01");entropy.Start();entropy.Tick(Start.AddSeconds(1));Check(entropy.Positions.Single().TradeType==TradeType.Sell,"T041 uses lag sign, not entropy as direction");Check(entropy.Logs.Any(x=>x.Contains("low_entropy=True")),"T041 records predetermined state");
        const string pullback="1990|2010|1980|2005|2000|1995|2010|0";
        var vwap=Feature<XauUsdVwapPullbackContinuation>(pullback,60);vwap.Start();vwap.Tick(Start.AddSeconds(1));Check(vwap.Orders==0,"T038 rejects EMA pullback without VWAP alignment");
        var control=Feature<XauUsdVwapPullbackContinuation>(pullback,60,"NoVwap");control.Start();control.Tick(Start.AddSeconds(1));Check(control.Orders==1,"T038 NoVwap control keeps EMA-only opportunity");
        var proxy=Feature<XauUsdVwapPullbackContinuation>(pullback[..^1]+"1",60);proxy.Start();Check(proxy.Stopped,"T038 cannot silently substitute tick-count volume");
        var ma=Feature<XauUsdSlowMovingAverage>("2|4",60);Set(ma,"MaxTradesPerDay",1);var m5=new Bars();
        for(int i=0;i<5;i++){m5.OpenTimes.Add(Start.AddMinutes(-20+i*5));m5.ClosePrices.Add(i==4?1:1900+i);}
        ma.MarketData.Series[TimeFrame.Minute5]=m5;ma.Start();ma.Tick(Start.AddSeconds(1));Check(ma.Positions.Single().TradeType==TradeType.Buy,"T037 excludes forming bar even when its close would reverse signal");
        var missing=Feature<XauUsdSlowMovingAverage>("2|4",60);Set(missing,"MaxTradesPerDay",1);var incomplete=new Bars();incomplete.OpenTimes.Add(Start.AddMinutes(-5));incomplete.ClosePrices.Add(2000);missing.MarketData.Series[TimeFrame.Minute5]=incomplete;missing.Start();missing.Tick(Start.AddSeconds(1));Check(missing.Orders==0,"T037 incomplete lookback never trades");
        var split=Scheduled(typeof(XauUsdSplitExecution),"FiveChildren");foreach(var second in new[]{1,8,15,23,30})split.Tick(Start.AddSeconds(second));Check(split.Orders==5,"T045 places five scheduled children including exact due time");Check(split.Positions.Sum(p=>p.VolumeInUnits)==5,"T045 child exposure equals fixed target");Check(split.Logs.Any(x=>x.Contains("unfilled_units=0")),"T045 logs complete basket");split.Tick(Start.AddMinutes(20));Check(split.Positions.Count==0,"T045 all children share original exit");
        var single=Scheduled(typeof(XauUsdSplitExecution),"SingleOrder");single.Tick(Start.AddSeconds(1));Check(single.Orders==1 && single.Positions.Single().VolumeInUnits==5,"T045 single control uses identical target units");
        var missed=Scheduled(typeof(XauUsdSplitExecution),"FiveChildren");missed.Tick(Start.AddSeconds(1));missed.Tick(Start.AddSeconds(31));Check(missed.Orders==2,"T045 missed schedule slots are not caught up");Check(missed.Logs.Any(x=>x.Contains("unfilled_units=3")),"T045 incomplete basket remains in opportunity ledger");
        var passive=Scheduled(typeof(XauUsdPassiveEntrySelection),"PassiveNoFallback");passive.Tick(Start.AddSeconds(1));Check(passive.LimitSubmissions==1 && passive.Orders==0,"T046 submits native limit instead of assuming immediate fill");passive.Tick(Start.AddSeconds(2),1999.8);Check(passive.Positions.Count==1 && passive.Positions.Single().EntryTime==Start.AddSeconds(2),"T046 records actual modeled native fill time");passive.Tick(Start.AddSeconds(7),1999.7);Check(passive.Logs.Any(x=>x.Contains("T046_MARKOUT")&&x.Contains("valid=True")),"T046 markout measured from fill, not request");passive.Tick(Start.AddMinutes(20));Check(passive.Positions.Count==0,"T046 passive fill preserves original exit");
        var unfilled=Scheduled(typeof(XauUsdPassiveEntrySelection),"PassiveNoFallback");unfilled.Tick(Start.AddSeconds(1));unfilled.Tick(Start.AddSeconds(30));Check(unfilled.PendingOrders.Count==0 && unfilled.Orders==0,"T046 native expiry has no market fallback");Check(unfilled.Logs.Any(x=>x.Contains("T046_UNFILLED")&&x.Contains("opportunity_pnl=0")),"T046 unfilled signal counts as an opportunity");
        CheckCandleExit();
        return count;
    }
    static XauUsdCandleConfirmedEmaExit Candle(string variant)
    {
        var r=new XauUsdCandleConfirmedEmaExit();Set(r,"Opportunities",$"{Start:yyyy-MM-ddTHH:mm:ssZ}|{Start.AddHours(1):yyyy-MM-ddTHH:mm:ssZ}|+1|1990|0|0|1");Set(r,"DollarRisk",50.0);Set(r,"PositionLabel","ema_exit");Set(r,"Variant",Enum.Parse(typeof(XauUsdCandleConfirmedEmaExit.Branch),variant));
        var bars=new Bars();for(int i=0;i<52;i++){bars.OpenTimes.Add(Start.AddMinutes(15*(i-51)));bars.ClosePrices.Add(2006);r.Indicators.EmaValues.Add(2005);}r.MarketData.Series[TimeFrame.Minute15]=bars;r.Start();return r;
    }
    static void CheckCandleExit()
    {
        var wick=Candle("Baseline");wick.Tick(Start.AddSeconds(1));wick.Tick(Start.AddSeconds(2),2001);Check(wick.Positions.Count==0,"T039 intrabar trail observes current threshold crossing");
        var close=Candle("Gated");close.Tick(Start.AddSeconds(1));close.Tick(Start.AddSeconds(2),2001);Check(close.Positions.Count==1 && close.Positions.Single().StopLoss==1990,"T039 waits for candle close while retaining hard stop");
        var bars=close.MarketData.Series[TimeFrame.Minute15];bars.ClosePrices[^1]=1999;bars.OpenTimes.Add(Start.AddMinutes(15));bars.ClosePrices.Add(2001);close.Indicators.EmaValues.Add(2005);close.Tick(Start.AddMinutes(15).AddSeconds(1),2001);Check(close.Positions.Count==0,"T039 executes only after confirmed M15 close across EMA");
    }
}
