using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T076 / EXP-016 overnight leg. The daytime leg is already implemented by
    /// XauUsdGcDaytimeShort.cs (EXP-011); this canonical hypothesis counts once.
    /// One ounce, Monday-Thursday 18:05 NY to next-day 08:20, fixed 1% stop,
    /// no target. Supply preaudited complete NY entry dates (171 held M5 bars
    /// plus exit bar); no missing bars are invented and date eligibility is
    /// structural data audit, never selected using PnL. Development 2018-2023
    /// only: failed development does not authorize opening 2024-2025 confirmation.
    /// Native broker bid/ask fills and net PnL plus a separate source-fee ledger.
    /// Reference ledger charges $30/million per side and $0.02/oz slippage per
    /// side on native fill prices; set platform slippage to zero for that ledger
    /// comparison to avoid double-counting. Actual platform fees remain separate.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdOvernightDrift : Robot
    {
        public enum Branch { OvernightLong, SameDateShortControl }
        [Parameter("Branch",DefaultValue=Branch.OvernightLong)] public Branch Variant {get;set;}
        [Parameter("Preaudited complete NY entry dates (yyyy-MM-dd)",DefaultValue="")] public string CompleteDates {get;set;}
        [Parameter("Position label",DefaultValue="XAU_T076_OVERNIGHT")] public string PositionLabel {get;set;}
        private class Window { public DateTime Entry,Exit,LocalDate; }
        private readonly List<Window> _windows=new List<Window>();
        private readonly Dictionary<int,DateTime> _exits=new Dictionary<int,DateTime>();
        private int _next;
        private string _label;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T076_STOP,reason=research_only");Stop();return;}
            try
            {
                var ny=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                var seen=new HashSet<DateTime>();
                foreach(var token in (CompleteDates??"").Split(new[]{',',';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var date=DateTime.ParseExact(token.Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None);
                    if(!seen.Add(date)||date.Year<2018||date.Year>2023||date.DayOfWeek==DayOfWeek.Friday||date.DayOfWeek==DayOfWeek.Saturday||date.DayOfWeek==DayOfWeek.Sunday)
                        throw new FormatException("Unique preaudited Mon-Thu development dates 2018-2023 required");
                    _windows.Add(new Window{LocalDate=date,Entry=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.AddHours(18).AddMinutes(5),DateTimeKind.Unspecified),ny),Exit=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.AddDays(1).AddHours(8).AddMinutes(20),DateTimeKind.Unspecified),ny)});
                }
                if(_windows.Count==0)throw new FormatException("Preaudited complete entry dates required");
                _windows.Sort((a,b)=>a.Entry.CompareTo(b.Entry));
            }
            catch(Exception e){Print("T076_STOP,error={0}",e.Message);Stop();return;}
            _label=PositionLabel+"_"+Variant;Positions.Closed+=Closed;
            Print("T076_START,branch={0},windows={1},volume=1,stop=1pct,entry=18:05_NY,exit=next_08:20_NY",Variant,_windows.Count);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            foreach(var p in Positions.FindAll(_label,SymbolName))
            {
                DateTime exit;if(_exits.TryGetValue(p.Id,out exit)&&now>=exit)
                {
                    var close=ClosePosition(p);Print("T076_EXIT,id={0},deadline={1:O},quote={2:O},success={3},error={4}",p.Id,exit,now,close.IsSuccessful,close.Error);
                }
            }
            while(_next<_windows.Count && _windows[_next].Entry<=now)
            {
                var w=_windows[_next++];
                if((now-w.Entry).TotalSeconds>2){Skip(w,"missing_exact_open_quote");continue;}
                if(Positions.FindAll(_label,SymbolName).Length>0){Skip(w,"overlapping_position");continue;}
                if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid){Skip(w,"invalid_quote");continue;}
                var volume=Symbol.NormalizeVolumeInUnits(1.0,RoundingMode.Down);
                if(volume!=1.0||volume<Symbol.VolumeInUnitsMin||volume>Symbol.VolumeInUnitsMax){Skip(w,"one_unit_not_representable");continue;}
                var side=Variant==Branch.OvernightLong?1:-1;
                var entry=side>0?Symbol.Ask:Symbol.Bid;var stop=entry*(1-side*0.01);
                if(side>0?stop>=Symbol.Bid:stop<=Symbol.Ask){Skip(w,"initial_stop_inside_spread");continue;}
                var result=ExecuteMarketOrder(side>0?TradeType.Buy:TradeType.Sell,SymbolName,volume,_label,entry*0.01/Symbol.PipSize,(double?)null,"EXP016 locked overnight");
                if(!result.IsSuccessful||result.Position==null){Skip(w,"order_failed_"+result.Error);continue;}
                var p=result.Position;_exits[p.Id]=w.Exit;
                var protection=ModifyPosition(p,p.EntryPrice*(1-side*0.01),(double?)null,ProtectionType.Absolute);
                if(!protection.IsSuccessful)
                {
                    _exits[p.Id]=now;var close=ClosePosition(p);
                    Print("T076_ABORT,id={0},reason=protection_failed,close_success={1}",p.Id,close.IsSuccessful);
                }
                Print("T076_ENTRY,date={0:yyyy-MM-dd},id={1},quote={2:O},entry={3},stop={4},exit={5:O},structural_complete_window_preaudited=1",w.LocalDate,p.Id,now,p.EntryPrice,p.EntryPrice*(1-side*0.01),w.Exit);
            }
        }
        private void Closed(PositionClosedEventArgs a)
        {
            var p=a.Position;if(p.Label!=_label||p.SymbolName!=SymbolName)return;_exits.Remove(p.Id);
            Print("T076_CLOSED,id={0},platform_net={1},reason={2}",p.Id,p.NetProfit,a.Reason);
            var side=p.TradeType==TradeType.Buy?1:-1;
            foreach(var fill in History.FindByPositionId(p.Id))
                Print("T076_REFERENCE_COST_LEDGER,id={0},exit_price={1},net_per_oz={2},extra_source_fees_are_not_platform_charges=1",p.Id,fill.ClosingPrice,side*(fill.ClosingPrice-p.EntryPrice)-0.00003*(fill.ClosingPrice+p.EntryPrice)-0.04);
        }
        private void Skip(Window w,string reason)=>Print("T076_SKIP,date={0:yyyy-MM-dd},reason={1}",w.LocalDate,reason);
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        protected override void OnStop(){Positions.Closed-=Closed;Print("T076_STOP,unreached_windows={0},open_positions={1}",_windows.Count-_next,_label==null?0:Positions.FindAll(_label,SymbolName).Length);}
    }
}
