using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T077: EXP-015 locked development replication: one-unit long at 20:00 NY after negative 08:20-16:00 midpoint return, exit next day 02:00, protected by the median BID range of the previous 20 complete 20:00-02:00 Asian windows. The row stop-distance field is that audited prior-only median; current night is excluded. US midpoint input must use synchronized bid/ask, not bid bars. Rows belong to the preaudited complete-date universe (92 US, 72 Asian M5 bars); incomplete dates are not filled or invented. UnconditionalLong includes all eligible nights; Opposite is the same-date short on negative-US dates. Development only 2018-2023: rejected confirmation 2024-2025 is not reopened. The source-fee ledger charges 30 USD/million per side and 0.02 USD/oz slippage per side on native fills; use zero additional platform slippage for that comparison to avoid double-counting. Platform net is separate.
    /// Input rows: receiptUTC|exitUTC|protectiveDistanceDollars|completed_us_midpoint_return,complete_us_m5_bars,prior_complete_asian_windows,preaudited_night_m5_bars.
    /// UTC format yyyy-MM-ddTHH:mm:ssZ; rows separated by semicolon or newline.
    /// All features are completed measurements received before receipt on audited feeds;
    /// this adapter performs the hypothesis's sign/eligibility calculation, not data acquisition.
    /// Same eligible dates for primary/opposite controls; unconditional long includes all dates. One unit with
    /// attached protection, no target, original fixed exit. Backtest only; broker tick data.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdAsiaReplenishment : Robot
    {
        public enum TestBranch { Primary, Opposite, Random , UnconditionalLong }
        [Parameter("Branch",DefaultValue=TestBranch.Primary)] public TestBranch Variant {get;set;}
        [Parameter("Point-in-time feature rows",DefaultValue="")] public string FeatureRows {get;set;}
        
        [Parameter("Position label",DefaultValue="XAU_T077")] public string PositionLabel {get;set;}
        
        private sealed class Event { public DateTime Receipt,Exit; public double StopDistance; public double[] Values; }
        private readonly List<Event> _events=new List<Event>();
        private readonly Dictionary<int,DateTime> _deadlines=new Dictionary<int,DateTime>();
        private string _label;
        private int _next;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T077_STOP,reason=research_only");Stop();return;}
            try
            {
                
                var seen=new HashSet<DateTime>();
                foreach(var row in (FeatureRows??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');
                    if(c.Length!=4+3)throw new FormatException("Expected 4 features plus receipt, exit, stop distance");
                    var ev=new Event{Receipt=Utc(c[0]),Exit=Utc(c[1]),StopDistance=Number(c[2]),Values=new double[4]};
                    if(!seen.Add(ev.Receipt) || ev.Exit-ev.Receipt!=TimeSpan.FromMinutes(360) || ev.StopDistance<=0)throw new FormatException("Duplicate receipt, incorrect horizon or invalid stop distance");
                    for(int i=0;i<ev.Values.Length;i++)ev.Values[i]=Number(c[i+3]);
                    var v=ev.Values;
                    var ny=TimeZoneInfo.ConvertTimeFromUtc(ev.Receipt,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));var end=TimeZoneInfo.ConvertTimeFromUtc(ev.Exit,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));if(ny.Year<2018||ny.Year>2023||ny.TimeOfDay!=new TimeSpan(20,0,0)||end.Date!=ny.Date.AddDays(1)||end.TimeOfDay!=new TimeSpan(2,0,0)||v[1]!=92||v[2]!=20||v[3]!=72)throw new FormatException("Requires locked development dates, exact clocks and complete prior-history universe");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Audited point-in-time features required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T077_STOP,reason=configuration,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            Positions.Closed+=Closed;
            Print("T077_START,events={0},branch={1},horizon_minutes=360,fields=completed_us_midpoint_return,complete_us_m5_bars,prior_complete_asian_windows,preaudited_night_m5_bars",_events.Count,Variant);
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
                    Print("T077_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",position.Id,deadline,now,close.IsSuccessful,close.Error);
                }
            }
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"late_quote");continue;}
                if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid){Skip(ev,"invalid_quote");continue;}
                if(Positions.FindAll(_label,SymbolName).Length>0){Skip(ev,"overlapping_event");continue;}
                var side=Choose(ev.Values);
                Print("T077_FEATURES,receipt={0:O},features={1},hypothesis_side={2}",ev.Receipt,string.Join("|",Array.ConvertAll(ev.Values,x=>x.ToString("R",CultureInfo.InvariantCulture))),side);
                if(side==0){Skip(ev,"hypothesis_not_eligible");continue;}
                if(Variant==TestBranch.Opposite)side=-side;
                else if(Variant==TestBranch.Random)side=StableSign(ev.Receipt);
                var stopPips=ev.StopDistance/Symbol.PipSize;
                if(ev.StopDistance<=Symbol.Ask-Symbol.Bid){Skip(ev,"initial_stop_inside_spread");continue;}
                var volume=Symbol.NormalizeVolumeInUnits(1.0,RoundingMode.Down);
                if(volume!=1.0 || volume>Symbol.VolumeInUnitsMax){Skip(ev,"one_unit_not_representable");continue;}
                if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"risk_volume_below_minimum");continue;}
                var requested=side>0?Symbol.Ask:Symbol.Bid;
                var result=ExecuteMarketOrder(side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,stopPips,(double?)null,"T077 point-in-time features");
                if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                _deadlines[result.Position.Id]=ev.Exit;
                Print("T077_ENTRY,receipt={0:O},quote={1:O},id={2},side={3},requested={4},filled={5},spread={6},volume={7},deadline={8:O}",ev.Receipt,now,result.Position.Id,side,requested,result.Position.EntryPrice,Symbol.Ask-Symbol.Bid,volume,ev.Exit);
            }
        }
        private int Choose(double[] v)
        {
            return Variant==TestBranch.UnconditionalLong || v[0]<0 ? 1 : 0;
        }
        private void Closed(PositionClosedEventArgs args)
        {
            if(args.Position.Label!=_label||args.Position.SymbolName!=SymbolName)return;
            _deadlines.Remove(args.Position.Id);
            var side=args.Position.TradeType==TradeType.Buy?1:-1;
            foreach(var fill in History.FindByPositionId(args.Position.Id))
                Print("T077_REFERENCE_COST_LEDGER,id={0},exit_price={1},net_per_oz={2},extra_source_fees_are_not_platform_charges=1",args.Position.Id,fill.ClosingPrice,side*(fill.ClosingPrice-args.Position.EntryPrice)-0.00003*(fill.ClosingPrice+args.Position.EntryPrice)-0.04);
            Print("T077_CLOSED,id={0},net={1},reason={2}",args.Position.Id,args.Position.NetProfit,args.Reason);
        }
        private void Skip(Event ev,string reason)=>Print("T077_SKIP,receipt={0:O},reason={1},opportunity_pnl=0",ev.Receipt,reason);
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite feature");return n;}
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        private static int StableSign(DateTime time){unchecked{ulong x=(ulong)time.Ticks;x^=x>>12;x^=x<<25;x^=x>>27;return (x&1)==0?1:-1;}}
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            Print("T077_STOP,unreached={0},open={1}",_events.Count-_next,_label==null?0:Positions.FindAll(_label,SymbolName).Length);
        }
    }
}
