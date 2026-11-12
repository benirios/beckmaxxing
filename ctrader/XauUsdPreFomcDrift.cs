using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T091: Fixed 12:00-13:55 New York pre-FOMC long versus matched non-FOMC, short/random and no-trade controls. Only a pre-entry-known normal 14:00 statement schedule qualifies; shifted schedules are skipped. Actual publication receipts are consumed only after their timestamps: an unexpected early release closes exposure and marks the fixed-window observation contaminated, never retrospectively avoids the entry. Source evidence was negative; no clock tuning is authorized.
    /// Input rows: receiptUTC|exitUTC|protectiveDistanceDollars|scheduled_fomc_date,pre_entry_scheduled_statement_minute_NY,matched_nonfomc_control.
    /// UTC format yyyy-MM-ddTHH:mm:ssZ; rows separated by semicolon or newline.
    /// All features are completed measurements received before receipt on audited feeds;
    /// this adapter performs the hypothesis's sign/eligibility calculation, not data acquisition.
    /// Same eligible events for opposite/random controls. Fixed account-currency risk with
    /// attached protection, no target, original fixed exit. Backtest only; broker tick data.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdPreFomcDrift : Robot
    {
        public enum TestBranch { Primary, Opposite, Random , NonFomcControl, NoTrade }
        [Parameter("Branch",DefaultValue=TestBranch.Primary)] public TestBranch Variant {get;set;}
        [Parameter("Point-in-time feature rows",DefaultValue="")] public string FeatureRows {get;set;}
        [Parameter("Risk per event (account currency)",DefaultValue=50.0,MinValue=0.01)] public double RiskBudget {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T091")] public string PositionLabel {get;set;}
        [Parameter("Actual publication receipts UTC",DefaultValue="")] public string ReleaseReceiptsUtc {get;set;}
        private readonly List<DateTime> _releases=new List<DateTime>();
        private readonly HashSet<DateTime> _published=new HashSet<DateTime>();
        private int _nextRelease;
        private sealed class Event { public DateTime Receipt,Exit; public double StopDistance; public double[] Values; }
        private readonly List<Event> _events=new List<Event>();
        private readonly Dictionary<int,DateTime> _deadlines=new Dictionary<int,DateTime>();
        private string _label;
        private int _next;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T091_STOP,reason=research_only");Stop();return;}
            try
            {
                if(RiskBudget<=0 || !Finite(RiskBudget))throw new FormatException("Positive finite risk required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (FeatureRows??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');
                    if(c.Length!=3+3)throw new FormatException("Expected 3 features plus receipt, exit, stop distance");
                    var ev=new Event{Receipt=Utc(c[0]),Exit=Utc(c[1]),StopDistance=Number(c[2]),Values=new double[3]};
                    if(!seen.Add(ev.Receipt) || ev.Exit-ev.Receipt!=TimeSpan.FromMinutes(115) || ev.StopDistance<=0)throw new FormatException("Duplicate receipt, incorrect horizon or invalid stop distance");
                    for(int i=0;i<ev.Values.Length;i++)ev.Values[i]=Number(c[i+3]);
                    var v=ev.Values;
                    var ny=TimeZoneInfo.ConvertTimeFromUtc(ev.Receipt,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));if(ny.TimeOfDay!=new TimeSpan(12,0,0)||(v[0]!=0&&v[0]!=1)||v[1]<0||v[1]>=1440||v[1]!=Math.Floor(v[1])||(v[2]!=0&&v[2]!=1))throw new FormatException("Requires exact predeclared noon decision and valid scheduled calendar");
                    _events.Add(ev);
                }
                var releaseTimes=new HashSet<DateTime>();
                foreach(var token in (ReleaseReceiptsUtc??"").Split(new[]{',',';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var release=Utc(token);if(!releaseTimes.Add(release))throw new FormatException("Duplicate publication receipt");_releases.Add(release);
                }
                if(_releases.Count==0)throw new FormatException("Actual publication receipt calendar required for contamination audit");
                _releases.Sort();
                if(_events.Count==0)throw new FormatException("Audited point-in-time features required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T091_STOP,reason=configuration,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;
            Positions.Closed+=Closed;
            Print("T091_START,events={0},branch={1},horizon_minutes=115,fields=scheduled_fomc_date,pre_entry_scheduled_statement_minute_NY,matched_nonfomc_control",_events.Count,Variant);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            while(_nextRelease<_releases.Count && _releases[_nextRelease]<now)
            {
                var release=_releases[_nextRelease++];
                var local=TimeZoneInfo.ConvertTimeFromUtc(release,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
                _published.Add(local.Date);
                foreach(var exposed in Positions.FindAll(_label,SymbolName))
                {
                    if(local.TimeOfDay<new TimeSpan(13,55,0))
                    {
                        _deadlines[exposed.Id]=now;
                        Print("T091_CONTAMINATED,id={0},publication_receipt={1:O},quote={2:O},reason=unexpected_early_release,no_retrospective_entry_filter=1",exposed.Id,release,now);
                    }
                }
            }
            foreach(var position in Positions.FindAll(_label,SymbolName))
            {
                DateTime deadline;
                if(_deadlines.TryGetValue(position.Id,out deadline) && now>=deadline)
                {
                    var close=ClosePosition(position);
                    Print("T091_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",position.Id,deadline,now,close.IsSuccessful,close.Error);
                }
            }
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2){Skip(ev,"late_quote");continue;}
                if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid){Skip(ev,"invalid_quote");continue;}
                if(Positions.FindAll(_label,SymbolName).Length>0){Skip(ev,"overlapping_event");continue;}
                var side=Choose(ev.Values,ev.Receipt);
                Print("T091_FEATURES,receipt={0:O},features={1},hypothesis_side={2}",ev.Receipt,string.Join("|",Array.ConvertAll(ev.Values,x=>x.ToString("R",CultureInfo.InvariantCulture))),side);
                if(side==0){Skip(ev,"hypothesis_not_eligible");continue;}
                if(Variant==TestBranch.Opposite)side=-side;
                else if(Variant==TestBranch.Random)side=StableSign(ev.Receipt);
                var stopPips=ev.StopDistance/Symbol.PipSize;
                var volume=Symbol.NormalizeVolumeInUnits(Math.Min(Symbol.VolumeForFixedRisk(RiskBudget,stopPips,RoundingMode.Down),Symbol.VolumeInUnitsMax),RoundingMode.Down);
                if(volume<Symbol.VolumeInUnitsMin){Skip(ev,"risk_volume_below_minimum");continue;}
                var requested=side>0?Symbol.Ask:Symbol.Bid;
                var result=ExecuteMarketOrder(side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,stopPips,(double?)null,"T091 point-in-time features");
                if(!result.IsSuccessful||result.Position==null){Skip(ev,"order_failed_"+result.Error);continue;}
                _deadlines[result.Position.Id]=ev.Exit;
                Print("T091_ENTRY,receipt={0:O},quote={1:O},id={2},side={3},requested={4},filled={5},spread={6},volume={7},deadline={8:O}",ev.Receipt,now,result.Position.Id,side,requested,result.Position.EntryPrice,Symbol.Ask-Symbol.Bid,volume,ev.Exit);
            }
        }
        private int Choose(double[] v,DateTime receipt)
        {
            var day=TimeZoneInfo.ConvertTimeFromUtc(receipt,TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).Date;if(_published.Contains(day)||Variant==TestBranch.NoTrade)return 0;if(Variant==TestBranch.NonFomcControl)return v[0]==0&&v[2]==1?1:0;return v[0]==1&&v[1]==840?1:0;
        }
        private void Closed(PositionClosedEventArgs args)
        {
            if(args.Position.Label!=_label||args.Position.SymbolName!=SymbolName)return;
            _deadlines.Remove(args.Position.Id);
            Print("T091_CLOSED,id={0},net={1},reason={2}",args.Position.Id,args.Position.NetProfit,args.Reason);
        }
        private void Skip(Event ev,string reason)=>Print("T091_SKIP,receipt={0:O},reason={1},opportunity_pnl=0",ev.Receipt,reason);
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite feature");return n;}
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        private static int StableSign(DateTime time){unchecked{ulong x=(ulong)time.Ticks;x^=x>>12;x^=x<<25;x^=x>>27;return (x&1)==0?1:-1;}}
        protected override void OnStop()
        {
            Positions.Closed-=Closed;
            Print("T091_STOP,unreached={0},open={1}",_events.Count-_next,_label==null?0:Positions.FindAll(_label,SymbolName).Length);
        }
    }
}
