using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
namespace cAlgo.Robots
{
    /// <summary>
    /// T093: Immediate forecast-to-target repositioning versus one frozen no-trade band around CURRENT signed gold units. Retain existing hard protection; new delta fills receive frozen-row protection and maximum holding/original exit. Reduce existing volume by native partial close; never open the opposite side until all required closes succeed. Prior cost/decay calibration is an external provenance requirement. Log actual achieved exposure and turnover, including failed/incomplete adjustments.
    /// Frozen rows: receiptUTC|exitUTC|strategyId|targetSignedUnits(0=flat)|stopDistanceUSD|targetDistanceUSD(0=none)|baseRiskAccountCurrency.
    /// All estimates must be available at receipt. Backtest only, no live deployment.
    /// Select the actual account reset rule/zone/clock and currency barriers explicitly;
    /// no account product or loss percentage is inferred. Initial account must be empty.
    /// External validation reference is provenance supplied by the researcher, not proof
    /// that this project has discovered a positive signal. All tick equity paths are logged.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdPositionHysteresis : Robot
    {
        public enum Branch { ImmediateTarget, NoTradeBand }
        public enum AnchorRule { Unspecified, ResetBalance, InitialBalance, HigherOfInitialAndReset }
        [Parameter("Branch",DefaultValue=Branch.NoTradeBand)] public Branch Variant {get;set;}
        [Parameter("Frozen signal opportunities",DefaultValue="")] public string Opportunities {get;set;}
        [Parameter("Prior validated signal reference",DefaultValue="")] public string ValidationReference {get;set;}
        [Parameter("Actual account reset IANA zone",DefaultValue="")] public string ResetZone {get;set;}
        [Parameter("Actual reset local clock HH:mm",DefaultValue="")] public string ResetClock {get;set;}
        [Parameter("Actual daily reference rule",DefaultValue=AnchorRule.Unspecified)] public AnchorRule DailyAnchor {get;set;}
        [Parameter("Daily allowance (account currency)",DefaultValue=0.0,MinValue=0.0)] public double DailyAllowance {get;set;}
        [Parameter("Actual total equity floor (account currency)",DefaultValue=0.0,MinValue=0.0)] public double TotalEquityFloor {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T093")] public string PositionLabel {get;set;}
        [Parameter("Frozen no-trade band in units",DefaultValue=0.0)] public double BandUnits {get;set;}
        [Parameter("Frozen maximum absolute units",DefaultValue=0.0)] public double MaximumUnits {get;set;}
        [Parameter("Frozen maximum holding minutes",DefaultValue=0.0)] public double MaximumHoldingMinutes {get;set;}
        [Parameter("Prior cost/forecast-decay calibration reference",DefaultValue="")] public string CostCalibrationReference {get;set;}
        private double _turnoverUnits;
        private class Opportunity { public DateTime Receipt,Exit; public string Strategy; public int Side; public double Stop,Target,Risk; public double TargetUnits; }
        private class Holding { public Opportunity Event; public DateTime Exit; public double InitialR,Units; }
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
            if(RunningMode==RunningMode.RealTime){Print("T093_STOP,reason=research_only");Stop();return;}
            try
            {
                if(string.IsNullOrWhiteSpace(ValidationReference)||DailyAnchor==AnchorRule.Unspecified||!Finite(DailyAllowance)||DailyAllowance<=0||!Finite(TotalEquityFloor)||TotalEquityFloor<=0||Positions.Count!=0||PendingOrders.Count!=0)throw new FormatException("Prior signal reference, explicit account rules/barriers and initially empty account required");
                _zone=TimeZoneInfo.FindSystemTimeZoneById(ResetZone);
                _resetClock=TimeSpan.ParseExact(ResetClock,"hh\\:mm",CultureInfo.InvariantCulture);
                if(Account.AccountType!=AccountType.Hedged||!Finite(BandUnits)||BandUnits<0||!Finite(MaximumUnits)||MaximumUnits<=0||!Finite(MaximumHoldingMinutes)||MaximumHoldingMinutes<=0||string.IsNullOrWhiteSpace(CostCalibrationReference))throw new FormatException("Hedged semantics, frozen band/exposure/holding limits and prior cost calibration required");
                var seen=new HashSet<string>();
                foreach(var row in (Opportunities??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=7)throw new FormatException("Expected 7 columns per frozen signal");
                    var side=Number(c[3]);var ev=new Opportunity{Receipt=Utc(c[0]),Exit=Utc(c[1]),Strategy=c[2].Trim(),Side=Math.Sign(side),Stop=Number(c[4]),Target=Number(c[5]),Risk=Number(c[6])};
                    if(ev.Strategy.Length==0||!seen.Add(ev.Receipt.Ticks+"_"+ev.Strategy)||ev.Exit<=ev.Receipt||ev.Stop<=0||ev.Target<0||ev.Risk<=0)throw new FormatException("Invalid frozen signal");
                    ev.TargetUnits=side;if(Math.Abs(side)>MaximumUnits||(side!=0&&(Math.Abs(side)<Symbol.VolumeInUnitsMin||Symbol.NormalizeVolumeInUnits(Math.Abs(side),RoundingMode.Down)!=Math.Abs(side))))throw new FormatException("Frozen target must be exactly representable and within common exposure cap");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Frozen positive-signal opportunities required");
                _events.Sort((a,b)=>a.Receipt==b.Receipt?string.CompareOrdinal(a.Strategy,b.Strategy):a.Receipt.CompareTo(b.Receipt));
                if(_events.Any(e=>e.Strategy!=_events[0].Strategy||Math.Abs(e.Risk-_events[0].Risk)>1e-9))throw new FormatException("One unchanged forecast-to-position stream required; ensemble allocation is T095");
            }
            catch(Exception e){Print("T093_STOP,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            _initialBalance=Account.Balance;_previousBalance=Account.Balance;_peakEquity=Account.Equity;
            Positions.Closed+=Closed;
            Print("T093_START,branch={0},events={1},validation_reference={2},daily_anchor={3},reset_zone={4},reset_clock={5},daily_allowance={6},total_floor={7}",Variant,_events.Count,ValidationReference,DailyAnchor,ResetZone,ResetClock,DailyAllowance,TotalEquityFloor);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            RefreshReset(now);
            
            _peakEquity=Math.Max(_peakEquity,Account.Equity);
            if(!_barrierHit && (Account.Equity<=DailyFloor()||Account.Equity<=TotalEquityFloor))
            {
                _barrierHit=true;_everBarrierHit=true;_dailyBreaches++;
                Print("T093_BREACH,quote={0:O},equity={1},daily_floor={2},total_floor={3}",now,Account.Equity,DailyFloor(),TotalEquityFloor);
            }
            if(_everBarrierHit)foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())_holdings[p.Id].Exit=now;
            
            foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())
                if(now>=_holdings[p.Id].Exit)
                {
                    var deadline=_holdings[p.Id].Exit;var close=ClosePosition(p);Print("T093_EXIT,id={0},quote={1:O},success={2},error={3},deadline={4:O},lateness_seconds={5}",p.Id,now,close.IsSuccessful,close.Error,deadline,(now-deadline).TotalSeconds);
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
                    AdjustTarget(ev,now);
                }
            }
            LogEquity(now);
            _previousBalance=Account.Balance;_previousTime=now;
        }
        private bool Eligible(Opportunity ev,DateTime now) { return true; }
        private double Allocate(Opportunity ev,DateTime now) { return ev.Risk; }
        private void Closed(PositionClosedEventArgs a)
        {
            Holding h;if(!_holdings.TryGetValue(a.Position.Id,out h))return;
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);RefreshReset(now);
            _holdings.Remove(a.Position.Id);
            _turnoverUnits+=h.Units;
            
            Print("T093_CLOSED,id={0},strategy={1},net={2},initial_R={3},realized_R={4},reason={5}",a.Position.Id,h.Event.Strategy,a.Position.NetProfit,h.InitialR,a.Position.NetProfit/h.InitialR,a.Reason);
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
            Print("T093_RESET,key={0:yyyy-MM-dd},instant={1:O},reference_balance={2},first_quote_lag_seconds={3},reference_sampling_uncertain={4}",key,instant,_resetBalance,(now-instant).TotalSeconds,(now-instant).TotalSeconds>2);
            
        }
        private double Reference(double resetBalance)=>DailyAnchor==AnchorRule.ResetBalance?resetBalance:DailyAnchor==AnchorRule.InitialBalance?_initialBalance:Math.Max(_initialBalance,resetBalance);
        private double DailyFloor()=>Reference(_resetBalance)-DailyAllowance;
        private DateTime NextReset(DateTime now)=>TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Key(now).AddDays(1).Add(_resetClock),DateTimeKind.Unspecified),_zone);
        private void LogEquity(DateTime now)
        {
            var gross=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>p.VolumeInUnits);
            var net=Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>(p.TradeType==TradeType.Buy?1:-1)*p.VolumeInUnits);
            Print("T093_EQUITY,quote={0:O},balance={1},equity={2},peak={3},daily_floor={4},total_floor={5},gross_gold_units={6},net_gold_units={7}",now,Account.Balance,Account.Equity,_peakEquity,DailyFloor(),TotalEquityFloor,gross,net);
        }
        private void Skip(Opportunity ev,string reason)=>Print("T093_SKIP,receipt={0:O},strategy={1},reason={2},actual_opportunity_pnl=0",ev.Receipt,ev.Strategy,reason);
        private bool ValidQuote()=>Finite(Symbol.Bid)&&Finite(Symbol.Ask)&&Symbol.Bid>0&&Symbol.Ask>=Symbol.Bid;
        private static DateTime Utc(string s)=>DateTime.ParseExact(s.Trim(),new[]{"yyyy-MM-ddTHH:mm:ss'Z'","yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string s){var n=double.Parse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite input");return n;}
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        private double SignedUnits()=>Positions.Where(p=>_holdings.ContainsKey(p.Id)).Sum(p=>(p.TradeType==TradeType.Buy?1:-1)*p.VolumeInUnits);
        private void AdjustTarget(Opportunity ev,DateTime now)
        {
            var current=SignedUnits();var target=ev.TargetUnits;
            if(Variant==Branch.NoTradeBand&&Math.Abs(target-current)<=BandUnits)
            {
                Print("T093_HOLD,receipt={0:O},target={1},actual={2},band={3},unchanged_initial_protection=1",ev.Receipt,target,current,BandUnits);return;
            }
            if(target==0||current*target<0)
            {
                foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).ToArray())
                {
                    var volume=p.VolumeInUnits;var result=ClosePosition(p);
                    Print("T093_FLIP_OR_FLAT_CLOSE,id={0},units={1},success={2},error={3}",p.Id,volume,result.IsSuccessful,result.Error);
                    if(!result.IsSuccessful){Print("T093_INCOMPLETE,receipt={0:O},reason=close_failed_no_opposite_opened",ev.Receipt);return;}
                }
                current=SignedUnits();
                if(target==0)return;
            }
            var side=Math.Sign(target);current=SignedUnits();
            if(current*side>0&&Math.Abs(current)>Math.Abs(target))
            {
                var remaining=Math.Abs(current)-Math.Abs(target);
                foreach(var p in Positions.Where(p=>_holdings.ContainsKey(p.Id)).OrderByDescending(p=>p.EntryTime).ToArray())
                {
                    if(remaining<=1e-9)break;
                    var old=p.VolumeInUnits;var amount=Math.Min(old,remaining);
                    var result=amount>=old?ClosePosition(p):ClosePosition(p,amount);
                    Print("T093_PARTIAL_REDUCTION,id={0},requested_close={1},success={2},error={3}",p.Id,amount,result.IsSuccessful,result.Error);
                    if(!result.IsSuccessful)return;
                    var actualClosed=_holdings.ContainsKey(p.Id)?old-p.VolumeInUnits:old;
                    if(_holdings.ContainsKey(p.Id)){_holdings[p.Id].InitialR*=p.VolumeInUnits/old;_holdings[p.Id].Units=p.VolumeInUnits;_turnoverUnits+=actualClosed;}
                    remaining-=actualClosed;
                    if(actualClosed<=1e-9)return;
                }
            }
            current=SignedUnits();var increase=Math.Max(0,Math.Abs(target)-Math.Abs(current));
            if(increase>0)
            {
                var available=Math.Max(0,ev.Risk-_holdings.Values.Sum(h=>h.InitialR));
                if(available<=0){Print("T093_INCOMPLETE,receipt={0:O},reason=common_risk_ceiling",ev.Receipt);return;}
                var units=Symbol.NormalizeVolumeInUnits(Math.Min(increase,Math.Min(Symbol.VolumeInUnitsMax,Symbol.VolumeForFixedRisk(available,ev.Stop/Symbol.PipSize,RoundingMode.Down))),RoundingMode.Down);
                if(units>=Symbol.VolumeInUnitsMin&&ev.Stop>Symbol.Ask-Symbol.Bid)
                {
                    var result=ExecuteMarketOrder(side>0?TradeType.Buy:TradeType.Sell,SymbolName,units,_label+"_"+_next,ev.Stop/Symbol.PipSize,ev.Target>0?(double?)(ev.Target/Symbol.PipSize):null,"T093 forecast delta");
                    if(result.IsSuccessful&&result.Position!=null)
                    {
                        var actual=result.Position.VolumeInUnits;
                        var stopVerified=result.Position.StopLoss.HasValue&&side*(result.Position.EntryPrice-result.Position.StopLoss.Value)>0;
                        var measuredPips=stopVerified?Math.Abs(result.Position.EntryPrice-result.Position.StopLoss.Value)/Symbol.PipSize:ev.Stop/Symbol.PipSize;
                        var deadline=ev.Exit<now.AddMinutes(MaximumHoldingMinutes)?ev.Exit:now.AddMinutes(MaximumHoldingMinutes);
                        if(!stopVerified){deadline=now;Print("T093_UNCERTIFIED_PROTECTION,id={0},close_at_next_quote=1",result.Position.Id);}
                        _holdings[result.Position.Id]=new Holding{Event=ev,Exit=deadline,InitialR=Symbol.AmountRisked(actual,measuredPips),Units=actual};
                        _turnoverUnits+=actual;
                        Print("T093_ENTRY,receipt={0:O},id={1},units={2},price={3},stop_distance={4},scheduled_exit={5:O}",ev.Receipt,result.Position.Id,actual,result.Position.EntryPrice,ev.Stop,_holdings[result.Position.Id].Exit);
                    }
                    Print("T093_DELTA,receipt={0:O},units={1},side={2},success={3},error={4}",ev.Receipt,units,side,result.IsSuccessful,result.Error);
                }
            }
            Print("T093_TARGET_RESULT,receipt={0:O},desired={1},achieved={2},turnover_units={3},initial_R_currency={4}",ev.Receipt,target,SignedUnits(),_turnoverUnits,_holdings.Values.Sum(h=>h.InitialR));
        }
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            foreach(var holding in _holdings)Print("T093_CENSORED_POSITION,id={0},original_exit={1:O}",holding.Key,holding.Value.Exit);
            Print("T093_TURNOVER,marketable_units={0}",_turnoverUnits);
            Print("T093_STOP,balance={0},equity={1},peak_equity={2},barrier_breach_days={3},log_wealth={4},unreached={5}",Account.Balance,Account.Equity,_peakEquity,_dailyBreaches,Account.Equity>0&&_initialBalance>0?Math.Log(Account.Equity/_initialBalance):double.NegativeInfinity,_events.Count-_next);
        }
    }
}
