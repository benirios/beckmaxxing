using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
namespace cAlgo.Robots
{
    /// <summary>
    /// T092: Stop NEW entries for the remainder of the declared session after actual cumulative realized PnL reaches -2 initial-risk units. Open protection/exit stays unchanged. Baseline and prior-calibrated random clock interruption use the same non-overlapping frozen signal stream. Counterfactual skipped outcomes are explicitly a marketable-quote model, never actual fills and never input to realized-loss decisions.
    /// Frozen rows: receiptUTC|exitUTC|strategyId|side(+1/-1)|stopDistanceUSD|targetDistanceUSD(0=none)|baseRiskAccountCurrency|counterfactualCommissionPerUnitRoundtrip|counterfactualSlippagePerUnitPerSide.
    /// All estimates must be available at receipt. Backtest only, no live deployment.
    /// Select the actual account reset rule/zone/clock and currency barriers explicitly;
    /// no account product or loss percentage is inferred. Initial account must be empty.
    /// External validation reference is provenance supplied by the researcher, not proof
    /// that this project has discovered a positive signal. All tick equity paths are logged.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdCumulativeLossInterruption : Robot
    {
        public enum Branch { Baseline, TwoRInterruption, RandomClockControl }
        public enum AnchorRule { Unspecified, ResetBalance, InitialBalance, HigherOfInitialAndReset }
        [Parameter("Branch",DefaultValue=Branch.TwoRInterruption)] public Branch Variant {get;set;}
        [Parameter("Frozen signal opportunities",DefaultValue="")] public string Opportunities {get;set;}
        [Parameter("Prior validated signal reference",DefaultValue="")] public string ValidationReference {get;set;}
        [Parameter("Actual account reset IANA zone",DefaultValue="")] public string ResetZone {get;set;}
        [Parameter("Actual reset local clock HH:mm",DefaultValue="")] public string ResetClock {get;set;}
        [Parameter("Actual daily reference rule",DefaultValue=AnchorRule.Unspecified)] public AnchorRule DailyAnchor {get;set;}
        [Parameter("Daily allowance (account currency)",DefaultValue=0.0,MinValue=0.0)] public double DailyAllowance {get;set;}
        [Parameter("Actual total equity floor (account currency)",DefaultValue=0.0,MinValue=0.0)] public double TotalEquityFloor {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T092")] public string PositionLabel {get;set;}
        [Parameter("Loss-session IANA zone",DefaultValue="")] public string LossSessionZone {get;set;}
        [Parameter("Loss-session local start HH:mm",DefaultValue="")] public string LossSessionClock {get;set;}
        [Parameter("Precalibrated random interruption UTC times",DefaultValue="")] public string RandomInterruptions {get;set;}
        private TimeZoneInfo _lossZone;
        private TimeSpan _lossClock;
        private DateTime _lossKey=DateTime.MinValue;
        private double _realizedR;
        private readonly Dictionary<int,double> _initialRisks=new Dictionary<int,double>();
        private readonly Dictionary<int,double> _initialVolumes=new Dictionary<int,double>();
        private readonly HashSet<long> _processedDeals=new HashSet<long>();
        private readonly HashSet<int> _closedAwaitingHistory=new HashSet<int>();
        private bool _lossHalt,_randomHalt;
        private readonly List<DateTime> _random=new List<DateTime>();
        private int _nextRandom;
        private class Shadow { public Opportunity Event; public double Entry,Stop,Target,Units; }
        private readonly List<Shadow> _shadows=new List<Shadow>();
        private class Opportunity { public DateTime Receipt,Exit; public string Strategy; public int Side; public double Stop,Target,Risk; public double Commission,Slippage; }
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
            if(RunningMode==RunningMode.RealTime){Print("T092_STOP,reason=research_only");Stop();return;}
            try
            {
                if(string.IsNullOrWhiteSpace(ValidationReference)||DailyAnchor==AnchorRule.Unspecified||!Finite(DailyAllowance)||DailyAllowance<=0||!Finite(TotalEquityFloor)||TotalEquityFloor<=0||Positions.Count!=0||PendingOrders.Count!=0)throw new FormatException("Prior signal reference, explicit account rules/barriers and initially empty account required");
                _zone=TimeZoneInfo.FindSystemTimeZoneById(ResetZone);
                _resetClock=TimeSpan.ParseExact(ResetClock,"hh\\:mm",CultureInfo.InvariantCulture);
                _lossZone=TimeZoneInfo.FindSystemTimeZoneById(LossSessionZone);
                _lossClock=TimeSpan.ParseExact(LossSessionClock,"hh\\:mm",CultureInfo.InvariantCulture);
                var randomSeen=new HashSet<DateTime>();
                foreach(var token in (RandomInterruptions??"").Split(new[]{',',';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var time=Utc(token);if(!randomSeen.Add(time))throw new FormatException("Duplicate random interruption");_random.Add(time);
                }
                _random.Sort();if(Variant==Branch.RandomClockControl&&_random.Count==0)throw new FormatException("Prior-calibrated random interruption times required");
                var seen=new HashSet<string>();
                foreach(var row in (Opportunities??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=9)throw new FormatException("Expected 9 columns per frozen signal");
                    var side=Number(c[3]);var ev=new Opportunity{Receipt=Utc(c[0]),Exit=Utc(c[1]),Strategy=c[2].Trim(),Side=(int)side,Stop=Number(c[4]),Target=Number(c[5]),Risk=Number(c[6])};
                    if(ev.Strategy.Length==0||!seen.Add(ev.Receipt.Ticks+"_"+ev.Strategy)||ev.Exit<=ev.Receipt||(side!=1&&side!=-1)||ev.Stop<=0||ev.Target<0||ev.Risk<=0)throw new FormatException("Invalid frozen signal");
                    ev.Commission=Number(c[7]);ev.Slippage=Number(c[8]);if(ev.Commission<0||ev.Slippage<0)throw new FormatException("Nonnegative explicit counterfactual costs required");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Frozen positive-signal opportunities required");
                _events.Sort((a,b)=>a.Receipt==b.Receipt?string.CompareOrdinal(a.Strategy,b.Strategy):a.Receipt.CompareTo(b.Receipt));
                for(int i=1;i<_events.Count;i++)if(_events[i].Receipt<_events[i-1].Exit)throw new FormatException("T092 requires the same non-overlapping separate-trade stream in each branch");
            }
            catch(Exception e){Print("T092_STOP,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            _initialBalance=Account.Balance;_previousBalance=Account.Balance;_peakEquity=Account.Equity;
            Positions.Closed+=Closed;
            Print("T092_START,branch={0},events={1},validation_reference={2},daily_anchor={3},reset_zone={4},reset_clock={5},daily_allowance={6},total_floor={7}",Variant,_events.Count,ValidationReference,DailyAnchor,ResetZone,ResetClock,DailyAllowance,TotalEquityFloor);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            RefreshReset(now);
            ProcessRealizations(now);
            while(_nextRandom<_random.Count&&_random[_nextRandom]<=now)
            {
                var stamp=_random[_nextRandom++];if(LossKey(stamp)==_lossKey)_randomHalt=true;
            }
            _peakEquity=Math.Max(_peakEquity,Account.Equity);
            if(!_barrierHit && (Account.Equity<=DailyFloor()||Account.Equity<=TotalEquityFloor))
            {
                _barrierHit=true;_everBarrierHit=true;_dailyBreaches++;
                Print("T092_BREACH,quote={0:O},equity={1},daily_floor={2},total_floor={3}",now,Account.Equity,DailyFloor(),TotalEquityFloor);
            }
            if(_everBarrierHit)foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())_holdings[p.Id].Exit=now;
            
            foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())
                if(now>=_holdings[p.Id].Exit)
                {
                    var deadline=_holdings[p.Id].Exit;var close=ClosePosition(p);Print("T092_EXIT,id={0},quote={1:O},success={2},error={3},deadline={4:O},lateness_seconds={5}",p.Id,now,close.IsSuccessful,close.Error,deadline,(now-deadline).TotalSeconds);
                }
            if(ValidQuote())
            {
                ProcessRealizations(now);
                for(int i=_shadows.Count-1;i>=0;i--)
                {
                    var s=_shadows[i];var exit=s.Event.Side>0?Symbol.Bid:Symbol.Ask;
                    var stop=s.Event.Side*(exit-s.Stop)<=0;
                    var target=s.Event.Target>0&&s.Event.Side*(exit-s.Target)>=0;
                    if(now>=s.Event.Exit||stop||target)
                    {
                        var pnl=s.Units*(s.Event.Side*(exit-s.Entry)-s.Event.Commission-2*s.Event.Slippage);
                        Print("T092_COUNTERFACTUAL_QUOTES,receipt={0:O},quote={1:O},units={2},modeled_net={3},stop={4},target={5},timed_endpoint_valid={6},not_an_actual_fill=1",s.Event.Receipt,now,s.Units,pnl,stop,target,now<=s.Event.Exit.AddSeconds(2));
                        _shadows.RemoveAt(i);
                    }
                }
                while(_next<_events.Count && _events[_next].Receipt<now)
                {
                    var ev=_events[_next++];
                    if(now>=ev.Exit){Skip(ev,"original_exit_already_reached");continue;}
                    if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"missing_timely_quote");continue;}
                    var shadowUnits=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(ev.Risk,ev.Stop/Symbol.PipSize,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                    if(shadowUnits>=Symbol.VolumeInUnitsMin&&ev.Stop>Symbol.Ask-Symbol.Bid)
                    {
                        var entry=ev.Side>0?Symbol.Ask:Symbol.Bid;
                        _shadows.Add(new Shadow{Event=ev,Entry=entry,Stop=entry-ev.Side*ev.Stop,Target=entry+ev.Side*ev.Target,Units=shadowUnits});
                    }
                    if(_everBarrierHit){Skip(ev,"account_barrier");continue;}
                    if(Positions.Any(p=>!_holdings.ContainsKey(p.Id))||PendingOrders.Count>0){Skip(ev,"unknown_external_account_risk");continue;}
                    if(_holdings.Count>0){Skip(ev,"overlapping_signal");continue;}
                    if(!Eligible(ev,now)){Skip(ev,"policy_interruption");continue;}
                    var risk=Allocate(ev,now);
                    if(!Finite(risk)||risk<=0){Skip(ev,"zero_policy_allocation");continue;}
                    var stopPips=ev.Stop/Symbol.PipSize;
                    if(ev.Stop<=Symbol.Ask-Symbol.Bid){Skip(ev,"stop_inside_spread");continue;}
                    var volume=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(risk,stopPips,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                    if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"volume_below_minimum");continue;}
                    var result=ExecuteMarketOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label+"_"+_next,stopPips,ev.Target>0?(double?)(ev.Target/Symbol.PipSize):null,"T092 frozen signal "+ev.Strategy);
                    if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                    var stopVerified=result.Position.StopLoss.HasValue&&ev.Side*(result.Position.EntryPrice-result.Position.StopLoss.Value)>0;
                    var measuredPips=stopVerified?Math.Abs(result.Position.EntryPrice-result.Position.StopLoss.Value)/Symbol.PipSize:stopPips;
                    var initialR=Symbol.AmountRisked(result.Position.VolumeInUnits,measuredPips);
                    if(!stopVerified)Print("T092_UNCERTIFIED_PROTECTION,id={0},planned_R_used_for_attempt_costs=1,close_at_next_quote=1",result.Position.Id);
                    _holdings[result.Position.Id]=new Holding{Event=ev,Exit=stopVerified?ev.Exit:now,InitialR=initialR};
                    _initialRisks[result.Position.Id]=initialR;_initialVolumes[result.Position.Id]=result.Position.VolumeInUnits;
                    Print("T092_ENTRY,receipt={0:O},id={1},strategy={2},side={3},units={4},allocated_risk={5},initial_actual_R={6},exit={7:O}",ev.Receipt,result.Position.Id,ev.Strategy,ev.Side,volume,risk,initialR,ev.Exit);
                }
            }
            LogEquity(now);
            _previousBalance=Account.Balance;_previousTime=now;
        }
        private bool Eligible(Opportunity ev,DateTime now) { return _closedAwaitingHistory.Count==0&&(Variant==Branch.Baseline || (Variant==Branch.TwoRInterruption?!_lossHalt:!_randomHalt)); }
        private double Allocate(Opportunity ev,DateTime now) { return ev.Risk; }
        private void Closed(PositionClosedEventArgs a)
        {
            Holding h;if(!_holdings.TryGetValue(a.Position.Id,out h))return;
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);RefreshReset(now);
            _holdings.Remove(a.Position.Id);
            _closedAwaitingHistory.Add(a.Position.Id);
            ProcessRealizations(now);
            Print("T092_CLOSED,id={0},strategy={1},last_callback_net={2},initial_R={3},callback_R={4},reason={5}",a.Position.Id,h.Event.Strategy,a.Position.NetProfit,h.InitialR,a.Position.NetProfit/h.InitialR,a.Reason);
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
            Print("T092_RESET,key={0:yyyy-MM-dd},instant={1:O},reference_balance={2},first_quote_lag_seconds={3},reference_sampling_uncertain={4}",key,instant,_resetBalance,(now-instant).TotalSeconds,(now-instant).TotalSeconds>2);
            
        }
        private double Reference(double resetBalance)=>DailyAnchor==AnchorRule.ResetBalance?resetBalance:DailyAnchor==AnchorRule.InitialBalance?_initialBalance:Math.Max(_initialBalance,resetBalance);
        private double DailyFloor()=>Reference(_resetBalance)-DailyAllowance;
        private DateTime NextReset(DateTime now)=>TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Key(now).AddDays(1).Add(_resetClock),DateTimeKind.Unspecified),_zone);
        private void LogEquity(DateTime now)
        {
            var gross=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>p.VolumeInUnits);
            var net=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>(p.TradeType==TradeType.Buy?1:-1)*p.VolumeInUnits);
            Print("T092_EQUITY,quote={0:O},balance={1},equity={2},peak={3},daily_floor={4},total_floor={5},gross_gold_units={6},net_gold_units={7}",now,Account.Balance,Account.Equity,_peakEquity,DailyFloor(),TotalEquityFloor,gross,net);
        }
        private void Skip(Opportunity ev,string reason)=>Print("T092_SKIP,receipt={0:O},strategy={1},reason={2},actual_opportunity_pnl=0",ev.Receipt,ev.Strategy,reason);
        private bool ValidQuote()=>Finite(Symbol.Bid)&&Finite(Symbol.Ask)&&Symbol.Bid>0&&Symbol.Ask>=Symbol.Bid;
        private static DateTime Utc(string s)=>DateTime.ParseExact(s.Trim(),new[]{"yyyy-MM-ddTHH:mm:ss'Z'","yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string s){var n=double.Parse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite input");return n;}
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        private void ProcessRealizations(DateTime now)
        {
            RefreshLossSession(now);
            foreach(var known in _initialRisks.ToArray())
            {
                var deals=History.FindByPositionId(known.Key);
                foreach(var deal in deals)
                {
                    if(!_processedDeals.Add(deal.ClosingDealId))continue;
                    var key=LossKey(deal.ClosingTime);
                    if(key==_lossKey){_realizedR+=deal.NetProfit/known.Value;if(_realizedR<=-2)_lossHalt=true;}
                    Print("T092_REALIZED_DEAL,position={0},deal={1},closing_time={2:O},deal_net={3},initial_R={4},closing_session={5:yyyy-MM-dd},current_session_R={6},sticky_interruption={7}",known.Key,deal.ClosingDealId,deal.ClosingTime,deal.NetProfit,known.Value,key,_realizedR,_lossHalt);
                }
                if(_closedAwaitingHistory.Contains(known.Key)&&deals.Sum(d=>d.VolumeInUnits)>=_initialVolumes[known.Key]-1e-9)
                {
                    _closedAwaitingHistory.Remove(known.Key);_initialRisks.Remove(known.Key);_initialVolumes.Remove(known.Key);
                    foreach(var deal in deals)_processedDeals.Remove(deal.ClosingDealId);
                }
            }
        }
        private DateTime LossKey(DateTime utc){var local=TimeZoneInfo.ConvertTimeFromUtc(utc,_lossZone);return local.TimeOfDay<_lossClock?local.Date.AddDays(-1):local.Date;}
        private void RefreshLossSession(DateTime now)
        {
            var key=LossKey(now);if(key==_lossKey)return;_lossKey=key;_realizedR=0;_lossHalt=false;_randomHalt=false;
            Print("T092_LOSS_SESSION_RESET,key={0:yyyy-MM-dd}",key);
        }
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            foreach(var holding in _holdings)Print("T092_CENSORED_POSITION,id={0},original_exit={1:O}",holding.Key,holding.Value.Exit);
            foreach(var s in _shadows)Print("T092_COUNTERFACTUAL_CENSORED,receipt={0:O}",s.Event.Receipt);
            Print("T092_STOP,balance={0},equity={1},peak_equity={2},barrier_breach_days={3},log_wealth={4},unreached={5}",Account.Balance,Account.Equity,_peakEquity,_dailyBreaches,Account.Equity>0&&_initialBalance>0?Math.Log(Account.Equity/_initialBalance):double.NegativeInfinity,_events.Count-_next);
        }
    }
}
