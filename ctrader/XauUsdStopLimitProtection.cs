using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
namespace cAlgo.Robots
{
    /// <summary>
    /// T101: ordinary native stop versus a native NETTED offsetting stop-limit.
    /// Rows: receiptUTC|originalExitUTC|side(+1/-1)|units|absoluteStopTrigger.
    /// This is NOT a close-only order on hedged accounts: those accounts are refused.
    /// Freeze limit range/fallback, use an initially isolated account, and include all
    /// partial fills/nonfills. Pending cancellation must be acknowledged before market
    /// fallback, preventing a late offset from opening a reversed position.
    /// Broker semantics reference is required; API acceptance alone is not an empirical
    /// execution audit. Backtest only; no live protective-order changes are authorized.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdStopLimitProtection : Robot
    {
        public enum Branch { OrdinaryStop, NettedStopLimit }
        [Parameter("Branch",DefaultValue=Branch.NettedStopLimit)] public Branch Variant {get;set;}
        [Parameter("Frozen entries",DefaultValue="")] public string Entries {get;set;}
        [Parameter("Broker netting/stop-limit semantics reference",DefaultValue="")] public string BrokerSemanticsReference {get;set;}
        [Parameter("Frozen stop-limit range (pips)",DefaultValue=-1.0)] public double LimitRangePips {get;set;}
        [Parameter("Frozen fallback seconds after observed trigger",DefaultValue=0.0)] public double FallbackSeconds {get;set;}
        [Parameter("Actual account equity floor (currency)",DefaultValue=0.0)] public double EquityFloor {get;set;}
        [Parameter("Initial stop-risk ceiling (currency)",DefaultValue=0.0)] public double InitialRiskCeiling {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T101")] public string PositionLabel {get;set;}
        private class Opportunity { public DateTime Receipt,Exit; public int Side; public double Units,Stop; }
        private class Trial
        {
            public Opportunity Event; public int PositionId;
            public string EntryLabel,OffsetLabel;
            public DateTime Trigger=DateTime.MinValue;
            public bool ForceClose,OffsetNotified,BarrierHit,PositionClosed;
            public double MinimumEquity,ActualUnits;
            public readonly HashSet<long> ReportedDeals=new HashSet<long>();
        }
        private readonly List<Opportunity> _events=new List<Opportunity>();
        private Trial _trial;
        private int _next;
        private string _prefix;
        private bool _halt;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T101_STOP,reason=research_only_no_live_changes");Stop();return;}
            try
            {
                if(Account.AccountType!=AccountType.Netted||Positions.Count!=0||PendingOrders.Count!=0||string.IsNullOrWhiteSpace(BrokerSemanticsReference)||!Finite(LimitRangePips)||LimitRangePips<0||!Finite(FallbackSeconds)||FallbackSeconds<=0||!Finite(EquityFloor)||EquityFloor<=0||EquityFloor>=Account.Equity||!Finite(InitialRiskCeiling)||InitialRiskCeiling<=0)
                    throw new FormatException("Verified NETTED semantics, isolated account, frozen range/fallback and actual currency limits required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (Entries??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=5)throw new FormatException("Five frozen-entry columns required");
                    var side=Number(c[2]);var ev=new Opportunity{Receipt=Utc(c[0]),Exit=Utc(c[1]),Side=(int)side,Units=Number(c[3]),Stop=Number(c[4])};
                    if(!seen.Add(ev.Receipt)||ev.Exit<=ev.Receipt||(side!=1&&side!=-1)||ev.Stop<=0||ev.Units<Symbol.VolumeInUnitsMin||ev.Units>Symbol.VolumeInUnitsMax||Symbol.NormalizeVolumeInUnits(ev.Units,RoundingMode.Down)!=ev.Units)throw new FormatException("Invalid frozen entry or unrepresentable size");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Frozen opportunities required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T101_STOP,error={0}",e.Message);Stop();return;}
            _prefix=PositionLabel+"_"+Variant+"_";
            Positions.Closed+=Closed;PendingOrders.Filled+=OffsetFilled;
            Print("T101_START,branch={0},opportunities={1},range_pips={2},fallback_seconds={3},semantics_reference={4}",Variant,_events.Count,LimitRangePips,FallbackSeconds,BrokerSemanticsReference);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            if(_trial!=null)
            {
                var t=_trial;t.MinimumEquity=Math.Min(t.MinimumEquity,Account.Equity);
                var p=Positions.FirstOrDefault(x=>x.Id==t.PositionId);
                var outside=Positions.Any(x=>x.Id!=t.PositionId)||PendingOrders.Any(o=>o.Label!=t.OffsetLabel);
                if(outside){_halt=true;t.ForceClose=true;Print("T101_ISOLATION_INVALID,quote={0:O},outside_account_activity=1",now);}
                if(Account.Equity<=EquityFloor){t.BarrierHit=true;t.ForceClose=true;_halt=true;}
                if(ValidQuote()&&p!=null)
                {
                    var executable=t.Event.Side>0?Symbol.Bid:Symbol.Ask;
                    if(t.Trigger==DateTime.MinValue&&t.Event.Side*(executable-t.Event.Stop)<=0)
                    {
                        t.Trigger=now;Print("T101_TRIGGER_QUOTE,receipt={0:O},quote={1:O},trigger={2},executable={3}",t.Event.Receipt,now,t.Event.Stop,executable);
                    }
                    var pending=PendingOrders.Where(o=>o.Label==t.OffsetLabel).ToArray();
                    if(pending.Any(o=>o.VolumeInUnits>p.VolumeInUnits+1e-9))
                    {
                        t.ForceClose=true;Print("T101_PARTIAL_REMAINDER_GUARD,position_units={0},reason=oversized_offset_could_reverse",p.VolumeInUnits);
                    }
                    Print("T101_OPEN_PATH,quote={0:O},equity={1},remaining_units={2},pending_count={3},observed_trigger={4},barrier_hit={5}",now,Account.Equity,p.VolumeInUnits,pending.Length,t.Trigger!=DateTime.MinValue,t.BarrierHit);
                }
                ReportDeals(t);
                var due=t.ForceClose||now>=t.Event.Exit||(t.Trigger!=DateTime.MinValue&&now>=t.Trigger.AddSeconds(FallbackSeconds));
                if(p==null)
                {
                    if(CancelOffsets(t,"original_position_closed"))FinishWhenHistoryComplete(t);
                }
                else if(due)
                {
                    // A cancel rejection is NOT permission to market-close and leave a
                    // possible late reverse order. Keep exposure visible and retry.
                    if(CancelOffsets(t,"fallback_or_original_exit"))
                    {
                        var result=ClosePosition(p);
                        Print("T101_FALLBACK,id={0},quote={1:O},remaining_units={2},success={3},error={4},barrier_hit={5}",p.Id,now,p.VolumeInUnits,result.IsSuccessful,result.Error,t.BarrierHit);
                        if(result.IsSuccessful){ReportDeals(t);FinishWhenHistoryComplete(t);}
                    }
                }
            }
            while(_next<_events.Count&&_events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if(Account.Equity<=EquityFloor)_halt=true;
                if(_halt||_trial!=null||Positions.Count!=0||PendingOrders.Count!=0){Skip(ev,"halted_or_not_isolated");continue;}
                if(!ValidQuote()||(now-ev.Receipt).TotalSeconds>2||now>=ev.Exit){Skip(ev,"missing_timely_executable_quote");continue;}
                var entry=ev.Side>0?Symbol.Ask:Symbol.Bid;var distance=ev.Side*(entry-ev.Stop);
                if(distance<=Symbol.Ask-Symbol.Bid||Symbol.AmountRisked(ev.Units,distance/Symbol.PipSize)>InitialRiskCeiling){Skip(ev,"stop_geometry_or_risk_ceiling");continue;}
                var entryLabel=_prefix+_next;var offsetLabel=entryLabel+"_OFFSET";
                var result=ExecuteMarketOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,ev.Units,entryLabel,Variant==Branch.OrdinaryStop?(double?)(distance/Symbol.PipSize):null,(double?)null,"T101 frozen entry");
                if(!result.IsSuccessful||result.Position==null){Skip(ev,"entry_rejected_"+result.Error);continue;}
                _trial=new Trial{Event=ev,PositionId=result.Position.Id,EntryLabel=entryLabel,OffsetLabel=offsetLabel,MinimumEquity=Account.Equity,ActualUnits=result.Position.VolumeInUnits};
                if(result.Position.VolumeInUnits!=ev.Units){_trial.ForceClose=true;Print("T101_ENTRY_PARTIAL,requested={0},filled={1},comparison_invalid=1",ev.Units,result.Position.VolumeInUnits);}
                if(Variant==Branch.OrdinaryStop)
                {
                    var protection=ModifyPosition(result.Position,(double?)ev.Stop,(double?)null,ProtectionType.Absolute);
                    if(!protection.IsSuccessful)_trial.ForceClose=true;
                }
                else
                {
                    var offset=PlaceStopLimitOrder(ev.Side>0?TradeType.Sell:TradeType.Buy,SymbolName,result.Position.VolumeInUnits,ev.Stop,LimitRangePips,offsetLabel,(double?)null,(double?)null,ProtectionType.Absolute,ev.Exit,"T101 NETTED offset protection");
                    Print("T101_OFFSET_SUBMIT,receipt={0:O},trigger={1},range_pips={2},units={3},success={4},error={5}",ev.Receipt,ev.Stop,LimitRangePips,result.Position.VolumeInUnits,offset.IsSuccessful,offset.Error);
                    if(!offset.IsSuccessful)_trial.ForceClose=true;
                }
                Print("T101_ENTRY,receipt={0:O},quote={1:O},id={2},filled_price={3},units={4},trigger={5},original_exit={6:O}",ev.Receipt,now,result.Position.Id,result.Position.EntryPrice,result.Position.VolumeInUnits,ev.Stop,ev.Exit);
            }
        }
        private bool CancelOffsets(Trial t,string reason)
        {
            var successful=true;
            foreach(var pending in PendingOrders.Where(o=>o.Label==t.OffsetLabel).ToArray())
            {
                var result=CancelPendingOrder(pending);successful&=result.IsSuccessful;
                Print("T101_CANCEL_ACK,order={0},reason={1},success={2},error={3}",pending.Id,reason,result.IsSuccessful,result.Error);
            }
            return successful&&!PendingOrders.Any(o=>o.Label==t.OffsetLabel);
        }
        private void OffsetFilled(PendingOrderFilledEventArgs args)
        {
            if(_trial==null||args.PendingOrder.Label!=_trial.OffsetLabel)return;
            _trial.OffsetNotified=true;
            if(_trial.Trigger==DateTime.MinValue)_trial.Trigger=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            Print("T101_NATIVE_OFFSET_FILL_NOTIFICATION,order={0},quote={1:O},history_required_for_completion=1",args.PendingOrder.Id,Server.Time);
        }
        private void Closed(PositionClosedEventArgs args)
        {
            if(_trial==null||args.Position.Id!=_trial.PositionId)return;
            _trial.PositionClosed=true;
            if(args.Reason==PositionCloseReason.StopLoss&&_trial.Trigger==DateTime.MinValue)_trial.Trigger=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
        }
        private void ReportDeals(Trial t)
        {
            foreach(var deal in History.FindByPositionId(t.PositionId))
                if(t.ReportedDeals.Add(deal.ClosingDealId))
                    Print("T101_ACTUAL_CLOSE,receipt={0:O},deal={1},time={2:O},units={3},price={4},adverse_trigger_slippage_price={5},net={6}",t.Event.Receipt,deal.ClosingDealId,deal.ClosingTime,deal.VolumeInUnits,deal.ClosingPrice,t.Event.Side*(t.Event.Stop-deal.ClosingPrice),deal.NetProfit);
        }
        private void FinishWhenHistoryComplete(Trial t)
        {
            if(Positions.Any(p=>p.Id==t.PositionId)||PendingOrders.Any(o=>o.Label==t.OffsetLabel))return;
            var deals=History.FindByPositionId(t.PositionId);var completed=deals.Sum(d=>d.VolumeInUnits);
            if(completed<t.ActualUnits-1e-8)return;
            Print("T101_COMPLETE,receipt={0:O},branch={1},initial_units={2},closed_units={3},native_offset_notification={4},minimum_account_equity={5},barrier_hit={6},net={7},all_nonfills_were_retained_in_open_path=1",t.Event.Receipt,Variant,t.Event.Units,completed,t.OffsetNotified,t.MinimumEquity,t.BarrierHit,deals.Sum(d=>d.NetProfit));
            _trial=null;
        }
        private void Skip(Opportunity ev,string reason)=>Print("T101_SKIP,receipt={0:O},reason={1},original_opportunity_counted=1",ev.Receipt,reason);
        private bool ValidQuote()=>Finite(Symbol.Bid)&&Finite(Symbol.Ask)&&Symbol.Bid>0&&Symbol.Ask>=Symbol.Bid;
        private static DateTime Utc(string s)=>DateTime.ParseExact(s.Trim(),new[]{"yyyy-MM-ddTHH:mm:ss'Z'","yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string s){var n=double.Parse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite input");return n;}
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        protected override void OnStop()
        {
            Positions.Closed-=Closed;PendingOrders.Filled-=OffsetFilled;
            if(_trial!=null){CancelOffsets(_trial,"run_end");Print("T101_CENSORED,receipt={0:O},position_open={1},pending_open={2},minimum_equity={3}",_trial.Event.Receipt,Positions.Any(p=>p.Id==_trial.PositionId),PendingOrders.Any(o=>o.Label==_trial.OffsetLabel),_trial.MinimumEquity);}
        }
    }
}
