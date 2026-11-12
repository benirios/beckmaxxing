using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
using System.Linq;
namespace cAlgo.Robots
{
    /// <summary>
    /// T097: Compute the mean of the latest TWENTY VALID PRIOR SESSION midpoint returns for the same frozen local half-hour block. History is filtered by actual completion availability and strictly earlier local session dates. Combine its excess over an earlier-trained unconditional clock mean with prior-trained momentum and block coefficients. Compare ConstantClock, ShuffledDays and AdjacentBlock forecasts on identical eligible dates; apply pre-known calendar exclusions. Block grid, estimator and coefficients are frozen before forward evaluation, never selected from winning blocks.
    /// Input rows: receiptUTC|exitUTC|protectiveDistanceDollars|blockId|unconditional_clock_mean,prior_daily_momentum,prior_fitted_momentum_coefficient,event_exclusion_flag,prior_shuffled_day_mean,prior_adjacent_block_mean,prior_fitted_block_coefficient.
    /// UTC format yyyy-MM-ddTHH:mm:ssZ; rows separated by semicolon or newline.
    /// All features are completed measurements received before receipt on audited feeds;
    /// this adapter performs the hypothesis's sign/eligibility calculation, not data acquisition.
    /// Same eligible events for opposite/random controls. Fixed account-currency risk with
    /// attached protection, no target, original fixed exit. Backtest only; broker tick data.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdSameClockPersistence : Robot
    {
        public enum TestBranch { Primary, Opposite, Random , ConstantClock, ShuffledDays, AdjacentBlock }
        [Parameter("Branch",DefaultValue=TestBranch.Primary)] public TestBranch Variant {get;set;}
        [Parameter("Point-in-time feature rows",DefaultValue="")] public string FeatureRows {get;set;}
        [Parameter("Risk per event (account currency)",DefaultValue=50.0,MinValue=0.01)] public double RiskBudget {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T097")] public string PositionLabel {get;set;}
        [Parameter("Frozen block grid: id|IANA zone|HH:mm",DefaultValue="")] public string BlockGrid {get;set;}
        [Parameter("Completed history: availableUTC|sessionDate|blockId|midpointReturn",DefaultValue="")] public string PastBlockReturns {get;set;}
        private class Slot { public TimeZoneInfo Zone; public TimeSpan Start; }
        private class Past { public DateTime Complete,SessionDate; public string BlockId; public double Return; }
        private readonly Dictionary<string,Slot> _slots=new Dictionary<string,Slot>();
        private readonly List<Past> _past=new List<Past>();
        private sealed class Event { public DateTime Receipt,Exit; public double StopDistance; public double[] Values; public string BlockId; public DateTime LocalDate; }
        private readonly List<Event> _events=new List<Event>();
        private readonly Dictionary<int,DateTime> _deadlines=new Dictionary<int,DateTime>();
        private string _label;
        private int _next;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T097_STOP,reason=research_only");Stop();return;}
            try
            {
                foreach(var row in (BlockGrid??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=3||string.IsNullOrWhiteSpace(c[0])||(c[1].Trim()!="Europe/London"&&c[1].Trim()!="America/New_York"))throw new FormatException("Freeze London/NY IANA block grid");
                    var start=TimeSpan.ParseExact(c[2].Trim(),"hh\\:mm",CultureInfo.InvariantCulture);
                    if(start.Minutes%30!=0||_slots.ContainsKey(c[0].Trim()))throw new FormatException("Unique half-hour blocks required");
                    _slots.Add(c[0].Trim(),new Slot{Zone=TimeZoneInfo.FindSystemTimeZoneById(c[1].Trim()),Start=start});
                }
                if(_slots.Count==0)throw new FormatException("Frozen block grid required");
                var sessions=new HashSet<string>();
                foreach(var row in (PastBlockReturns??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=4)throw new FormatException("History requires availability, local session date, block ID and actual midpoint return");
                    var h=new Past{Complete=Utc(c[0]),SessionDate=DateTime.ParseExact(c[1].Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture),BlockId=c[2].Trim(),Return=Number(c[3])};
                    Slot slot;if(!_slots.TryGetValue(h.BlockId,out slot)||h.Return<=-1||!sessions.Add(h.BlockId+"_"+h.SessionDate.ToString("yyyyMMdd")))throw new FormatException("Invalid or duplicate block/session history");
                    var expected=h.SessionDate.Add(slot.Start).AddMinutes(30);var local=TimeZoneInfo.ConvertTimeFromUtc(h.Complete,slot.Zone);
                    if(local<expected||local>expected.AddSeconds(2))throw new FormatException("Historical return must be completed before availability, on the same declared local clock");
                    _past.Add(h);
                }

                if(_past.Count==0)throw new FormatException("Audited completed block-return history required");
                if(RiskBudget<=0 || !Finite(RiskBudget))throw new FormatException("Positive finite risk required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (FeatureRows??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');
                    if(c.Length!=7+4)throw new FormatException("Expected block ID plus seven model fields, receipt, exit, stop distance");
                    var ev=new Event{Receipt=Utc(c[0]),Exit=Utc(c[1]),StopDistance=Number(c[2]),Values=new double[7]};
                    if(!seen.Add(ev.Receipt) || ev.Exit-ev.Receipt>TimeSpan.FromMinutes(30)||ev.Exit-ev.Receipt<TimeSpan.FromMinutes(30).Subtract(TimeSpan.FromSeconds(2)) || ev.StopDistance<=0)throw new FormatException("Duplicate receipt, incorrect horizon or invalid stop distance");
                    ev.BlockId=c[3].Trim();for(int i=0;i<ev.Values.Length;i++)ev.Values[i]=Number(c[i+4]);
                    var v=ev.Values;
                    Slot slot;if(!_slots.TryGetValue(ev.BlockId,out slot))throw new FormatException("Unknown frozen block ID");var local=TimeZoneInfo.ConvertTimeFromUtc(ev.Receipt,slot.Zone);ev.LocalDate=local.Date;var expectedStart=local.Date.Add(slot.Start);var expectedEnd=expectedStart.AddMinutes(30);var localExit=TimeZoneInfo.ConvertTimeFromUtc(ev.Exit,slot.Zone);if(local<expectedStart||local>expectedStart.AddSeconds(2)||localExit!=expectedEnd||(v[3]!=0&&v[3]!=1)||v[6]<0)throw new FormatException("Decision/exit must match the frozen local half-hour grid and prior model");
                    _events.Add(ev);
                }
                if(_events.Count==0)throw new FormatException("Audited point-in-time features required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T097_STOP,reason=configuration,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            Positions.Closed+=Closed;
            Print("T097_START,events={0},branch={1},horizon_minutes=30,fields=blockId|unconditional_clock_mean,prior_daily_momentum,prior_fitted_momentum_coefficient,event_exclusion_flag,prior_shuffled_day_mean,prior_adjacent_block_mean,prior_fitted_block_coefficient",_events.Count,Variant);
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
                    Print("T097_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",position.Id,deadline,now,close.IsSuccessful,close.Error);
                }
            }
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"late_quote");continue;}
                if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid){Skip(ev,"invalid_quote");continue;}
                if(Positions.FindAll(_label,SymbolName).Length>0){Skip(ev,"overlapping_event");continue;}
                var side=Choose(ev.Values,ev);
                Print("T097_FEATURES,receipt={0:O},features={1},hypothesis_side={2}",ev.Receipt,string.Join("|",Array.ConvertAll(ev.Values,x=>x.ToString("R",CultureInfo.InvariantCulture))),side);
                if(side==0){Skip(ev,"hypothesis_not_eligible");continue;}
                if(Variant==TestBranch.Opposite)side=-side;
                else if(Variant==TestBranch.Random)side=StableSign(ev.Receipt);
                var stopPips=ev.StopDistance/Symbol.PipSize;
                var volume=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(RiskBudget,stopPips,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"risk_volume_below_minimum");continue;}
                var requested=side>0?Symbol.Ask:Symbol.Bid;
                var result=ExecuteMarketOrder(side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,stopPips,(double?)null,"T097 point-in-time features");
                if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                _deadlines[result.Position.Id]=ev.Exit;
                Print("T097_ENTRY,receipt={0:O},quote={1:O},id={2},side={3},requested={4},filled={5},spread={6},volume={7},deadline={8:O}",ev.Receipt,now,result.Position.Id,side,requested,result.Position.EntryPrice,Symbol.Ask-Symbol.Bid,volume,ev.Exit);
            }
        }
        private int Choose(double[] v,Event ev)
        {
            if(v[3]!=0)return 0;var history=_past.Where(h=>h.BlockId==ev.BlockId&&h.Complete<ev.Receipt&&h.SessionDate<ev.LocalDate).OrderByDescending(h=>h.SessionDate).Take(20).ToArray();if(history.Length!=20){Print("T097_WARMUP,receipt={0:O},block={1},prior_sessions={2}",ev.Receipt,ev.BlockId,history.Length);return 0;}var mean=history.Average(h=>h.Return);Print("T097_MEAN,receipt={0:O},block={1},prior20_mean={2},clock_mean={3}",ev.Receipt,ev.BlockId,mean,v[0]);var predictor=Variant==TestBranch.ShuffledDays?v[4]:Variant==TestBranch.AdjacentBlock?v[5]:mean;var baseForecast=v[0]+v[2]*v[1];return Math.Sign(Variant==TestBranch.ConstantClock?baseForecast:baseForecast+v[6]*(predictor-v[0]));
        }
        private void Closed(PositionClosedEventArgs args)
        {
            if(args.Position.Label!=_label||args.Position.SymbolName!=SymbolName)return;
            _deadlines.Remove(args.Position.Id);
            Print("T097_CLOSED,id={0},net={1},reason={2}",args.Position.Id,args.Position.NetProfit,args.Reason);
        }
        private void Skip(Event ev,string reason)=>Print("T097_SKIP,receipt={0:O},reason={1},opportunity_pnl=0",ev.Receipt,reason);
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite feature");return n;}
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        private static int StableSign(DateTime time){unchecked{ulong x=(ulong)time.Ticks;x^=x>>12;x^=x<<25;x^=x>>27;return (x&1)==0?1:-1;}}
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            Print("T097_STOP,unreached={0},open={1}",_events.Count-_next,_label==null?0:Positions.FindAll(_label,SymbolName).Length);
        }
    }
}
