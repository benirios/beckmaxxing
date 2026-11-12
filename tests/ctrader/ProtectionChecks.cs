using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.Robots;
static class ProtectionChecks
{
    static int checks;
    static readonly DateTime At=new(2026,6,17,9,0,0,DateTimeKind.Utc);
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static XauUsdStopLimitProtection StopBot(XauUsdStopLimitProtection.Branch branch=XauUsdStopLimitProtection.Branch.NettedStopLimit)
    {
        var r=new XauUsdStopLimitProtection{Entries="2026-06-17T09:00:00Z|2026-06-17T10:00:00Z|1|5|1990",Variant=branch,BrokerSemanticsReference="SYNTHETIC_NETTING_ONLY",LimitRangePips=10,FallbackSeconds=10,EquityFloor=9000,InitialRiskCeiling=100,PositionLabel="T101_CHECK"};r.Account.AccountType=AccountType.Netted;return r;
    }
    static string Q(int seconds,double bid=2,double ask=3,int tradable=1)=>$"{At.AddSeconds(seconds):yyyy-MM-ddTHH:mm:ssZ}|{At.AddSeconds(seconds):yyyy-MM-ddTHH:mm:ssZ}|{bid}|{ask}|2002|{tradable}";
    static XauUsdQuotedOptionHedge OptionBot(XauUsdQuotedOptionHedge.Branch branch=XauUsdQuotedOptionHedge.Branch.Hedged)
        =>new(){Variant=branch,Opportunities="2026-06-17T09:00:00Z|2026-06-17T09:01:00Z|1|5|2|10|1|50|50|3|3|2026-06-17T08:00:00Z",OptionQuotes=Q(0)+";"+Q(60,5,6),OptionContract="SYNTHETIC_PUT",UnderlyingContract="SYNTHETIC_GC",ExerciseReference="SYNTHETIC_NO_EXPIRY_POSITION",CalibrationReference="SYNTHETIC_PRIOR_EQUAL_BUDGET",OptionRight=XauUsdQuotedOptionHedge.Right.Put,Strike=2000,ExpiryUtc="2026-06-18T17:00:00Z",Multiplier=10,InitialOptionCash=1000,Fee=1,MaximumAgeSeconds=5,CalibrationTolerance=0.01,EquityFloor=9000,PositionLabel="T100_CHECK"};
    public static int Run()
    {
        var hedged=StopBot();hedged.Account.AccountType=AccountType.Hedged;hedged.Start();Check(hedged.Stopped,"T101 rejects opening a hedge disguised as protective closure");
        var fill=StopBot();fill.Start();fill.Tick(At.AddSeconds(1));Check(fill.Positions.Count==1&&fill.PendingOrders.Count==1,"T101 initial native entry and offset pending");fill.Tick(At.AddSeconds(2),1990);Check(fill.Positions.Count==0&&fill.PendingOrders.Count==0&&fill.History.Trades.Sum(x=>x.VolumeInUnits)==5,"T101 triggered stop-limit nets original volume without opening opposite");Check(fill.Logs.Any(x=>x.Contains("T101_COMPLETE")),"T101 completion requires original actual close deals");
        var gap=StopBot();gap.Start();gap.Tick(At.AddSeconds(1));gap.Tick(At.AddSeconds(2),1980);Check(gap.Positions.Count==1&&gap.PendingOrders.Count==1,"T101 gap through limit remains unfilled open risk");gap.FailCancel=true;gap.Tick(At.AddSeconds(13),1970);Check(gap.Positions.Count==1&&gap.PendingOrders.Count==1,"T101 failed cancel blocks fallback reversal risk");gap.FailCancel=false;gap.FailClose=true;gap.Tick(At.AddSeconds(14),1970);Check(gap.Positions.Count==1&&gap.PendingOrders.Count==0,"T101 close rejection remains visible after acknowledged cancel");gap.FailClose=false;gap.Tick(At.AddSeconds(15),1960);Check(gap.Positions.Count==0&&gap.History.Trades.Single().ClosingPrice<1990,"T101 nonfill retained through worse actual fallback price");
        var ordinary=StopBot(XauUsdStopLimitProtection.Branch.OrdinaryStop);ordinary.Start();ordinary.Tick(At.AddSeconds(1));ordinary.Tick(At.AddSeconds(2),1980);Check(ordinary.History.Trades.Single().ClosingPrice==1979.9,"T101 ordinary stop records actual gap fill rather than trigger");
        var partial=StopBot();partial.StopLimitFillFraction=0.5;partial.Start();partial.Tick(At.AddSeconds(1));partial.Tick(At.AddSeconds(2),1990);Check(partial.Positions.Single().VolumeInUnits==2.5&&partial.PendingOrders.Single().VolumeInUnits==2.5,"T101 partial netting leaves matching pending remainder");partial.Tick(At.AddSeconds(13),1980);Check(partial.Positions.Count==0&&partial.History.Trades.Sum(x=>x.VolumeInUnits)==5,"T101 partial and fallback closing deals both included");
        var barrier=StopBot();barrier.EquityFloor=9950;barrier.Start();barrier.Tick(At.AddSeconds(1));barrier.Tick(At.AddSeconds(2),1980);Check(barrier.Positions.Count==0&&barrier.Logs.Any(x=>x.Contains("barrier_hit=True")),"T101 unfilled stop-limit cannot hide account barrier breach");
        var option=OptionBot();option.Start();Check(!option.Stopped,"T100 explicit quoted option contract and prior budgets accepted");option.Tick(At.AddSeconds(1));Check(option.Positions.Single().VolumeInUnits==5&&option.Logs.Any(x=>x.Contains("external_cash=969")),"T100 premium and fee debit external cash only");Check(option.Logs.Any(x=>x.Contains("external_liquidation_equity=988")),"T100 entry equity includes option spread and both fees immediately");Check(option.Account.Balance==10000,"T100 outside premium does not change native CFD balance");option.Tick(At.AddSeconds(60));Check(option.Positions.Count==0&&option.Logs.Any(x=>x.Contains("external_option_net=18")),"T100 quoted bid liquidation includes both option fees");
        var smaller=OptionBot(XauUsdQuotedOptionHedge.Branch.ReducedCfdControl);smaller.Start();smaller.Tick(At.AddSeconds(1));Check(smaller.Positions.Single().VolumeInUnits==2,"T100 smaller control uses prior calibrated units");smaller.Tick(At.AddSeconds(60));Check(smaller.Logs.Any(x=>x.Contains("external_option_net=0")),"T100 control retains identical external cash without imaginary hedge");
        var stale=OptionBot();stale.OptionQuotes=Q(0);stale.Start();stale.Tick(At.AddSeconds(1));stale.Tick(At.AddSeconds(60));Check(stale.Positions.Count==0&&!stale.Logs.Any(x=>x.Contains("T100_COMPLETE")),"T100 stale quote cannot fabricate option liquidation");stale.End();Check(stale.Logs.Any(x=>x.Contains("option_open=True")),"T100 unliquidated option is censored");
        var noBid=OptionBot();noBid.OptionQuotes=Q(0)+";"+Q(60,0,1);noBid.Start();noBid.Tick(At.AddSeconds(1));noBid.Tick(At.AddSeconds(60));Check(!noBid.Logs.Any(x=>x.Contains("T100_OPTION_QUOTED_SALE")),"T100 no-bid quote is not a zero-price executable sale");
        var late=OptionBot();late.Opportunities=late.Opportunities.Replace("08:00:00","09:00:00");late.Start();Check(late.Stopped,"T100 rejects calibration unavailable before decision");
        var unequal=OptionBot();unequal.Opportunities=unequal.Opportunities.Replace("|50|50|","|50|30|");unequal.Start();Check(unequal.Stopped,"T100 rejects unequal calibrated risk budgets");
        var currency=OptionBot();currency.Account.Asset.Name="EUR";currency.Start();Check(currency.Stopped,"T100 rejects silent currency conversion");
        var wrong=OptionBot();wrong.OptionRight=XauUsdQuotedOptionHedge.Right.Call;wrong.Start();Check(wrong.Stopped,"T100 rejects convex leg with wrong protective direction");
        var tail=OptionBot();tail.EquityFloor=9950;tail.OptionQuotes=Q(0)+";"+Q(2,50,51);tail.Start();tail.Tick(At.AddSeconds(1));tail.Tick(At.AddSeconds(2),1980);Check(tail.Positions.Count==0&&tail.Logs.Any(x=>x.Contains("cfd_barrier_hit=True")),"T100 profitable outside option cannot erase native account barrier failure");
        var future=OptionBot();future.OptionQuotes=Q(2);future.Start();future.Tick(At.AddSeconds(1));Check(future.Orders==0,"T100 future received option quote cannot enter earlier opportunity");
        var live=OptionBot();live.RunningMode=RunningMode.RealTime;live.Start();Check(live.Stopped,"T100 adapter is explicitly research only");
        return checks;
    }
}
