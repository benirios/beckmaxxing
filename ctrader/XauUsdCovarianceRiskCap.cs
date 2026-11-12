using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
namespace cAlgo.Robots
{
    /// <summary>
    /// T095: Aggregate concurrent gold signal risk using a prior-only PSD covariance matrix, independent budgets and simple gross-risk controls. Covariance is of EACH STRATEGY signed net PnL normalized by its initial R; weights are POSITIVE account-currency R allocations, never signed again. Unknown outside account positions block new exposure and cause managed exposure to flatten, rather than count as zero risk. Actual hedged account semantics are required. Deterministic receipt/strategy ordering is fixed before results; exposure matching is evaluated from logged paths, not claimed by parameter names.
    /// Frozen rows: receiptUTC|exitUTC|strategyId|side(+1/-1)|stopDistanceUSD|targetDistanceUSD(0=none)|baseRiskAccountCurrency.
    /// All estimates must be available at receipt. Backtest only, no live deployment.
    /// Select the actual account reset rule/zone/clock and currency barriers explicitly;
    /// no account product or loss percentage is inferred. Initial account must be empty.
    /// External validation reference is provenance supplied by the researcher, not proof
    /// that this project has discovered a positive signal. All tick equity paths are logged.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdCovarianceRiskCap : Robot
    {
        public enum Branch { CovarianceAware, IndependentBudgets, GrossCapControl, PriorScaledIndependent }
        public enum AnchorRule { Unspecified, ResetBalance, InitialBalance, HigherOfInitialAndReset }
        [Parameter("Branch",DefaultValue=Branch.CovarianceAware)] public Branch Variant {get;set;}
        [Parameter("Frozen signal opportunities",DefaultValue="")] public string Opportunities {get;set;}
        [Parameter("Prior validated signal reference",DefaultValue="")] public string ValidationReference {get;set;}
        [Parameter("Actual account reset IANA zone",DefaultValue="")] public string ResetZone {get;set;}
        [Parameter("Actual reset local clock HH:mm",DefaultValue="")] public string ResetClock {get;set;}
        [Parameter("Actual daily reference rule",DefaultValue=AnchorRule.Unspecified)] public AnchorRule DailyAnchor {get;set;}
        [Parameter("Daily allowance (account currency)",DefaultValue=0.0,MinValue=0.0)] public double DailyAllowance {get;set;}
        [Parameter("Actual total equity floor (account currency)",DefaultValue=0.0,MinValue=0.0)] public double TotalEquityFloor {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T095")] public string PositionLabel {get;set;}
        [Parameter("Covariance snapshots: availableUTC|fitEndUTC|idsCSV|rowMajorCovarianceCSV",DefaultValue="")] public string CovarianceSnapshots {get;set;}
        [Parameter("Covariance std budget (account currency)",DefaultValue=0.0)] public double StandardDeviationBudget {get;set;}
        [Parameter("Gross initial-R currency cap",DefaultValue=0.0)] public double GrossRiskCap {get;set;}
        [Parameter("Prior-calibrated independent exposure multiplier",DefaultValue=0.0)] public double IndependentMultiplier {get;set;}
        private class Covariance { public DateTime Available; public string[] Ids; public double[,] Matrix; }
        private readonly List<Covariance> _covariances=new List<Covariance>();
        private class Opportunity { public DateTime Receipt,Exit; public string Strategy; public int Side; public double Stop,Target,Risk;  }
        private class Holding { public Opportunity Event; public DateTime Exit; public double InitialR; }
        private readonly List<Opportunity> _events=new List<Opportunity>();
        private readonly Dictionary<int,Holding> _holdings=new Dictionary<int,Holding>();
        private int _next;
        private string _label;
        private TimeZoneInfo _zone;
        private TimeSpan _resetClock;
        private DateTime _resetKey=DateTime.MinValue,_previousTime;
        private double _initialBalance,_resetBalance,_previousBalance,_peakEquity;
        private bool _barrierHit,_everBarrierHit;
        private int _dailyBreaches;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T095_STOP,reason=research_only");Stop();return;}
            try
            {
                if(string.IsNullOrWhiteSpace(ValidationReference)||DailyAnchor==AnchorRule.Unspecified||!Finite(DailyAllowance)||DailyAllowance<=0||!Finite(TotalEquityFloor)||TotalEquityFloor<=0||Positions.Count!=0||PendingOrders.Count!=0)throw new FormatException("Prior signal reference, explicit account rules/barriers and initially empty account required");
                _zone=TimeZoneInfo.FindSystemTimeZoneById(ResetZone);
                _resetClock=TimeSpan.ParseExact(ResetClock,"hh\\:mm",CultureInfo.InvariantCulture);
                if(Account.AccountType!=AccountType.Hedged||!Finite(StandardDeviationBudget)||!Finite(GrossRiskCap)||!Finite(IndependentMultiplier)||StandardDeviationBudget<=0||GrossRiskCap<=0||IndependentMultiplier<=0||IndependentMultiplier>1)throw new FormatException("Hedged semantics and frozen positive covariance/gross budgets plus scaled control required");
                var seenCov=new HashSet<DateTime>();
                foreach(var row in (CovarianceSnapshots??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=4)throw new FormatException("Four covariance columns required");
                    var available=Utc(c[0]);var fitEnd=Utc(c[1]);var ids=c[2].Split(',').Select(s=>s.Trim()).ToArray();var flat=c[3].Split(',');
                    if(!seenCov.Add(available)||fitEnd>=available||ids.Length==0||ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct().Count()!=ids.Length||flat.Length!=ids.Length*ids.Length)throw new FormatException("Prior-only fit and complete unique strategy covariance required");
                    var matrix=new double[ids.Length,ids.Length];for(int i=0;i<ids.Length;i++)for(int j=0;j<ids.Length;j++)matrix[i,j]=Number(flat[i*ids.Length+j]);
                    ValidateCovariance(matrix);_covariances.Add(new Covariance{Available=available,Ids=ids,Matrix=matrix});
                }
                if(_covariances.Count==0)throw new FormatException("Received prior-trained covariance snapshots required");
                _covariances.Sort((a,b)=>a.Available.CompareTo(b.Available));
                var seen=new HashSet<string>();
                foreach(var row in (Opportunities??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=7)throw new FormatException("Expected 7 columns per frozen signal");
                    var side=Number(c[3]);var ev=new Opportunity{Receipt=Utc(c[0]),Exit=Utc(c[1]),Strategy=c[2].Trim(),Side=(int)side,Stop=Number(c[4]),Target=Number(c[5]),Risk=Number(c[6])};
                    if(ev.Strategy.Length==0||!seen.Add(ev.Receipt.Ticks+"_"+ev.Strategy)||ev.Exit<=ev.Receipt||(side!=1&&side!=-1)||ev.Stop<=0||ev.Target<0||ev.Risk<=0)throw new FormatException("Invalid frozen signal");
                    
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Frozen positive-signal opportunities required");
                _events.Sort((a,b)=>a.Receipt==b.Receipt?string.CompareOrdinal(a.Strategy,b.Strategy):a.Receipt.CompareTo(b.Receipt));
                
            }
            catch(Exception e){Print("T095_STOP,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            _initialBalance=Account.Balance;_previousBalance=Account.Balance;_peakEquity=Account.Equity;
            Positions.Closed+=Closed;
            Print("T095_START,branch={0},events={1},validation_reference={2},daily_anchor={3},reset_zone={4},reset_clock={5},daily_allowance={6},total_floor={7}",Variant,_events.Count,ValidationReference,DailyAnchor,ResetZone,ResetClock,DailyAllowance,TotalEquityFloor);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            RefreshReset(now);
            
            _peakEquity=Math.Max(_peakEquity,Account.Equity);
            if(!_barrierHit && (Account.Equity<=DailyFloor()||Account.Equity<=TotalEquityFloor))
            {
                _barrierHit=true;_everBarrierHit=true;_dailyBreaches++;
                Print("T095_BREACH,quote={0:O},equity={1},daily_floor={2},total_floor={3}",now,Account.Equity,DailyFloor(),TotalEquityFloor);
            }
            if(_everBarrierHit)foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())_holdings[p.Id].Exit=now;
            if(_holdings.Count>0)
            {
                var unknown=Positions.Any(p=>!_holdings.ContainsKey(p.Id))||PendingOrders.Count>0;
                var covariance=FindCovariance(now);
                if(unknown||(Variant==Branch.CovarianceAware&&(covariance==null||_holdings.Values.Any(h=>Array.IndexOf(covariance.Ids,h.Event.Strategy)<0))))
                {
                    Print("T095_UNKNOWN_ACCOUNT_RISK,quote={0:O},outside_positions={1},flatten_managed=1",now,unknown);
                    foreach(var h in _holdings.Values)h.Exit=now;
                }
                else if(Variant==Branch.CovarianceAware||Variant==Branch.GrossCapControl)
                {
                    var gross=_holdings.Values.Sum(h=>h.InitialR);
                    var std=Variant==Branch.CovarianceAware?PortfolioStd(covariance,null,0):0;
                    var factor=Math.Min(1.0,gross>0?GrossRiskCap/gross:1.0);
                    if(std>0)factor=Math.Min(factor,StandardDeviationBudget/std);
                    if(factor<1-1e-10)
                        foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())
                        {
                            var old=p.VolumeInUnits;var desired=Symbol.NormalizeVolumeInUnits(old*factor,RoundingMode.Down);
                            if(desired<Symbol.VolumeInUnitsMin){_holdings[p.Id].Exit=now;continue;}
                            if(desired>=old)continue;
                            var result=ClosePosition(p,old-desired);
                            if(result.IsSuccessful&&_holdings.ContainsKey(p.Id))_holdings[p.Id].InitialR*=p.VolumeInUnits/old;
                            Print("T095_PARTIAL_REDUCTION,id={0},old_units={1},desired_units={2},success={3},error={4}",p.Id,old,desired,result.IsSuccessful,result.Error);
                        }
                }
            }
            foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())
                if(now>=_holdings[p.Id].Exit)
                {
                    var deadline=_holdings[p.Id].Exit;var close=ClosePosition(p);Print("T095_EXIT,id={0},quote={1:O},success={2},error={3},deadline={4:O},lateness_seconds={5}",p.Id,now,close.IsSuccessful,close.Error,deadline,(now-deadline).TotalSeconds);
                }
            if(ValidQuote())
            {
                
                while(_next<_events.Count && _events[_next].Receipt<now)
                {
                    var ev=_events[_next++];
                    if(now>=ev.Exit){Skip(ev,"original_exit_already_reached");continue;}
                    if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"missing_timely_quote");continue;}
                    
                    if(_everBarrierHit){Skip(ev,"account_barrier");continue;}
                    if(Positions.Any(p=>!_holdings.ContainsKey(p.Id))||PendingOrders.Count>0){Skip(ev,"unknown_external_account_risk");continue;}
                    if(_holdings.Values.Any(h=>h.Event.Strategy==ev.Strategy)){Skip(ev,"overlapping_signal");continue;}
                    if(!Eligible(ev,now)){Skip(ev,"policy_interruption");continue;}
                    var risk=Allocate(ev,now);
                    if(!Finite(risk)||risk<=0){Skip(ev,"zero_policy_allocation");continue;}
                    var stopPips=ev.Stop/Symbol.PipSize;
                    if(ev.Stop<=Symbol.Ask-Symbol.Bid){Skip(ev,"stop_inside_spread");continue;}
                    var volume=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(risk,stopPips,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                    if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"volume_below_minimum");continue;}
                    var result=ExecuteMarketOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label+"_"+_next,stopPips,ev.Target>0?(double?)(ev.Target/Symbol.PipSize):null,"T095 frozen signal "+ev.Strategy);
                    if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                    var stopVerified=result.Position.StopLoss.HasValue&&ev.Side*(result.Position.EntryPrice-result.Position.StopLoss.Value)>0;
                    var measuredPips=stopVerified?Math.Abs(result.Position.EntryPrice-result.Position.StopLoss.Value)/Symbol.PipSize:stopPips;
                    var initialR=Symbol.AmountRisked(result.Position.VolumeInUnits,measuredPips);
                    if(!stopVerified)Print("T095_UNCERTIFIED_PROTECTION,id={0},planned_R_used_for_attempt_costs=1,close_at_next_quote=1",result.Position.Id);
                    _holdings[result.Position.Id]=new Holding{Event=ev,Exit=stopVerified?ev.Exit:now,InitialR=initialR};
                    Print("T095_ENTRY,receipt={0:O},id={1},strategy={2},side={3},units={4},allocated_risk={5},initial_actual_R={6},exit={7:O}",ev.Receipt,result.Position.Id,ev.Strategy,ev.Side,volume,risk,initialR,ev.Exit);
                }
            }
            LogEquity(now);
            _previousBalance=Account.Balance;_previousTime=now;
        }
        private bool Eligible(Opportunity ev,DateTime now) { var cov=FindCovariance(ev.Receipt);return cov!=null&&Array.IndexOf(cov.Ids,ev.Strategy)>=0&&_holdings.Values.All(h=>Array.IndexOf(cov.Ids,h.Event.Strategy)>=0); }
        private double Allocate(Opportunity ev,DateTime now) { if(Variant==Branch.IndependentBudgets)return ev.Risk;
            if(Variant==Branch.PriorScaledIndependent)return ev.Risk*IndependentMultiplier;
            var allowed=Math.Min(ev.Risk,Math.Max(0,GrossRiskCap-_holdings.Values.Sum(h=>h.InitialR)));
            if(Variant==Branch.GrossCapControl)return allowed;
            var cov=FindCovariance(ev.Receipt);
            if(PortfolioStd(cov,ev.Strategy,0)>StandardDeviationBudget)return 0;
            double low=0,high=allowed;
            for(int i=0;i<80;i++){var middle=(low+high)/2;if(PortfolioStd(cov,ev.Strategy,middle)<=StandardDeviationBudget)low=middle;else high=middle;}
            Print("T095_PROJECTED_RISK,receipt={0:O},strategy={1},proposed_R={2},std={3},gross_R={4}",ev.Receipt,ev.Strategy,low,PortfolioStd(cov,ev.Strategy,low),_holdings.Values.Sum(h=>h.InitialR)+low);
            return low; }
        private void Closed(PositionClosedEventArgs a)
        {
            Holding h;if(!_holdings.TryGetValue(a.Position.Id,out h))return;
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);RefreshReset(now);
            _holdings.Remove(a.Position.Id);
            
            Print("T095_CLOSED,id={0},strategy={1},net={2},initial_R={3},realized_R={4},reason={5}",a.Position.Id,h.Event.Strategy,a.Position.NetProfit,h.InitialR,a.Position.NetProfit/h.InitialR,a.Reason);
        }
        private DateTime Key(DateTime utc)
        {
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,_zone);return local.TimeOfDay<_resetClock?local.Date.AddDays(-1):local.Date;
        }
        private void RefreshReset(DateTime now)
        {
            var key=Key(now);if(key==_resetKey)return;
            var instant=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(key.Add(_resetClock),DateTimeKind.Unspecified),_zone);
            // Native stop callbacks may precede OnTick. Retain the last pre-reset balance,
            // not a balance already altered by a stop filled on the first new-day quote.
            _resetBalance=_previousTime!=DateTime.MinValue && _previousTime<instant?_previousBalance:Account.Balance;
            _resetKey=key;_barrierHit=false;
            Print("T095_RESET,key={0:yyyy-MM-dd},instant={1:O},reference_balance={2},first_quote_lag_seconds={3},reference_sampling_uncertain={4}",key,instant,_resetBalance,(now-instant).TotalSeconds,(now-instant).TotalSeconds>2);
            
        }
        private double Reference(double resetBalance)=>DailyAnchor==AnchorRule.ResetBalance?resetBalance:DailyAnchor==AnchorRule.InitialBalance?_initialBalance:Math.Max(_initialBalance,resetBalance);
        private double DailyFloor()=>Reference(_resetBalance)-DailyAllowance;
        private DateTime NextReset(DateTime now)=>TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Key(now).AddDays(1).Add(_resetClock),DateTimeKind.Unspecified),_zone);
        private void LogEquity(DateTime now)
        {
            var gross=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>p.VolumeInUnits);
            var net=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>(p.TradeType==TradeType.Buy?1:-1)*p.VolumeInUnits);
            Print("T095_EQUITY,quote={0:O},balance={1},equity={2},peak={3},daily_floor={4},total_floor={5},gross_gold_units={6},net_gold_units={7}",now,Account.Balance,Account.Equity,_peakEquity,DailyFloor(),TotalEquityFloor,gross,net);
        }
        private void Skip(Opportunity ev,string reason)=>Print("T095_SKIP,receipt={0:O},strategy={1},reason={2},actual_opportunity_pnl=0",ev.Receipt,ev.Strategy,reason);
        private bool ValidQuote()=>Finite(Symbol.Bid)&&Finite(Symbol.Ask)&&Symbol.Bid>0&&Symbol.Ask>=Symbol.Bid;
        private static DateTime Utc(string s)=>DateTime.ParseExact(s.Trim(),new[]{"yyyy-MM-ddTHH:mm:ss'Z'","yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string s){var n=double.Parse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite input");return n;}
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        private Covariance FindCovariance(DateTime knownBy)=>_covariances.LastOrDefault(c=>c.Available<=knownBy);
        private double PortfolioStd(Covariance c,string proposed,double risk)
        {
            var w=new double[c.Ids.Length];foreach(var h in _holdings.Values)w[Array.IndexOf(c.Ids,h.Event.Strategy)]+=h.InitialR;
            if(proposed!=null)w[Array.IndexOf(c.Ids,proposed)]+=risk;
            double variance=0;for(int i=0;i<w.Length;i++)for(int j=0;j<w.Length;j++)variance+=w[i]*c.Matrix[i,j]*w[j];
            return Math.Sqrt(Math.Max(0,variance));
        }
        public static void ValidateCovariance(double[,] matrix)
        {
            if(matrix==null||matrix.GetLength(0)==0||matrix.GetLength(0)!=matrix.GetLength(1))throw new ArgumentException("Square covariance required");
            int n=matrix.GetLength(0);double scale=0;for(int i=0;i<n;i++){if(!Finite(matrix[i,i]))throw new ArgumentException("Finite covariance required");scale=Math.Max(scale,Math.Abs(matrix[i,i]));}
            var tolerance=1e-10*(scale>0?scale:1.0);var l=new double[n,n];
            for(int i=0;i<n;i++)for(int j=0;j<=i;j++)
            {
                if(!Finite(matrix[i,j])||!Finite(matrix[j,i])||Math.Abs(matrix[i,j]-matrix[j,i])>tolerance)throw new ArgumentException("Symmetric finite covariance required");
                var residual=matrix[i,j];for(int k=0;k<j;k++)residual-=l[i,k]*l[j,k];
                if(i==j){if(residual< -tolerance)throw new ArgumentException("Covariance must be positive semidefinite");l[i,j]=Math.Sqrt(Math.Max(0,residual));}
                else if(l[j,j]>Math.Sqrt(tolerance)*1e-3)l[i,j]=residual/l[j,j];
                else if(Math.Abs(residual)>tolerance)throw new ArgumentException("Singular covariance is not positive semidefinite");
            }
        }
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            foreach(var holding in _holdings)Print("T095_CENSORED_POSITION,id={0},original_exit={1:O}",holding.Key,holding.Value.Exit);
            
            Print("T095_STOP,balance={0},equity={1},peak_equity={2},barrier_breach_days={3},log_wealth={4},unreached={5}",Account.Balance,Account.Equity,_peakEquity,_dailyBreaches,Account.Equity>0&&_initialBalance>0?Math.Log(Account.Equity/_initialBalance):double.NegativeInfinity,_events.Count-_next);
        }
    }
}
