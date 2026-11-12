using cAlgo.API;
using cAlgo.Robots;
using System.Globalization;
static class PortfolioPolicyChecks
{
    static int checks;
    static DateTime U(string value)=>DateTime.Parse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
    static readonly DateTime Day=U("2026-06-17T00:00:00Z");
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static void Set(Robot r,string name,object value)=>r.GetType().GetProperty(name).SetValue(r,value);
    static T Rules<T>(string rows,string branch) where T:Robot,new()
    {
        var r=new T();Set(r,"Opportunities",rows);Set(r,"ValidationReference","SYNTHETIC_NOT_MARKET_VALIDATION");Set(r,"ResetZone","UTC");Set(r,"ResetClock","00:00");Set(r,"DailyAnchor",Enum.Parse(typeof(T).GetNestedType("AnchorRule"),"ResetBalance"));Set(r,"DailyAllowance",500.0);Set(r,"TotalEquityFloor",9000.0);Set(r,"PositionLabel","policy_checks");Set(r,"Variant",Enum.Parse(typeof(T).GetNestedType("Branch"),branch));return r;
    }
    static string Row(DateTime start,DateTime exit,string strategy="A",double side=1,double stop=10,double risk=50,string extra="")=>$"{start:yyyy-MM-ddTHH:mm:ssZ}|{exit:yyyy-MM-ddTHH:mm:ssZ}|{strategy}|{side.ToString(CultureInfo.InvariantCulture)}|{stop.ToString(CultureInfo.InvariantCulture)}|0|{risk.ToString(CultureInfo.InvariantCulture)}{extra}";
    public static int Run()
    {
        CheckLoss();CheckHysteresis();CheckGrowth();CheckCovariance();CheckReset();return checks;
    }
    static void CheckLoss()
    {
        var at=Day.AddHours(9);
        var rows=string.Join(';',new[]{Row(at,at.AddMinutes(10),extra:"|0|0"),Row(at.AddMinutes(11),at.AddMinutes(20),extra:"|0|0"),Row(at.AddMinutes(21),at.AddMinutes(30),extra:"|0|0"),Row(at.AddMinutes(31),at.AddMinutes(40),extra:"|0|0"),Row(at.AddDays(1),at.AddDays(1).AddMinutes(10),extra:"|0|0")});
        var r=Rules<XauUsdCumulativeLossInterruption>(rows,"TwoRInterruption");r.LossSessionZone="UTC";r.LossSessionClock="00:00";r.Start();Check(!r.Stopped,"T092 explicit loss/account session rules accepted");
        r.Tick(at.AddSeconds(1));r.Tick(at.AddMinutes(1),1989.9);r.Tick(at.AddMinutes(11).AddSeconds(1));r.Tick(at.AddMinutes(12),1989.9);Check(r.Orders==2&&r.Positions.Count==0,"T092 first two separate losing trades execute and realize");
        r.Tick(at.AddMinutes(21).AddSeconds(1));Check(r.Orders==2,"T092 actual cumulative losses beyond -2R block third entry");r.Tick(at.AddMinutes(30),2010);Check(r.Logs.Any(x=>x.Contains("T092_COUNTERFACTUAL_QUOTES")),"T092 skipped opportunity has explicitly modeled executable quote ledger");r.Tick(at.AddMinutes(31).AddSeconds(1));Check(r.Orders==2,"T092 profitable skipped shadow does not alter actual realized-loss halt");
        r.Tick(at.AddDays(1).AddSeconds(1));Check(r.Orders==3,"T092 new declared loss session resets the optional interruption");
        var partial=Rules<XauUsdCumulativeLossInterruption>(rows,"TwoRInterruption");partial.LossSessionZone="UTC";partial.LossSessionClock="00:00";partial.Start();partial.Tick(at.AddSeconds(1));partial.Tick(at.AddSeconds(2),1990.3);
        typeof(Robot).GetMethod("ClosePosition",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(Position),typeof(double)},null).Invoke(partial,new object[]{partial.Positions.Single(),4.0});
        partial.Tick(at.AddSeconds(3),1990.3);partial.Tick(at.AddMinutes(1),1985);partial.Tick(at.AddMinutes(11).AddSeconds(1));partial.Tick(at.AddMinutes(12),1989.9);partial.Tick(at.AddMinutes(21).AddSeconds(1));Check(partial.Orders==2,"T092 includes every partial realized closing deal exactly once, not only the final position callback");
        var missing=Rules<XauUsdCumulativeLossInterruption>(rows,"TwoRInterruption");missing.LossSessionZone="UTC";missing.LossSessionClock="00:00";missing.DailyAnchor=XauUsdCumulativeLossInterruption.AnchorRule.Unspecified;missing.Start();Check(missing.Stopped,"Portfolio policy cannot silently assume an account product");
        var random=Rules<XauUsdCumulativeLossInterruption>(rows,"RandomClockControl");random.LossSessionZone="UTC";random.LossSessionClock="00:00";random.RandomInterruptions="2026-06-17T08:59:00Z";random.Start();random.Tick(at.AddSeconds(1));Check(random.Orders==0,"T092 random interruption is prior-fixed, not conditioned on future losses");
    }
    static void CheckHysteresis()
    {
        var at=Day.AddHours(9);var exit=at.AddHours(1);var rows=string.Join(';',new[]{Row(at,exit,side:2,risk:100),Row(at.AddMinutes(1),exit,side:3,risk:100),Row(at.AddMinutes(2),exit,side:1,risk:100),Row(at.AddMinutes(3),exit,side:-2,risk:100)});
        var immediate=Rules<XauUsdPositionHysteresis>(rows,"ImmediateTarget");immediate.BandUnits=1;immediate.MaximumUnits=10;immediate.MaximumHoldingMinutes=60;immediate.CostCalibrationReference="SYNTHETIC_PRIOR_COST_FIT";immediate.Start();immediate.Tick(at.AddSeconds(1));immediate.Tick(at.AddMinutes(1).AddSeconds(1));Check(immediate.Orders==2&&immediate.Positions.Sum(p=>p.VolumeInUnits)==3,"T093 increases only target delta instead of full close/reopen");immediate.Tick(at.AddMinutes(2).AddSeconds(1));Check(immediate.Orders==2&&immediate.Positions.Sum(p=>p.VolumeInUnits)==1,"T093 reduces natively with partial close without opening replacement");
        var band=Rules<XauUsdPositionHysteresis>(rows,"NoTradeBand");band.BandUnits=1;band.MaximumUnits=10;band.MaximumHoldingMinutes=60;band.CostCalibrationReference="SYNTHETIC_PRIOR_COST_FIT";band.Start();band.Tick(at.AddSeconds(1));band.Tick(at.AddMinutes(1).AddSeconds(1));band.Tick(at.AddMinutes(2).AddSeconds(1));Check(band.Orders==1&&band.Positions.Sum(p=>p.VolumeInUnits)==2,"T093 no-trade band avoids weak update turnover");band.FailClose=true;band.Tick(at.AddMinutes(3).AddSeconds(1));Check(band.Orders==1&&band.Positions.All(p=>p.TradeType==TradeType.Buy),"T093 failed flip closure cannot open an opposite hedge");band.FailClose=false;band.Tick(exit);Check(band.Positions.Count==0,"T093 original/max holding deadline remains effective after failed update");band.End();Check(band.Logs.Any(x=>x.Contains("T093_TURNOVER,marketable_units=4")),"T093 turnover includes final timed exits exactly once");
    }
    static void CheckGrowth()
    {
        var p=new[]{0.6,0.4};var outcomes=new[]{1.0,-1.0};var fraction=XauUsdRiskConstrainedGrowth.SolveRiskFraction(p,outcomes,0.8,0.1,0.5);var lambda=Math.Log(0.1)/Math.Log(0.8);
        Check(fraction>0&&fraction<0.2,"T094 risk-constrained optimum is below unconstrained Kelly for these scenarios");Check(XauUsdRiskConstrainedGrowth.DrawdownMoment(p,outcomes,fraction,lambda)<=1+1e-10,"T094 solved allocation satisfies the actual drawdown moment constraint");Check(XauUsdRiskConstrainedGrowth.SolveRiskFraction(new[]{0.4,0.6},outcomes,0.8,0.1,0.5)==0,"T094 negative expected scenarios do not manufacture positive allocation");
        var gap=XauUsdRiskConstrainedGrowth.SolveRiskFraction(new[]{0.9,0.1},new[]{1.0,-3.0},0.8,0.1,0.8);Check(1-3*gap>0,"T094 gap-loss support remains inside positive wealth domain");
        bool bad=false;try{XauUsdRiskConstrainedGrowth.SolveRiskFraction(new[]{0.6,0.6},outcomes,0.8,0.1,0.5);}catch(ArgumentException){bad=true;}Check(bad,"T094 probabilities must genuinely sum to one");
        var at=Day.AddHours(9);var rows=Row(at,at.AddMinutes(10),risk:1000,extra:"|60|0.6:1,0.4:-1")+";"+Row(at.AddDays(1),at.AddDays(1).AddMinutes(10),risk:1000,extra:"|60|0.6:1,0.4:-1");
        var r=Rules<XauUsdRiskConstrainedGrowth>(rows,"FixedFraction");r.Alpha=0.8;r.Beta=0.1;r.MaximumFraction=0.5;r.FixedFraction=0.1;r.ReducedFraction=0.01;r.DailyAllowance=100;r.Start();r.Tick(at.AddSeconds(1));Check(r.Orders==1,"T094 unchanged opportunity executes with declared fixed baseline allocation");r.Tick(at.AddSeconds(2),1999);Check(r.Positions.Count==0&&r.Logs.Any(x=>x.Contains("T094_BREACH")),"T094 monitors intratrade equity, not just closing PnL");r.Tick(at.AddDays(1).AddSeconds(1));Check(r.Orders==1,"An actual equity-barrier failure cannot be erased by daily reset");
    }
    static void CheckCovariance()
    {
        var at=Day.AddHours(9);var rows=Row(at,at.AddHours(1),"A")+";"+Row(at,at.AddHours(1),"B")+";"+Row(at.AddMinutes(3),at.AddHours(1),"A");
        var r=Rules<XauUsdCovarianceRiskCap>(rows,"CovarianceAware");r.StandardDeviationBudget=75;r.GrossRiskCap=150;r.IndependentMultiplier=0.5;r.CovarianceSnapshots="2026-06-17T08:00:00Z|2026-06-17T07:59:00Z|A,B|1,1,1,1;2026-06-17T09:01:00Z|2026-06-17T09:00:59Z|A,B|4,4,4,4";r.Start();r.Tick(at.AddSeconds(1));Check(r.Orders==2&&r.Positions.Sum(p=>p.VolumeInUnits)<=7,"T095 aggregate covariance cap reduces second correlated allocation");var orders=r.Orders;r.Tick(at.AddMinutes(1).AddSeconds(1));Check(r.Orders==orders&&r.Positions.Sum(p=>p.VolumeInUnits)<=3,"T095 newly received covariance increase contracts existing exposure by partial closes");
        r.Positions.Add(new Position{Id=999,SymbolName="XAUUSD",Label="external_unprotected",TradeType=TradeType.Buy,VolumeInUnits=1,EntryPrice=2000});r.Tick(at.AddMinutes(2));Check(r.Positions.Count==1&&r.Positions[0].Id==999,"T095 unknown outside risk is not counted as zero or silently managed");r.Tick(at.AddMinutes(3).AddSeconds(1));Check(r.Orders==orders,"T095 new exposure remains blocked while outside account risk is unknown");
        var hedgedRows=Row(at,at.AddHours(1),"A")+";"+Row(at,at.AddHours(1),"B",side:-1);
        var opposing=Rules<XauUsdCovarianceRiskCap>(hedgedRows,"CovarianceAware");opposing.StandardDeviationBudget=75;opposing.GrossRiskCap=150;opposing.IndependentMultiplier=0.5;opposing.CovarianceSnapshots="2026-06-17T08:00:00Z|2026-06-17T07:59:00Z|A,B|1,-1,-1,1";opposing.Start();opposing.Tick(at.AddSeconds(1));Check(opposing.Positions.Sum(p=>p.VolumeInUnits)==10,"T095 signed strategy-PnL covariance does not count direction a second time");
        bool bad=false;try{XauUsdCovarianceRiskCap.ValidateCovariance(new double[,]{{1,2},{2,1}});}catch(ArgumentException){bad=true;}Check(bad,"T095 indefinite matrices are not accepted as covariance");
        var netted=Rules<XauUsdCovarianceRiskCap>(rows,"CovarianceAware");netted.StandardDeviationBudget=75;netted.GrossRiskCap=150;netted.IndependentMultiplier=0.5;netted.CovarianceSnapshots=r.CovarianceSnapshots;netted.Account.AccountType=cAlgo.API.Internals.AccountType.Netted;netted.Start();Check(netted.Stopped,"T095 cannot silently overwrite strategy holdings on a netted account");
    }
    static void CheckReset()
    {
        var at=Day.AddHours(23).AddMinutes(55);var reset=Day.AddDays(1);var rows=Row(at,reset.AddMinutes(10),stop:200,risk:1000);
        var r=Rules<XauUsdResetHeadroomPolicy>(rows,"HeadroomClosure");r.LeadSeconds=120;r.HeadroomBuffer=400;r.ChargesDeclared=true;r.LongResetCharge=1;r.Start();r.Tick(Day);r.Tick(at.AddSeconds(1));Check(r.Positions.Count==1,"T096 original frozen entry remains unchanged outside reset lead window");r.Tick(reset.AddMinutes(-1),1960);Check(r.Positions.Count==0&&r.Logs.Any(x=>x.Contains("T096_EARLY_CLOSE")),"T096 insufficient projected new-day headroom closes before reset");Check(Math.Abs(r.Account.Balance-9799)<1e-7,"T096 closure realizes the floating loss rather than pretending it disappears");r.Tick(reset.AddMilliseconds(500));Check(r.Logs.Any(x=>x.Contains("reference_balance=9799")),"T096 new reset-balance reference reflects actual pre-reset realization");
        var baseline=Rules<XauUsdResetHeadroomPolicy>(rows,"Baseline");baseline.LeadSeconds=120;baseline.HeadroomBuffer=400;baseline.ChargesDeclared=true;baseline.LongResetCharge=1;baseline.Start();baseline.Tick(Day);baseline.Tick(at.AddSeconds(1));baseline.Tick(reset.AddMinutes(-1),1960);Check(baseline.Positions.Count==1,"T096 baseline is not secretly given the closure treatment");
        var pragueAt=Day.AddHours(21).AddMinutes(55);var pragueRows=Row(pragueAt,Day.AddHours(22).AddMinutes(10),stop:200,risk:1000);
        var prague=Rules<XauUsdResetHeadroomPolicy>(pragueRows,"HeadroomClosure");prague.ResetZone="Europe/Prague";prague.LeadSeconds=120;prague.HeadroomBuffer=400;prague.ChargesDeclared=true;prague.Start();prague.Tick(pragueAt.AddSeconds(1));prague.Tick(Day.AddHours(21).AddMinutes(59),1960);Check(prague.Positions.Count==0&&prague.Logs.Any(x=>x.Contains("next_reset=2026-06-17T22:00:00")),"T096 summer CE(S)T reset uses historical IANA rules instead of UTC midnight");
        var undeclared=Rules<XauUsdResetHeadroomPolicy>(rows,"HeadroomClosure");undeclared.LeadSeconds=120;undeclared.HeadroomBuffer=400;undeclared.Start();Check(undeclared.Stopped,"T096 unknown fee/swap convention is not assumed zero");
    }
}
