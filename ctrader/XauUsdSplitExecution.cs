using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T045: identical frozen entry/exit/exposure, one market order versus five
    /// equal child orders at 0, 7.5, 15, 22.5 and 30 seconds from decision.
    /// Rows: receiptUTC|exitUTC|side(+1/-1)|totalUnits|absoluteStop|absoluteTarget(0=none).
    /// Full exposure must be exactly representable as five valid equal orders in BOTH
    /// branches. No fallback or catch-up for a missed child; every opportunity is logged.
    /// Original absolute protection, original exit, aggregate risk ceiling throughout.
    /// Tick backtest only. Simulation cannot establish actual size impact or fill quality.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdSplitExecution : Robot
    {
        public enum Branch { SingleOrder, FiveChildren }
        [Parameter("Branch",DefaultValue=Branch.FiveChildren)] public Branch Variant {get;set;}
        [Parameter("Frozen entries",DefaultValue="")] public string Entries {get;set;}
        [Parameter("Aggregate initial risk (account currency)",DefaultValue=50.0,MinValue=0.01)] public double RiskBudget {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T045")] public string PositionLabel {get;set;}
        private class Basket
        {
            public DateTime Receipt,Exit;
            public int Side,NextChild;
            public double Units,Stop,Target,ChildUnits,ArrivalMid,Filled,PriceVolume,RiskCommitted;
        }
        private readonly List<Basket> _events=new List<Basket>();
        private readonly Dictionary<int,DateTime> _exits=new Dictionary<int,DateTime>();
        private Basket _basket;
        private int _next;
        private string _label;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T045_STOP,reason=research_only");Stop();return;}
            try
            {
                if(RiskBudget<=0 || !Finite(RiskBudget))throw new FormatException("Positive finite risk required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (Entries??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=6)throw new FormatException("Six columns required");
                    var side=Number(c[2]);
                    var ev=new Basket{Receipt=Utc(c[0]),Exit=Utc(c[1]),Side=(int)side,Units=Number(c[3]),Stop=Number(c[4]),Target=Number(c[5])};
                    if(!seen.Add(ev.Receipt) || ev.Exit<=ev.Receipt.AddSeconds(30) || (side!=1 && side!=-1) || ev.Units<=0 || ev.Stop<=0 || ev.Target<0)throw new FormatException("Invalid basket");
                    ev.ChildUnits=Symbol.NormalizeVolumeInUnits(ev.Units/5,RoundingMode.Down);
                    if(ev.ChildUnits<Symbol.VolumeInUnitsMin || ev.Units>Symbol.VolumeInUnitsMax || Math.Abs(ev.ChildUnits*5-ev.Units)>1e-8)
                        throw new FormatException("Total size must be exactly five valid equal child sizes and a valid single size");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Frozen entry opportunities required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T045_STOP,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;Positions.Closed+=Closed;
            Print("T045_START,branch={0},opportunities={1}",Variant,_events.Count);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            foreach(var p in Positions.FindAll(_label,SymbolName))
            {
                DateTime exit;if(_exits.TryGetValue(p.Id,out exit) && now>=exit)
                {
                    var r=ClosePosition(p);Print("T045_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",p.Id,exit,now,r.IsSuccessful,r.Error);
                }
            }
            if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid)return;
            ProcessBasket(now);
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2 || _basket!=null || Positions.FindAll(_label,SymbolName).Length>0)
                { Print("T045_SKIP,receipt={0:O},reason=late_or_overlapping,unfilled_units={1}",ev.Receipt,ev.Units);continue; }
                ev.ArrivalMid=(Symbol.Bid+Symbol.Ask)/2;_basket=ev;
                ProcessBasket(now);
            }
        }
        private void ProcessBasket(DateTime now)
        {
            if(_basket==null)return;
            var ev=_basket;var count=Variant==Branch.SingleOrder?1:5;
            while(ev.NextChild<count)
            {
                var index=ev.NextChild;
                var due=ev.Receipt.AddSeconds(Variant==Branch.SingleOrder?0:7.5*index);
                if(now<due)break;
                ev.NextChild++;
                if((now-due).TotalSeconds>2 || now>=ev.Exit)
                {Print("T045_CHILD_SKIP,receipt={0:O},child={1},reason=missed_quote",ev.Receipt,index);continue;}
                var volume=Variant==Branch.SingleOrder?ev.Units:ev.ChildUnits;
                var price=ev.Side>0?Symbol.Ask:Symbol.Bid;
                var distance=ev.Side*(price-ev.Stop);
                var targetDistance=ev.Side*(ev.Target-price);
                var risk=Symbol.AmountRisked(volume,Math.Max(0,distance)/Symbol.PipSize);
                if(distance<=0 || (ev.Target>0 && targetDistance<=0) || ev.RiskCommitted+risk>RiskBudget)
                {Print("T045_CHILD_SKIP,receipt={0:O},child={1},reason=geometry_or_aggregate_risk",ev.Receipt,index);continue;}
                var result=ExecuteMarketOrder(ev.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,distance/Symbol.PipSize,ev.Target>0?(double?)(targetDistance/Symbol.PipSize):null,"T045 frozen basket");
                if(!result.IsSuccessful || result.Position==null)
                {Print("T045_REJECT,receipt={0:O},child={1},error={2}",ev.Receipt,index,result.Error);continue;}
                var p=result.Position;_exits[p.Id]=ev.Exit;
                var protection=ModifyPosition(p,ev.Stop,ev.Target>0?(double?)ev.Target:null,ProtectionType.Absolute);
                var actualRisk=Symbol.AmountRisked(volume,Math.Abs(p.EntryPrice-ev.Stop)/Symbol.PipSize);
                ev.RiskCommitted+=actualRisk;ev.Filled+=volume;ev.PriceVolume+=volume*p.EntryPrice;
                Print("T045_FILL,receipt={0:O},child={1},id={2},quote={3:O},volume={4},price={5},aggregate_initial_risk={6}",ev.Receipt,index,p.Id,now,volume,p.EntryPrice,ev.RiskCommitted);
                if(!protection.IsSuccessful || ev.RiskCommitted>RiskBudget*1.01)
                {
                    _exits[p.Id]=now;var close=ClosePosition(p);
                    ev.NextChild=count;
                    Print("T045_ABORT,id={0},close_success={1},reason=protection_or_risk",p.Id,close.IsSuccessful);
                }
            }
            if(ev.NextChild==count && now>=ev.Receipt.AddSeconds(30))
            {
                var mid=(Symbol.Bid+Symbol.Ask)/2;
                var filledShortfall=ev.Side*(ev.PriceVolume-ev.Filled*ev.ArrivalMid);
                var missedOpportunityCost=ev.Side*(mid-ev.ArrivalMid)*(ev.Units-ev.Filled);
                Print("T045_BASKET,receipt={0:O},intended_units={1},filled_units={2},unfilled_units={3},filled_price_shortfall={4},missed_mark_cost={5},mark_quote={6:O}",ev.Receipt,ev.Units,ev.Filled,ev.Units-ev.Filled,filledShortfall,missedOpportunityCost,now);
                _basket=null;
            }
        }
        private void Closed(PositionClosedEventArgs a){if(a.Position.Label!=_label||a.Position.SymbolName!=SymbolName)return;_exits.Remove(a.Position.Id);Print("T045_CLOSED,id={0},net={1},reason={2}",a.Position.Id,a.Position.NetProfit,a.Reason);}
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite value");return n;}
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        protected override void OnStop(){Positions.Closed-=Closed;if(_basket!=null)Print("T045_CENSORED,receipt={0:O},filled={1},intended={2}",_basket.Receipt,_basket.Filled,_basket.Units);}
    }
}
