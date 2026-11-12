using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T037: Dual simple moving-average trend rule on completed broker M5 closes. Fast and slow periods are REQUIRED in the audited opportunity rows, identical for the entire run, and must be frozen from the source economic timescale before inspecting broker results. No default periods or sweep are supplied. Decision clocks and one-hour intraday exits come from preregistered rows; UTC turnover cap is explicit. Broker server bars may be bid bars, not midpoint; declare bar provenance in the run ledger.
    /// Input rows: receiptUTC|exitUTC|protectiveDistanceDollars|fast_m5_periods,slow_m5_periods.
    /// UTC format yyyy-MM-ddTHH:mm:ssZ; rows separated by semicolon or newline.
    /// All features are completed measurements received before receipt on audited feeds;
    /// this adapter performs the hypothesis's sign/eligibility calculation, not data acquisition.
    /// Same eligible events for opposite/random controls. Fixed account-currency risk with
    /// attached protection, no target, original fixed exit. Backtest only; broker tick data.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdSlowMovingAverage : Robot
    {
        public enum TestBranch { Primary, Opposite, Random  }
        [Parameter("Branch",DefaultValue=TestBranch.Primary)] public TestBranch Variant {get;set;}
        [Parameter("Point-in-time feature rows",DefaultValue="")] public string FeatureRows {get;set;}
        [Parameter("Risk per event (account currency)",DefaultValue=50.0,MinValue=0.01)] public double RiskBudget {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T037")] public string PositionLabel {get;set;}
        [Parameter("Max trades per UTC day",DefaultValue=1,MinValue=1)] public int MaxTradesPerDay {get;set;}
        private Bars _m5;
        private int _fast,_slow;
        private readonly Dictionary<DateTime,int> _dailyTrades=new Dictionary<DateTime,int>();
        private sealed class Event { public DateTime Receipt,Exit; public double StopDistance; public double[] Values; }
        private readonly List<Event> _events=new List<Event>();
        private readonly Dictionary<int,DateTime> _deadlines=new Dictionary<int,DateTime>();
        private string _label;
        private int _next;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T037_STOP,reason=research_only");Stop();return;}
            try
            {
                if(RiskBudget<=0 || !Finite(RiskBudget))throw new FormatException("Positive finite risk required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (FeatureRows??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');
                    if(c.Length!=2+3)throw new FormatException("Expected 2 features plus receipt, exit, stop distance");
                    var ev=new Event{Receipt=Utc(c[0]),Exit=Utc(c[1]),StopDistance=Number(c[2]),Values=new double[2]};
                    if(!seen.Add(ev.Receipt) || ev.Exit-ev.Receipt!=TimeSpan.FromMinutes(60) || ev.StopDistance<=0)throw new FormatException("Duplicate receipt, incorrect horizon or invalid stop distance");
                    for(int i=0;i<ev.Values.Length;i++)ev.Values[i]=Number(c[i+3]);
                    var v=ev.Values;
                    if(v[0]<1 || v[1]<=v[0] || v[0]!=Math.Floor(v[0]) || v[1]!=Math.Floor(v[1]) || v[1]>100000 || ev.Receipt.Minute%5!=0 || ev.Receipt.Second!=0 || ev.Receipt.Date!=ev.Exit.Date)throw new FormatException("Freeze integer MA periods, M5 decision clocks and intraday exit");
                    if(_fast!=0 && (_fast!=(int)v[0] || _slow!=(int)v[1]))throw new FormatException("MA periods must remain frozen for the entire run");
                    _fast=(int)v[0];_slow=(int)v[1];
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Audited point-in-time features required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T037_STOP,reason=configuration,error={0}",e.Message);Stop();return;}
            _m5=MarketData.GetBars(TimeFrame.Minute5,SymbolName);
            _label=PositionLabel+"_"+Variant;
            Positions.Closed+=Closed;
            Print("T037_START,events={0},branch={1},horizon_minutes=60,fields=fast_m5_periods,slow_m5_periods",_events.Count,Variant);
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
                    Print("T037_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",position.Id,deadline,now,close.IsSuccessful,close.Error);
                }
            }
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"late_quote");continue;}
                if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid){Skip(ev,"invalid_quote");continue;}
                if(Positions.FindAll(_label,SymbolName).Length>0){Skip(ev,"overlapping_event");continue;}
                var side=Choose(ev.Values,ev.Receipt);
                Print("T037_FEATURES,receipt={0:O},features={1},hypothesis_side={2}",ev.Receipt,string.Join("|",Array.ConvertAll(ev.Values,x=>x.ToString("R",CultureInfo.InvariantCulture))),side);
                if(side==0){Skip(ev,"hypothesis_not_eligible");continue;}
                if(Variant==TestBranch.Opposite)side=-side;
                else if(Variant==TestBranch.Random)side=StableSign(ev.Receipt);
                var stopPips=ev.StopDistance/Symbol.PipSize;
                var volume=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(RiskBudget,stopPips,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"risk_volume_below_minimum");continue;}
                var requested=side>0?Symbol.Ask:Symbol.Bid;
                var result=ExecuteMarketOrder(side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,stopPips,(double?)null,"T037 point-in-time features");
                if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                _deadlines[result.Position.Id]=ev.Exit;
                int count;_dailyTrades.TryGetValue(ev.Receipt.Date,out count);_dailyTrades[ev.Receipt.Date]=count+1;
                Print("T037_ENTRY,receipt={0:O},quote={1:O},id={2},side={3},requested={4},filled={5},spread={6},volume={7},deadline={8:O}",ev.Receipt,now,result.Position.Id,side,requested,result.Position.EntryPrice,Symbol.Ask-Symbol.Bid,volume,ev.Exit);
            }
        }
        private int Choose(double[] v,DateTime receipt)
        {
            int traded;
            _dailyTrades.TryGetValue(receipt.Date,out traded);
            if(traded>=MaxTradesPerDay)return 0;
            int last=_m5.Count-1;
            while(last>=0 && _m5.OpenTimes[last].AddMinutes(5)>receipt)last--;
            if(last-_slow+1<0)return 0;
            double fast=0,slow=0;
            for(int n=0;n<_slow;n++)
            {
                var index=last-n;
                if(_m5.OpenTimes[index]!=receipt.AddMinutes(-5*(n+1)) || _m5.ClosePrices[index]<=0)return 0;
                slow+=_m5.ClosePrices[index];
                if(n<_fast)fast+=_m5.ClosePrices[index];
            }
            return Math.Sign(fast/_fast-slow/_slow);
        }
        private void Closed(PositionClosedEventArgs args)
        {
            if(args.Position.Label!=_label||args.Position.SymbolName!=SymbolName)return;
            _deadlines.Remove(args.Position.Id);
            Print("T037_CLOSED,id={0},net={1},reason={2}",args.Position.Id,args.Position.NetProfit,args.Reason);
        }
        private void Skip(Event ev,string reason)=>Print("T037_SKIP,receipt={0:O},reason={1},opportunity_pnl=0",ev.Receipt,reason);
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite feature");return n;}
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        private static int StableSign(DateTime time){unchecked{ulong x=(ulong)time.Ticks;x^=x>>12;x^=x<<25;x^=x>>27;return (x&1)==0?1:-1;}}
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            Print("T037_STOP,unreached={0},open={1}",_events.Count-_next,_label==null?0:Positions.FindAll(_label,SymbolName).Length);
        }
    }
}
