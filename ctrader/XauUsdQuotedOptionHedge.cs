using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>T100: native CFD execution plus explicitly simulated external long-option
    /// account using received firm bids/asks. No native exchange-option execution bridge.
    /// Rows: receiptUTC|exitUTC|side|fullUnits|reducedUnits|stopUSD|contracts|
    /// hedgedRisk|reducedRisk|hedgedCost|reducedCost|calibrationAvailableUTC.
    /// Quotes: receiptUTC|sourceUTC|bid|ask|underlyingGcMid|tradable(0/1).
    /// Every branch requires the same option data and prior calibrated budgets. No
    /// intrinsic-value substitute, stale liquidation or transfer into prop equity.</summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdQuotedOptionHedge : Robot
    {
        public enum Branch { Hedged, Unhedged, ReducedCfdControl }
        public enum Right { Unspecified, Put, Call }
        [Parameter("Branch",DefaultValue=Branch.Hedged)] public Branch Variant {get;set;}
        [Parameter("Frozen opportunities",DefaultValue="")] public string Opportunities {get;set;}
        [Parameter("Received option quotes",DefaultValue="")] public string OptionQuotes {get;set;}
        [Parameter("Actual option contract identifier",DefaultValue="")] public string OptionContract {get;set;}
        [Parameter("Actual GC underlying identifier",DefaultValue="")] public string UnderlyingContract {get;set;}
        [Parameter("Exercise rules reference",DefaultValue="")] public string ExerciseReference {get;set;}
        [Parameter("Prior cost/risk calibration reference",DefaultValue="")] public string CalibrationReference {get;set;}
        [Parameter("Option right",DefaultValue=Right.Unspecified)] public Right OptionRight {get;set;}
        [Parameter("Strike USD/oz",DefaultValue=0.0)] public double Strike {get;set;}
        [Parameter("Expiry UTC",DefaultValue="")] public string ExpiryUtc {get;set;}
        [Parameter("Contract multiplier oz",DefaultValue=0.0)] public double Multiplier {get;set;}
        [Parameter("External initial cash USD",DefaultValue=0.0)] public double InitialOptionCash {get;set;}
        [Parameter("Option fee USD/contract/side",DefaultValue=-1.0)] public double Fee {get;set;}
        [Parameter("Maximum source age seconds",DefaultValue=0.0)] public double MaximumAgeSeconds {get;set;}
        [Parameter("Calibration relative tolerance",DefaultValue=-1.0)] public double CalibrationTolerance {get;set;}
        [Parameter("Actual CFD equity floor USD",DefaultValue=0.0)] public double EquityFloor {get;set;}
        [Parameter("Label",DefaultValue="XAU_T100")] public string PositionLabel {get;set;}
        private class Event { public DateTime Receipt,Exit; public int Side,Contracts; public double Full,Reduced,Stop; }
        private class Quote { public DateTime Receipt,Source; public double Bid,Ask,Gc; public bool Tradable; }
        private class Trial { public Event Event; public int Id; public bool OptionOpen,ExitRequested,Invalid,Barrier; public double Buy,MinCfd,MinCombined; }
        private readonly List<Event> _events=new List<Event>();
        private readonly List<Quote> _quotes=new List<Quote>();
        private Trial _trial; private int _next,_qi; private Quote _quote;
        private DateTime _expiry; private double _cash; private bool _halt;
        protected override void OnStart()
        {
            try
            {
                if(RunningMode==RunningMode.RealTime)throw new ArgumentException("Historical research only");
                if(Account.Asset.Name!="USD"||Positions.Count!=0||PendingOrders.Count!=0)throw new ArgumentException("Initially isolated USD account required");
                if(new[]{OptionContract,UnderlyingContract,ExerciseReference,CalibrationReference,PositionLabel}.Any(string.IsNullOrWhiteSpace)||OptionRight==Right.Unspecified)throw new ArgumentException("Actual contract and prior calibration metadata required");
                if(!Positive(Strike)||!Positive(Multiplier)||!Positive(InitialOptionCash)||!Positive(MaximumAgeSeconds)||!Positive(EquityFloor)||EquityFloor>=Account.Equity||!Finite(Fee)||Fee<0||!Finite(CalibrationTolerance)||CalibrationTolerance<0||CalibrationTolerance>0.1)throw new ArgumentException("Explicit valid cash, fees, freshness and account barrier required");
                _expiry=Utc(ExpiryUtc);_cash=InitialOptionCash;
                foreach(var row in (Opportunities??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var f=row.Split('|');if(f.Length!=12)throw new FormatException("Opportunity needs 12 fields");
                    var e=new Event{Receipt=Utc(f[0]),Exit=Utc(f[1]),Side=(int)N(f[2]),Full=N(f[3]),Reduced=N(f[4]),Stop=N(f[5]),Contracts=int.Parse(f[6],CultureInfo.InvariantCulture)};
                    if((N(f[2])!=1&&N(f[2])!=-1)||!Units(e.Full)||!Units(e.Reduced)||e.Reduced>=e.Full||!Positive(e.Stop)||e.Contracts<1||e.Exit<=e.Receipt||e.Exit>=_expiry||Utc(f[11])>=e.Receipt||e.Receipt.Date!=e.Exit.Date||!Matched(N(f[7]),N(f[8]))||!Matched(N(f[9]),N(f[10])))throw new FormatException("Invalid intraday event or prior equal-budget calibration");
                    if((e.Side>0&&OptionRight!=Right.Put)||(e.Side<0&&OptionRight!=Right.Call))throw new FormatException("Option must protect specified CFD direction");
                    _events.Add(e);
                }
                foreach(var row in (OptionQuotes??"").Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var f=row.Split('|');if(f.Length!=6)throw new FormatException("Quote needs 6 fields");
                    var q=new Quote{Receipt=Utc(f[0]),Source=Utc(f[1]),Bid=N(f[2]),Ask=N(f[3]),Gc=N(f[4]),Tradable=f[5].Trim()=="1"};
                    if(q.Source>q.Receipt||q.Bid<0||q.Ask<q.Bid||q.Gc<=0||(f[5].Trim()!="0"&&f[5].Trim()!="1"))throw new FormatException("Invalid quote chronology or market");_quotes.Add(q);
                }
                if(_events.Count==0||_quotes.Count==0)throw new FormatException("Both tapes required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));_quotes.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
                Positions.Closed+=Closed;
                Print("T100_QUOTE_REPLAY_ONLY,contract={0},underlying={1},right={2},strike={3},expiry={4:O},multiplier={5},external_cash={6},no_actual_option_fills=1",OptionContract,UnderlyingContract,OptionRight,Strike,_expiry,Multiplier,_cash);
            }
            catch(Exception ex){Print("T100_INVALID,{0}",ex.Message);Stop();}
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            while(_qi<_quotes.Count&&_quotes[_qi].Receipt<=now)_quote=_quotes[_qi++];
            if(_trial!=null)
            {
                var t=_trial;var p=Positions.FirstOrDefault(x=>x.Id==t.Id);
                if(Positions.Any(x=>x.Id!=t.Id)||PendingOrders.Count!=0){t.Invalid=true;t.ExitRequested=true;_halt=true;Print("T100_ISOLATION_INVALID,outside_activity=1");}
                t.MinCfd=Math.Min(t.MinCfd,Account.Equity);
                if(Account.Equity<=EquityFloor){t.Barrier=true;t.ExitRequested=true;_halt=true;}
                if(p==null||now>=t.Event.Exit)t.ExitRequested=true;
                var fresh=Fresh(now);var optionEquity=_cash+(t.OptionOpen&&fresh?_quote.Bid*Multiplier*t.Event.Contracts-Fee*t.Event.Contracts:0);
                if(t.OptionOpen&&!fresh){t.Invalid=true;Print("T100_OPTION_MARK_UNAVAILABLE,time={0:O},outside_position_still_open=1,combined_equity_unknown=1",now);}
                else t.MinCombined=Math.Min(t.MinCombined,Account.Equity+optionEquity);
                Print("T100_PATH,time={0:O},cfd_equity={1},external_cash={2},external_liquidation_equity={3},combined_equity={4},basis_gc_minus_broker={5},barrier={6}",now,Account.Equity,_cash,t.OptionOpen&&!fresh?double.NaN:optionEquity,t.OptionOpen&&!fresh?double.NaN:Account.Equity+optionEquity,fresh?_quote.Gc-(Symbol.Bid+Symbol.Ask)/2:double.NaN,t.Barrier);
                if(t.ExitRequested)
                {
                    if(p!=null){var close=ClosePosition(p);if(!close.IsSuccessful)Print("T100_CFD_CLOSE_REJECT,{0}",close.Error);}
                    if(t.OptionOpen&&fresh&&now<_expiry)
                    {
                        _cash+=(_quote.Bid*Multiplier-Fee)*t.Event.Contracts;t.OptionOpen=false;
                        Print("T100_OPTION_QUOTED_SALE,time={0:O},source={1:O},bid={2},contracts={3},cash={4},simulated_external_fill=1",now,_quote.Source,_quote.Bid,t.Event.Contracts,_cash);
                    }
                    if(t.OptionOpen&&now>=_expiry){t.Invalid=true;_halt=true;Print("T100_CENSORED_OPTION_EXPIRY,no_intrinsic_settlement_assumed=1");}
                    if(!Positions.Any(x=>x.Id==t.Id)&&!t.OptionOpen)
                    {
                        var deals=History.FindByPositionId(t.Id);
                        if(deals.Sum(x=>x.VolumeInUnits)>=(Variant==Branch.ReducedCfdControl?t.Event.Reduced:t.Event.Full)-1e-8)
                        {Print("T100_COMPLETE,receipt={0:O},branch={1},native_cfd_net={2},external_option_net={3},minimum_cfd_equity={4},minimum_combined_equity={5},cfd_barrier_hit={6},comparison_invalid={7}",t.Event.Receipt,Variant,deals.Sum(x=>x.NetProfit),Variant==Branch.Hedged?(_cash-t.Buy):0,t.MinCfd,t.MinCombined,t.Barrier,t.Invalid);_trial=null;}
                    }
                }
            }
            while(_next<_events.Count&&_events[_next].Receipt<now)
            {
                var e=_events[_next++];
                if(Account.Equity<=EquityFloor)_halt=true;
                if(_halt||_trial!=null||Positions.Count!=0||PendingOrders.Count!=0||!Fresh(now)||now>=e.Exit||(now-e.Receipt).TotalSeconds>2||!Positive(Symbol.Bid)||Symbol.Ask<Symbol.Bid){Skip(e,"not_isolated_or_missing_timely_quotes");continue;}
                var premium=(_quote.Ask*Multiplier+Fee)*e.Contracts;
                if(_cash<premium+Fee*e.Contracts){Skip(e,"insufficient_equal_external_cash_budget");continue;}
                var units=Variant==Branch.ReducedCfdControl?e.Reduced:e.Full;
                var result=ExecuteMarketOrder(e.Side>0?TradeType.Buy:TradeType.Sell,SymbolName,units,PositionLabel+"_"+_next,e.Stop/Symbol.PipSize,(double?)null,"T100 native CFD with quoted external option");
                if(!result.IsSuccessful||result.Position==null){Skip(e,"CFD_entry_rejected");continue;}
                _trial=new Trial{Event=e,Id=result.Position.Id,Buy=_cash,OptionOpen=Variant==Branch.Hedged,MinCfd=Account.Equity,MinCombined=Account.Equity+_cash,Invalid=result.Position.VolumeInUnits!=units,ExitRequested=result.Position.VolumeInUnits!=units};
                if(_trial.OptionOpen)_cash-=premium;
                _trial.MinCombined=Account.Equity+_cash+(_trial.OptionOpen?(_quote.Bid*Multiplier-Fee)*e.Contracts:0);
                Print("T100_ENTRY_EQUITY,cfd_equity={0},external_liquidation_equity={1},combined_equity={2}",Account.Equity,_trial.MinCombined-Account.Equity,_trial.MinCombined);
                Print("T100_ENTRY,receipt={0:O},branch={1},cfd_id={2},units={3},option_ask={4},contracts={5},hedge_contracts_per_cfd_unit={6},basis={7},external_cash={8},option_fill_is_quote_model=1",e.Receipt,Variant,result.Position.Id,result.Position.VolumeInUnits,_quote.Ask,_trial.OptionOpen?e.Contracts:0,e.Contracts/e.Full,_quote.Gc-(Symbol.Bid+Symbol.Ask)/2,_cash);
            }
        }
        private void Closed(PositionClosedEventArgs e){if(_trial!=null&&e.Position.Id==_trial.Id)_trial.ExitRequested=true;}
        private bool Fresh(DateTime now)=>_quote!=null&&_quote.Tradable&&_quote.Bid>0&&_quote.Ask>0&&now>=_quote.Source&&(now-_quote.Source).TotalSeconds<=MaximumAgeSeconds&&now<_expiry;
        private bool Units(double n)=>Positive(n)&&n>=Symbol.VolumeInUnitsMin&&n<=Symbol.VolumeInUnitsMax&&Math.Abs(Symbol.NormalizeVolumeInUnits(n,RoundingMode.Down)-n)<1e-8;
        private bool Matched(double a,double b)=>Positive(a)&&Positive(b)&&Math.Abs(a-b)<=CalibrationTolerance*Math.Max(a,b)+1e-10;
        private void Skip(Event e,string reason)=>Print("T100_SKIP,receipt={0:O},reason={1},original_opportunity_counted=1",e.Receipt,reason);
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
        private static bool Positive(double n)=>Finite(n)&&n>0;
        private static double N(string s){var n=double.Parse(s,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite input");return n;}
        private static DateTime Utc(string s)=>DateTime.ParseExact(s.Trim(),new[]{"yyyy-MM-ddTHH:mm:ss'Z'","yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        protected override void OnStop(){Positions.Closed-=Closed;if(_trial!=null)Print("T100_CENSORED,cfd_open={0},option_open={1},external_cash={2},no_unobserved_liquidation=1",Positions.Any(x=>x.Id==_trial.Id),_trial.OptionOpen,_cash);}
    }
}
