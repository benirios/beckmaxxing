using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T065: At auction start +5 minutes compare completed versus still-running auctions.
    /// Baseline executes unchanged signals; Gated waits for a COMPLETION RECEIPT,
    /// capped at five minutes and the original exit. Never classify by eventual duration.
    /// Rows: receiptUTC|exitUTC|side|absoluteStop|absoluteTarget(0=none)|runningState|positiveUnusedField|secondsSinceAuctionStart|completionStatusReceived(0=running,1=complete)|initialMove|initialSpread.
    /// Separate status updates: receiptUTC|auctionStartUTC|isRunning(0/1).
    /// One row per line or semicolon. All data must be known by receipt. Gating supplies no alpha.
    /// Backtest with broker ticks, same opportunities, and declared actual spread/commission/slippage.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdAuctionCompletionWait : Robot
    {
        public enum Branch { Baseline, Gated, FixedFiveMinuteDelay }
        [Parameter("Branch",DefaultValue=Branch.Gated)] public Branch Variant {get;set;}
        [Parameter("Frozen opportunities",DefaultValue="")] public string Opportunities {get;set;}
        [Parameter("Dollar risk per original opportunity",DefaultValue=50.0,MinValue=0.01)] public double DollarRisk {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T065")] public string PositionLabel {get;set;}
        [Parameter("Auction status updates",DefaultValue="")] public string StatusUpdates {get;set;}
        private class Status { public DateTime Receipt,AuctionStart; public bool Running; }
        private readonly List<Status> _statuses=new List<Status>();
        private readonly HashSet<DateTime> _completed=new HashSet<DateTime>();
        private int _nextStatus;
        private class Opportunity { public DateTime Receipt,Exit; public int Side,State; public double Stop,Target,Limit,OriginalMid; public DateTime AuctionStart; public double[] Features; }
        private readonly List<Opportunity> _events=new List<Opportunity>();
        private readonly Dictionary<int,DateTime> _exits=new Dictionary<int,DateTime>();
        private readonly List<Opportunity> _waiting=new List<Opportunity>();
        private int _next;
        private string _label;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T065_STOP,reason=research_only");Stop();return;}
            try
            {
                var seen=new HashSet<DateTime>();
                foreach(var row in (Opportunities??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var cells=row.Trim().Split('|');
                    if(cells.Length!=11)throw new FormatException("Eleven columns required");
                    var ev=new Opportunity{Receipt=ParseUtc(cells[0]),Exit=ParseUtc(cells[1])};
                    var side=Number(cells[2]);var state=Number(cells[5]);
                    ev.Stop=Number(cells[3]);ev.Target=Number(cells[4]);ev.Limit=Number(cells[6]);
                    if(!seen.Add(ev.Receipt) || ev.Exit<=ev.Receipt || (side!=1 && side!=-1) || (state!=0 && state!=1) || ev.Stop<=0 || ev.Target<0 || ev.Limit<=0)
                        throw new FormatException("Invalid or duplicate frozen opportunity");
                    ev.Side=(int)side;ev.State=(int)state;
                    ev.Features=new double[4];for(int i=0;i<4;i++)ev.Features[i]=Number(cells[i+7]);
                    var v=ev.Features;
                    if(v[0]!=300||(v[1]!=0&&v[1]!=1)||v[3]<0||ev.State!=(v[1]==0?1:0))throw new FormatException("Requires actual auction +5 received status");
                    ev.AuctionStart=ev.Receipt.AddSeconds(-v[0]);
                    _events.Add(ev);
                }
                var statusKeys=new HashSet<string>();
                foreach(var row in (StatusUpdates??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=3)throw new FormatException("Status needs receipt, auction start and running flag");
                    var receipt=ParseUtc(c[0]);var start=ParseUtc(c[1]);var running=Number(c[2]);
                    if(receipt<start || (running!=0&&running!=1) || !statusKeys.Add(receipt.Ticks+"_"+start.Ticks))throw new FormatException("Invalid status receipt");
                    _statuses.Add(new Status{Receipt=receipt,AuctionStart=start,Running=running==1});
                }
                _statuses.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
                if(_events.Count==0 || DollarRisk<=0 || double.IsNaN(DollarRisk) || double.IsInfinity(DollarRisk)) throw new FormatException("Valid frozen opportunities and positive dollar risk required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T065_STOP,reason=configuration,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            Positions.Closed+=OnClosed;
            Print("T065_START,branch={0},opportunities={1},dollar_risk={2}",Variant,_events.Count,DollarRisk);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            while(_nextStatus<_statuses.Count && _statuses[_nextStatus].Receipt<now)
            {
                var status=_statuses[_nextStatus++];
                if(!status.Running)_completed.Add(status.AuctionStart);
                Print("T065_STATUS,receipt={0:O},auction_start={1:O},running={2},first_broker_quote={3:O}",status.Receipt,status.AuctionStart,status.Running,now);
            }
            foreach(var position in Positions.FindAll(_label,SymbolName))
            {
                DateTime exit;
                if(_exits.TryGetValue(position.Id,out exit) && now>=exit)
                {
                    var result=ClosePosition(position);
                    Print("T065_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",position.Id,exit,now,result.IsSuccessful,result.Error);
                    // Failed closes retain their deadline and retry next tick.
                }
            }
            if(Symbol.Bid<=0 || Symbol.Ask<Symbol.Bid || double.IsNaN(Symbol.Bid) || double.IsNaN(Symbol.Ask) || double.IsInfinity(Symbol.Bid) || double.IsInfinity(Symbol.Ask))return;
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"missing_timely_signal_quote");continue;}
                ev.OriginalMid=(Symbol.Bid+Symbol.Ask)/2;
                Print("T065_INITIAL_STATE,receipt={0:O},auction_start={1:O},running={2},initial_move={3},initial_spread={4}",ev.Receipt,ev.AuctionStart,ev.State,ev.Features[2],ev.Features[3]);
                if(Variant==Branch.Gated && (false)){Skip(ev,"frozen_gate_exclusion");continue;}
                _waiting.Add(ev);
            }
            for(int i=_waiting.Count-1;i>=0;i--)
            {
                var ev=_waiting[i];
                var delay=Variant==Branch.FixedFiveMinuteDelay;
                var spreadGate=Variant==Branch.Gated && (ev.State==1);
                if(now>=ev.Exit || (spreadGate && now>ev.Receipt.AddMinutes(5))){Skip(ev,"gate_expired_original_exit_preserved");_waiting.RemoveAt(i);continue;}
                if(delay && now<ev.Receipt.AddMinutes(5))continue;
                if(delay && (now-ev.Receipt.AddMinutes(5)).TotalSeconds>2){Skip(ev,"missing_delay_quote");_waiting.RemoveAt(i);continue;}
                if(spreadGate && !_completed.Contains(ev.AuctionStart))continue;
                _waiting.RemoveAt(i);
                if(Positions.FindAll(_label,SymbolName).Length>0){Skip(ev,"overlapping_position");continue;}
                Enter(ev,now);
            }
        }
        private void Enter(Opportunity ev,DateTime now)
        {
            var entry=ev.Side>0?Symbol.Ask:Symbol.Bid;
            var stopDistance=ev.Side*(entry-ev.Stop);
            var targetDistance=ev.Side*(ev.Target-entry);
            if(stopDistance<=0 || (ev.Target>0 && targetDistance<=0)){Skip(ev,"stop_or_target_crossed_during_wait");return;}
            var stopPips=stopDistance/Symbol.PipSize;
            var volume=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(DollarRisk,stopPips,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
            if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"risk_volume_below_minimum");return;}
            var result=ExecuteMarketOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,stopPips,ev.Target>0?(double?)(targetDistance/Symbol.PipSize):null,"T065 frozen opportunity");
            if(!result.IsSuccessful || result.Position==null){Skip(ev,"order_failed_"+result.Error);return;}
            _exits[result.Position.Id]=ev.Exit;
            // The initial order already carries protection. Restore original absolute levels
            // after fill so waiting never changes the underlying strategy's stop or target.
            var protection=ModifyPosition(result.Position,ev.Stop,ev.Target>0?(double?)ev.Target:null,ProtectionType.Absolute);
            var actualRisk=Symbol.AmountRisked(volume,Math.Abs(result.Position.EntryPrice-ev.Stop)/Symbol.PipSize);
            if(!protection.IsSuccessful || actualRisk>DollarRisk*1.01)
            {
                _exits[result.Position.Id]=now;
                var close=ClosePosition(result.Position);
                Print("T065_ABORT,id={0},reason=protection_or_risk,protection_success={1},actual_risk={2},close_success={3}",result.Position.Id,protection.IsSuccessful,actualRisk,close.IsSuccessful);
                return;
            }
            Print("T065_ENTRY,receipt={0:O},quote={1:O},id={2},state={3},spread={4},wait_seconds={5},mid_movement={6},adverse_slippage={7},risk={8},exit={9:O}",ev.Receipt,now,result.Position.Id,ev.State,Symbol.Ask-Symbol.Bid,(now-ev.Receipt).TotalSeconds,(Symbol.Bid+Symbol.Ask)/2-ev.OriginalMid,ev.Side*(result.Position.EntryPrice-entry),actualRisk,ev.Exit);
        }
        private void Skip(Opportunity ev,string reason){Print("T065_SKIP,receipt={0:O},state={1},reason={2},opportunity_pnl=0",ev.Receipt,ev.State,reason);}
        private void OnClosed(PositionClosedEventArgs args)
        {
            if(args.Position.Label!=_label || args.Position.SymbolName!=SymbolName)return;
            _exits.Remove(args.Position.Id);
            Print("T065_CLOSED,id={0},reason={1},net={2}",args.Position.Id,args.Reason,args.Position.NetProfit);
        }
        private static DateTime ParseUtc(string text)=>DateTime.ParseExact(text.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string text){var value=double.Parse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(double.IsNaN(value)||double.IsInfinity(value))throw new FormatException("Nonfinite input");return value;}
        protected override void OnStop()
        {
            Positions.Closed-=OnClosed;
            foreach(var ev in _waiting)Print("T065_CENSORED,receipt={0:O},reason=run_ended_while_waiting",ev.Receipt);
            Print("T065_STOP,open_positions={0},unreached_opportunities={1}",_label==null?0:Positions.FindAll(_label,SymbolName).Length,_events.Count-_next);
        }
    }
}
