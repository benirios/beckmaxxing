using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
namespace cAlgo.Robots
{
    /// <summary>
    /// T096: Before the DECLARED actual account reset, close when projected next-day equity headroom after known remaining charges is below a frozen buffer. Compare baseline and prior-calibrated random early closure. Realizing floating PnL changes balance and may change the reset reference; log both projections. Outside unknown positions make the projection uncertified. No generic FTMO percentage/product is assumed, and an already failed account never resumes after a reset.
    /// Frozen rows: receiptUTC|exitUTC|strategyId|side(+1/-1)|stopDistanceUSD|targetDistanceUSD(0=none)|baseRiskAccountCurrency.
    /// All estimates must be available at receipt. Backtest only, no live deployment.
    /// Select the actual account reset rule/zone/clock and currency barriers explicitly;
    /// no account product or loss percentage is inferred. Initial account must be empty.
    /// External validation reference is provenance supplied by the researcher, not proof
    /// that this project has discovered a positive signal. All tick equity paths are logged.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdResetHeadroomPolicy : Robot
    {
        public enum Branch { Baseline, HeadroomClosure, RandomClosureControl }
        public enum AnchorRule { Unspecified, ResetBalance, InitialBalance, HigherOfInitialAndReset }
        [Parameter("Branch",DefaultValue=Branch.HeadroomClosure)] public Branch Variant {get;set;}
        [Parameter("Frozen signal opportunities",DefaultValue="")] public string Opportunities {get;set;}
        [Parameter("Prior validated signal reference",DefaultValue="")] public string ValidationReference {get;set;}
        [Parameter("Actual account reset IANA zone",DefaultValue="")] public string ResetZone {get;set;}
        [Parameter("Actual reset local clock HH:mm",DefaultValue="")] public string ResetClock {get;set;}
        [Parameter("Actual daily reference rule",DefaultValue=AnchorRule.Unspecified)] public AnchorRule DailyAnchor {get;set;}
        [Parameter("Daily allowance (account currency)",DefaultValue=0.0,MinValue=0.0)] public double DailyAllowance {get;set;}
        [Parameter("Actual total equity floor (account currency)",DefaultValue=0.0,MinValue=0.0)] public double TotalEquityFloor {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T096")] public string PositionLabel {get;set;}
        [Parameter("Frozen pre-reset lead seconds",DefaultValue=0.0)] public double LeadSeconds {get;set;}
        [Parameter("Frozen headroom buffer (account currency)",DefaultValue=0.0)] public double HeadroomBuffer {get;set;}
        [Parameter("Exact remaining charges declared",DefaultValue=false)] public bool ChargesDeclared {get;set;}
        [Parameter("Reset charge per long unit (negative=credit)",DefaultValue=0.0)] public double LongResetCharge {get;set;}
        [Parameter("Reset charge per short unit (negative=credit)",DefaultValue=0.0)] public double ShortResetCharge {get;set;}
        [Parameter("Additional unaccrued exit charge per unit",DefaultValue=0.0)] public double ExitCharge {get;set;}
        [Parameter("Prior-calibrated random early-close UTC times",DefaultValue="")] public string RandomClosures {get;set;}
        private readonly List<DateTime> _randomClosures=new List<DateTime>();
        private int _nextRandom;
        private DateTime _closedForNextReset=DateTime.MinValue;
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
            if(RunningMode==RunningMode.RealTime){Print("T096_STOP,reason=research_only");Stop();return;}
            try
            {
                if(string.IsNullOrWhiteSpace(ValidationReference)||DailyAnchor==AnchorRule.Unspecified||!Finite(DailyAllowance)||DailyAllowance<=0||!Finite(TotalEquityFloor)||TotalEquityFloor<=0||Positions.Count!=0||PendingOrders.Count!=0)throw new FormatException("Prior signal reference, explicit account rules/barriers and initially empty account required");
                _zone=TimeZoneInfo.FindSystemTimeZoneById(ResetZone);
                _resetClock=TimeSpan.ParseExact(ResetClock,"hh\\:mm",CultureInfo.InvariantCulture);
                if(Account.AccountType!=AccountType.Hedged||!Finite(LeadSeconds)||LeadSeconds<=0||LeadSeconds>=86400||!Finite(HeadroomBuffer)||HeadroomBuffer<0||!ChargesDeclared||!Finite(LongResetCharge)||!Finite(ShortResetCharge)||!Finite(ExitCharge)||ExitCharge<0)throw new FormatException("Freeze actual charge convention, lead and buffer");
                var seenRandom=new HashSet<DateTime>();
                foreach(var token in (RandomClosures??"").Split(new[]{',',';','\n','\r'},StringSplitOptions.RemoveEmptyEntries)){var time=Utc(token);if((NextReset(time)-time).TotalSeconds>LeadSeconds)throw new FormatException("Random closure must fall inside the frozen pre-reset window");if(!seenRandom.Add(time))throw new FormatException("Duplicate random close");_randomClosures.Add(time);}
                _randomClosures.Sort();if(Variant==Branch.RandomClosureControl&&_randomClosures.Count==0)throw new FormatException("Precalibrated random close schedule required");
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
            catch(Exception e){Print("T096_STOP,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            _initialBalance=Account.Balance;_previousBalance=Account.Balance;_peakEquity=Account.Equity;
            Positions.Closed+=Closed;
            Print("T096_START,branch={0},events={1},validation_reference={2},daily_anchor={3},reset_zone={4},reset_clock={5},daily_allowance={6},total_floor={7}",Variant,_events.Count,ValidationReference,DailyAnchor,ResetZone,ResetClock,DailyAllowance,TotalEquityFloor);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            RefreshReset(now);
            
            _peakEquity=Math.Max(_peakEquity,Account.Equity);
            if(!_barrierHit && (Account.Equity<=DailyFloor()||Account.Equity<=TotalEquityFloor))
            {
                _barrierHit=true;_everBarrierHit=true;_dailyBreaches++;
                Print("T096_BREACH,quote={0:O},equity={1},daily_floor={2},total_floor={3}",now,Account.Equity,DailyFloor(),TotalEquityFloor);
            }
            if(_everBarrierHit)foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())_holdings[p.Id].Exit=now;
            var reset=NextReset(now);var until=(reset-now).TotalSeconds;var randomDue=false;
            while(_nextRandom<_randomClosures.Count&&_randomClosures[_nextRandom]<=now)
            {
                var stamp=_randomClosures[_nextRandom++];if(Key(stamp)==Key(now))randomDue=true;
            }
            if(until<=LeadSeconds&&until>0)
            {
                var outside=Positions.Any(p=>!_holdings.ContainsKey(p.Id))||PendingOrders.Count>0;
                var projected=ProjectedHeadroom(0,0);
                Print("T096_PRE_RESET,quote={0:O},next_reset={1:O},projected_headroom={2},buffer={3},outside_unknown_positions={4},reference_balance_before_closure={5}",now,reset,projected,HeadroomBuffer,outside,Account.Balance);
                var policy=Variant==Branch.HeadroomClosure&&(outside||projected<HeadroomBuffer);
                if(policy||(Variant==Branch.RandomClosureControl&&randomDue))
                {
                    _closedForNextReset=reset;
                    foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())
                    {
                        _holdings[p.Id].Exit=now;
                        var close=ClosePosition(p);
                        Print("T096_EARLY_CLOSE,id={0},success={1},error={2},headroom_after={3},balance_after={4}",p.Id,close.IsSuccessful,close.Error,ProjectedHeadroom(0,0),Account.Balance);
                    }
                }
            }
            foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())
                if(now>=_holdings[p.Id].Exit)
                {
                    var deadline=_holdings[p.Id].Exit;var close=ClosePosition(p);Print("T096_EXIT,id={0},quote={1:O},success={2},error={3},deadline={4:O},lateness_seconds={5}",p.Id,now,close.IsSuccessful,close.Error,deadline,(now-deadline).TotalSeconds);
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
                    var result=ExecuteMarketOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label+"_"+_next,stopPips,ev.Target>0?(double?)(ev.Target/Symbol.PipSize):null,"T096 frozen signal "+ev.Strategy);
                    if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                    var stopVerified=result.Position.StopLoss.HasValue&&ev.Side*(result.Position.EntryPrice-result.Position.StopLoss.Value)>0;
                    var measuredPips=stopVerified?Math.Abs(result.Position.EntryPrice-result.Position.StopLoss.Value)/Symbol.PipSize:stopPips;
                    var initialR=Symbol.AmountRisked(result.Position.VolumeInUnits,measuredPips);
                    if(!stopVerified)Print("T096_UNCERTIFIED_PROTECTION,id={0},planned_R_used_for_attempt_costs=1,close_at_next_quote=1",result.Position.Id);
                    _holdings[result.Position.Id]=new Holding{Event=ev,Exit=stopVerified?ev.Exit:now,InitialR=initialR};
                    Print("T096_ENTRY,receipt={0:O},id={1},strategy={2},side={3},units={4},allocated_risk={5},initial_actual_R={6},exit={7:O}",ev.Receipt,result.Position.Id,ev.Strategy,ev.Side,volume,risk,initialR,ev.Exit);
                }
            }
            LogEquity(now);
            _previousBalance=Account.Balance;_previousTime=now;
        }
        private bool Eligible(Opportunity ev,DateTime now) { if(Variant==Branch.Baseline)return true;
            var reset=NextReset(now);if(_closedForNextReset==reset)return false;
            if(Variant==Branch.HeadroomClosure&&(reset-now).TotalSeconds<=LeadSeconds)
            {
                var units=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(ev.Risk,ev.Stop/Symbol.PipSize,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                var immediateSpread=Symbol.AmountRisked(units,(Symbol.Ask-Symbol.Bid)/Symbol.PipSize);
                return ProjectedHeadroom(ev.Side*units,immediateSpread)>=HeadroomBuffer;
            }
            return true; }
        private double Allocate(Opportunity ev,DateTime now) { return ev.Risk; }
        private void Closed(PositionClosedEventArgs a)
        {
            Holding h;if(!_holdings.TryGetValue(a.Position.Id,out h))return;
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);RefreshReset(now);
            _holdings.Remove(a.Position.Id);
            
            Print("T096_CLOSED,id={0},strategy={1},net={2},initial_R={3},realized_R={4},reason={5}",a.Position.Id,h.Event.Strategy,a.Position.NetProfit,h.InitialR,a.Position.NetProfit/h.InitialR,a.Reason);
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
            Print("T096_RESET,key={0:yyyy-MM-dd},instant={1:O},reference_balance={2},first_quote_lag_seconds={3},reference_sampling_uncertain={4}",key,instant,_resetBalance,(now-instant).TotalSeconds,(now-instant).TotalSeconds>2);
            
        }
        private double Reference(double resetBalance)=>DailyAnchor==AnchorRule.ResetBalance?resetBalance:DailyAnchor==AnchorRule.InitialBalance?_initialBalance:Math.Max(_initialBalance,resetBalance);
        private double DailyFloor()=>Reference(_resetBalance)-DailyAllowance;
        private DateTime NextReset(DateTime now)=>TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Key(now).AddDays(1).Add(_resetClock),DateTimeKind.Unspecified),_zone);
        private void LogEquity(DateTime now)
        {
            var gross=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>p.VolumeInUnits);
            var net=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>(p.TradeType==TradeType.Buy?1:-1)*p.VolumeInUnits);
            Print("T096_EQUITY,quote={0:O},balance={1},equity={2},peak={3},daily_floor={4},total_floor={5},gross_gold_units={6},net_gold_units={7}",now,Account.Balance,Account.Equity,_peakEquity,DailyFloor(),TotalEquityFloor,gross,net);
        }
        private void Skip(Opportunity ev,string reason)=>Print("T096_SKIP,receipt={0:O},strategy={1},reason={2},actual_opportunity_pnl=0",ev.Receipt,ev.Strategy,reason);
        private bool ValidQuote()=>Finite(Symbol.Bid)&&Finite(Symbol.Ask)&&Symbol.Bid>0&&Symbol.Ask>=Symbol.Bid;
        private static DateTime Utc(string s)=>DateTime.ParseExact(s.Trim(),new[]{"yyyy-MM-ddTHH:mm:ss'Z'","yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string s){var n=double.Parse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite input");return n;}
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        private double ProjectedHeadroom(double proposedSignedUnits,double proposedImmediateLoss)
        {
            var charges=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>p.VolumeInUnits*((p.TradeType==TradeType.Buy?LongResetCharge:ShortResetCharge)+ExitCharge));
            if(proposedSignedUnits!=0)charges+=Math.Abs(proposedSignedUnits)*((proposedSignedUnits>0?LongResetCharge:ShortResetCharge)+ExitCharge);
            var projected=Account.Equity-proposedImmediateLoss-charges;
            return projected-Math.Max(Reference(Account.Balance)-DailyAllowance,TotalEquityFloor);
        }
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            foreach(var holding in _holdings)Print("T096_CENSORED_POSITION,id={0},original_exit={1:O}",holding.Key,holding.Value.Exit);
            
            Print("T096_STOP,balance={0},equity={1},peak_equity={2},barrier_breach_days={3},log_wealth={4},unreached={5}",Account.Balance,Account.Equity,_peakEquity,_dailyBreaches,Account.Equity>0&&_initialBalance>0?Math.Log(Account.Equity/_initialBalance):double.NegativeInfinity,_events.Count-_next);
        }
    }
}
