using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T053: At 10:00 New York short only after a positive 09:30-10:00 observed fixed-maturity TIPS real-yield change. Nominal yield is logged as a separate control, not substituted for real yield. Received executable TIPS quotes require a frozen age cutoff and liquidity controls; exclude restricted news upstream. Fixed 10:30 exit.
    /// Input rows: receiptUTC|exitUTC|protectiveDistanceDollars|completed_tips_real_yield_change,nominal_yield_change,dollar_return,gold_return,tips_liquidity,received_tips_age_seconds,frozen_maximum_quote_age_seconds.
    /// UTC format yyyy-MM-ddTHH:mm:ssZ; rows separated by semicolon or newline.
    /// All features are completed measurements received before receipt on audited feeds;
    /// this adapter performs the hypothesis's sign/eligibility calculation, not data acquisition.
    /// Same eligible events for opposite/random controls. Fixed account-currency risk with
    /// attached protection, no target, original fixed exit. Backtest only; broker tick data.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdRealYieldOpportunityCost : Robot
    {
        public enum TestBranch { Primary, Opposite, Random  }
        [Parameter("Branch",DefaultValue=TestBranch.Primary)] public TestBranch Variant {get;set;}
        [Parameter("Point-in-time feature rows",DefaultValue="")] public string FeatureRows {get;set;}
        [Parameter("Risk per event (account currency)",DefaultValue=50.0,MinValue=0.01)] public double RiskBudget {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T053")] public string PositionLabel {get;set;}
        
        private sealed class Event { public DateTime Receipt,Exit; public double StopDistance; public double[] Values; }
        private readonly List<Event> _events=new List<Event>();
        private readonly Dictionary<int,DateTime> _deadlines=new Dictionary<int,DateTime>();
        private string _label;
        private int _next;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T053_STOP,reason=research_only");Stop();return;}
            try
            {
                if(RiskBudget<=0 || !Finite(RiskBudget))throw new FormatException("Positive finite risk required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (FeatureRows??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');
                    if(c.Length!=7+3)throw new FormatException("Expected 7 features plus receipt, exit, stop distance");
                    var ev=new Event{Receipt=Utc(c[0]),Exit=Utc(c[1]),StopDistance=Number(c[2]),Values=new double[7]};
                    if(!seen.Add(ev.Receipt) || ev.Exit-ev.Receipt!=TimeSpan.FromMinutes(30) || ev.StopDistance<=0)throw new FormatException("Duplicate receipt, incorrect horizon or invalid stop distance");
                    for(int i=0;i<ev.Values.Length;i++)ev.Values[i]=Number(c[i+3]);
                    var v=ev.Values;
                    if(v[4]<0 || v[5]<0 || v[6]<=0 || v[5]>v[6])throw new FormatException("Invalid or stale TIPS quote"); var ny=TimeZoneInfo.ConvertTimeFromUtc(ev.Receipt,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));if(ny.TimeOfDay!=new TimeSpan(10,0,0))throw new FormatException("Decision must be 10:00 NY");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Audited point-in-time features required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T053_STOP,reason=configuration,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            Positions.Closed+=Closed;
            Print("T053_START,events={0},branch={1},horizon_minutes=30,fields=completed_tips_real_yield_change,nominal_yield_change,dollar_return,gold_return,tips_liquidity,received_tips_age_seconds,frozen_maximum_quote_age_seconds",_events.Count,Variant);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            foreach(var position in Positions.FindAll(_label,SymbolName))
            {
                DateTime deadline;
                if(_deadlines.TryGetValue(position.Id,out deadline) && now>=deadline)
                {
                    var close=ClosePosition(position);
                    Print("T053_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",position.Id,deadline,now,close.IsSuccessful,close.Error);
                }
            }
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"late_quote");continue;}
                if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid){Skip(ev,"invalid_quote");continue;}
                if(Positions.FindAll(_label,SymbolName).Length>0){Skip(ev,"overlapping_event");continue;}
                var side=Choose(ev.Values);
                Print("T053_FEATURES,receipt={0:O},features={1},hypothesis_side={2}",ev.Receipt,string.Join("|",Array.ConvertAll(ev.Values,x=>x.ToString("R",CultureInfo.InvariantCulture))),side);
                if(side==0){Skip(ev,"hypothesis_not_eligible");continue;}
                if(Variant==TestBranch.Opposite)side=-side;
                else if(Variant==TestBranch.Random)side=StableSign(ev.Receipt);
                var stopPips=ev.StopDistance/Symbol.PipSize;
                var volume=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(RiskBudget,stopPips,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"risk_volume_below_minimum");continue;}
                var requested=side>0?Symbol.Ask:Symbol.Bid;
                var result=ExecuteMarketOrder(side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,stopPips,(double?)null,"T053 point-in-time features");
                if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                _deadlines[result.Position.Id]=ev.Exit;
                Print("T053_ENTRY,receipt={0:O},quote={1:O},id={2},side={3},requested={4},filled={5},spread={6},volume={7},deadline={8:O}",ev.Receipt,now,result.Position.Id,side,requested,result.Position.EntryPrice,Symbol.Ask-Symbol.Bid,volume,ev.Exit);
            }
        }
        private int Choose(double[] v)
        {
            return v[0]>0 ? -1 : 0;
        }
        private void Closed(PositionClosedEventArgs args)
        {
            if(args.Position.Label!=_label||args.Position.SymbolName!=SymbolName)return;
            _deadlines.Remove(args.Position.Id);
            Print("T053_CLOSED,id={0},net={1},reason={2}",args.Position.Id,args.Position.NetProfit,args.Reason);
        }
        private void Skip(Event ev,string reason)=>Print("T053_SKIP,receipt={0:O},reason={1},opportunity_pnl=0",ev.Receipt,reason);
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite feature");return n;}
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        private static int StableSign(DateTime time){unchecked{ulong x=(ulong)time.Ticks;x^=x>>12;x^=x<<25;x^=x>>27;return (x&1)==0?1:-1;}}
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            Print("T053_STOP,unreached={0},open={1}",_events.Count-_next,_label==null?0:Positions.FindAll(_label,SymbolName).Length);
        }
    }
}
