using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T046: market entry versus same-side quote limit (buy bid / sell ask),
    /// 30-second native expiry, no market fallback. Frozen exposure, hard protection,
    /// and original exit. Never infer a limit fill from an OHLC touch.
    /// Rows: receiptUTC|exitUTC|side(+1/-1)|units|absoluteStop|absoluteTarget(0=none).
    /// All original opportunities including unfilled/rejected orders are reported.
    /// Quote markouts at 5/15/30 seconds after actual fill distinguish selection from
    /// spread saved. Use actual broker fills for empirical claims. Backtest only.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdPassiveEntrySelection : Robot
    {
        public enum Branch { Market, PassiveNoFallback }
        [Parameter("Branch",DefaultValue=Branch.PassiveNoFallback)] public Branch Variant {get;set;}
        [Parameter("Frozen opportunities",DefaultValue="")] public string Opportunities {get;set;}
        [Parameter("Risk ceiling (account currency)",DefaultValue=50.0,MinValue=0.01)] public double RiskBudget {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T046")] public string PositionLabel {get;set;}
        private class Opportunity { public DateTime Receipt,Exit; public int Side; public double Units,Stop,Target,ArrivalMid; public bool ExpiryReported,ForceClose,Filled; }
        private class Markout { public DateTime Fill; public int Side,Id,Next; public double Price; }
        private readonly List<Opportunity> _events=new List<Opportunity>();
        private readonly Dictionary<string,Opportunity> _owned=new Dictionary<string,Opportunity>();
        private readonly List<Markout> _markouts=new List<Markout>();
        private int _next;
        private string _prefix;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T046_STOP,reason=research_only");Stop();return;}
            try
            {
                if(RiskBudget<=0 || !Finite(RiskBudget))throw new FormatException("Positive risk ceiling required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (Opportunities??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=6)throw new FormatException("Six columns required");
                    var side=Number(c[2]);var ev=new Opportunity{Receipt=Utc(c[0]),Exit=Utc(c[1]),Side=(int)side,Units=Number(c[3]),Stop=Number(c[4]),Target=Number(c[5])};
                    if(!seen.Add(ev.Receipt)||ev.Exit<=ev.Receipt.AddSeconds(30)||(side!=1&&side!=-1)||ev.Units<Symbol.VolumeInUnitsMin||ev.Units>Symbol.VolumeInUnitsMax||Math.Abs(Symbol.NormalizeVolumeInUnits(ev.Units,RoundingMode.Down)-ev.Units)>1e-8||ev.Stop<=0||ev.Target<0)throw new FormatException("Invalid opportunity or nonrepresentable size");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Frozen opportunities required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T046_STOP,error={0}",e.Message);Stop();return;}
            _prefix=PositionLabel+"_"+Variant+"_";
            Positions.Opened+=Opened;Positions.Closed+=Closed;
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            foreach(var item in _owned.ToArray())
            {
                var ev=item.Value;
                if(now>=ev.Receipt.AddSeconds(30))
                {
                    var pending=PendingOrders.Where(o=>o.Label==item.Key && o.SymbolName==SymbolName).ToArray();
                    foreach(var order in pending)
                    {
                        var cancel=CancelPendingOrder(order);
                        Print("T046_CANCEL,receipt={0:O},id={1},success={2},error={3}",ev.Receipt,order.Id,cancel.IsSuccessful,cancel.Error);
                    }
                    if(!ev.ExpiryReported)
                    {
                        ev.ExpiryReported=true;
                        var unresolved=PendingOrders.Any(o=>o.Label==item.Key && o.SymbolName==SymbolName);
                        Print("T046_EXPIRY,receipt={0:O},quote={1:O},pending_after_expiry={2},filled={3}",ev.Receipt,now,unresolved,ev.Filled);
                        if(!unresolved && !ev.Filled)Print("T046_UNFILLED,receipt={0:O},units={1},opportunity_pnl=0",ev.Receipt,ev.Units);
                    }
                }
                if(now>=ev.Exit || ev.ForceClose)
                    foreach(var p in Positions.FindAll(item.Key,SymbolName))
                    {
                        var close=ClosePosition(p);Print("T046_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",p.Id,ev.Exit,now,close.IsSuccessful,close.Error);
                    }
            }
            if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid)return;
            var mid=(Symbol.Bid+Symbol.Ask)/2;
            for(int i=_markouts.Count-1;i>=0;i--)
            {
                var m=_markouts[i];var offsets=new[]{5,15,30};
                while(m.Next<offsets.Length && now>=m.Fill.AddSeconds(offsets[m.Next]))
                {
                    var delay=(now-m.Fill.AddSeconds(offsets[m.Next])).TotalSeconds;
                    Print("T046_MARKOUT,id={0},seconds={1},valid={2},signed_mid_return_dollars={3}",m.Id,offsets[m.Next],delay<=2,m.Side*(mid-m.Price));m.Next++;
                }
                if(m.Next==offsets.Length)_markouts.RemoveAt(i);
            }
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"late_quote");continue;}
                if(Positions.Any(p=>p.SymbolName==SymbolName && p.Label!=null && p.Label.StartsWith(_prefix)) || PendingOrders.Any(o=>o.SymbolName==SymbolName && o.Label!=null && o.Label.StartsWith(_prefix)))
                {Skip(ev,"overlapping_opportunity");continue;}
                var market=ev.Side>0?Symbol.Ask:Symbol.Bid;
                var limit=ev.Side>0?Symbol.Bid:Symbol.Ask;
                var requested=Variant==Branch.Market?market:limit;
                var distance=ev.Side*(requested-ev.Stop);
                var conservativeRisk=Symbol.AmountRisked(ev.Units,Math.Max(0,ev.Side*(market-ev.Stop))/Symbol.PipSize);
                if(distance<=0 || (ev.Target>0 && ev.Side*(ev.Target-requested)<=0) || conservativeRisk>RiskBudget)
                {Skip(ev,"geometry_or_risk_ceiling");continue;}
                ev.ArrivalMid=mid;var label=_prefix+ev.Receipt.Ticks;_owned[label]=ev;
                TradeResult result;
                if(Variant==Branch.PassiveNoFallback)
                    result=PlaceLimitOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,ev.Units,limit,label,(double?)ev.Stop,ev.Target>0?(double?)ev.Target:null,ProtectionType.Absolute,ev.Receipt.AddSeconds(30),"T046 no fallback");
                else
                {
                    result=ExecuteMarketOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,ev.Units,label,distance/Symbol.PipSize,ev.Target>0?(double?)(ev.Side*(ev.Target-market)/Symbol.PipSize):null,"T046 market control");
                    if(result.IsSuccessful && result.Position!=null)
                    {
                        var protection=ModifyPosition(result.Position,ev.Stop,ev.Target>0?(double?)ev.Target:null,ProtectionType.Absolute);
                        if(!protection.IsSuccessful)ev.ForceClose=true;
                    }
                }
                Print("T046_SUBMIT,receipt={0:O},branch={1},quote={2:O},requested={3},arrival_mid={4},spread={5},units={6},success={7},error={8}",ev.Receipt,Variant,now,requested,mid,Symbol.Ask-Symbol.Bid,ev.Units,result.IsSuccessful,result.Error);
                if(!result.IsSuccessful)Skip(ev,"order_rejected_"+result.Error);
            }
        }
        private void Opened(PositionOpenedEventArgs args)
        {
            var p=args.Position;Opportunity ev;
            if(p.SymbolName!=SymbolName||p.Label==null||!_owned.TryGetValue(p.Label,out ev))return;
            ev.Filled=true;
            if(Variant==Branch.PassiveNoFallback && p.EntryTime>=ev.Receipt.AddSeconds(30))ev.ForceClose=true;
            _markouts.Add(new Markout{Fill=p.EntryTime,Side=ev.Side,Id=p.Id,Price=p.EntryPrice});
            var risk=Symbol.AmountRisked(p.VolumeInUnits,Math.Abs(p.EntryPrice-ev.Stop)/Symbol.PipSize);
            if(risk>RiskBudget*1.01)ev.ForceClose=true;
            Print("T046_FILL,receipt={0:O},id={1},fill={2:O},price={3},price_shortfall={4},risk={5}",ev.Receipt,p.Id,p.EntryTime,p.EntryPrice,ev.Side*(p.EntryPrice-ev.ArrivalMid)*p.VolumeInUnits,risk);
        }
        private void Closed(PositionClosedEventArgs a){if(a.Position.SymbolName==SymbolName && a.Position.Label!=null && _owned.ContainsKey(a.Position.Label))Print("T046_CLOSED,id={0},net={1},reason={2}",a.Position.Id,a.Position.NetProfit,a.Reason);}
        private void Skip(Opportunity ev,string reason)=>Print("T046_SKIP,receipt={0:O},reason={1},opportunity_pnl=0",ev.Receipt,reason);
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite number");return n;}
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        protected override void OnStop(){Positions.Opened-=Opened;Positions.Closed-=Closed;foreach(var m in _markouts)Print("T046_CENSORED,id={0},reason=incomplete_markout",m.Id);}
    }
}
