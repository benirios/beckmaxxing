using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T072: actual authorized dealer inventory, not public taker flow, predicts
    /// dealer quote changes relative to a synchronized composite over 15 minutes.
    /// Inventory rows: receiptUTC|netLongInventory|customerFlow|publicGoldReturn|authorized(1).
    /// Quote rows: receiptUTC|sourceUTC|dealerBid|dealerAsk|compositeBid|compositeAsk.
    /// sourceUTC is the OLDEST contributing quote timestamp of the paired snapshot.
    /// A quote is visible only after delivery, never from a future row. Fresh source
    /// quotes are required at both endpoints. Also measure broker return over the
    /// FOLLOWING 15 minutes against the observed relative dealer skew.
    /// Observation cBot only: a dealer discount is not automatically a broker trade.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdDealerInventoryQuoteSkew : Robot
    {
        [Parameter("Authorized inventory events",DefaultValue="")] public string InventoryEvents {get;set;}
        [Parameter("Received dealer/composite quotes",DefaultValue="")] public string ReceivedQuotes {get;set;}
        [Parameter("Frozen maximum source age (seconds)",DefaultValue=2.0,MinValue=0.01)] public double MaxSourceAgeSeconds {get;set;}
        private class Event { public DateTime Receipt; public double Inventory,CustomerFlow,PublicReturn; }
        private class Quote { public DateTime Receipt,Source; public double DealerMid,CompositeMid; }
        private class Sample
        {
            public Event Event;
            public double DealerStart,CompositeStart,BrokerStart,BrokerAt15,RelativeSkew;
            public bool PrimaryDone;
        }
        private readonly List<Event> _events=new List<Event>();
        private readonly List<Quote> _quotes=new List<Quote>();
        private readonly List<Sample> _samples=new List<Sample>();
        private int _nextEvent,_nextQuote;
        private Quote _latest;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T072_STOP,reason=research_only");Stop();return;}
            try
            {
                if(!Finite(MaxSourceAgeSeconds)||MaxSourceAgeSeconds<=0)throw new FormatException("Positive frozen quote-age tolerance required");
                var seen=new HashSet<DateTime>();
                foreach(var row in (InventoryEvents??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=5)throw new FormatException("Inventory rows need five columns");
                    var ev=new Event{Receipt=Utc(c[0]),Inventory=Number(c[1]),CustomerFlow=Number(c[2]),PublicReturn=Number(c[3])};
                    if(!seen.Add(ev.Receipt)||Number(c[4])!=1)throw new FormatException("Unique actual authorized inventory observations required");
                    _events.Add(ev);
                }
                seen.Clear();
                foreach(var row in (ReceivedQuotes??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=6)throw new FormatException("Quote rows need six columns");
                    var quote=new Quote{Receipt=Utc(c[0]),Source=Utc(c[1])};
                    var db=Number(c[2]);var da=Number(c[3]);var cb=Number(c[4]);var ca=Number(c[5]);
                    if(!seen.Add(quote.Receipt)||quote.Source>quote.Receipt||db<=0||da<db||cb<=0||ca<cb)throw new FormatException("Invalid or future dealer/composite quote");
                    quote.DealerMid=(db+da)/2;quote.CompositeMid=(cb+ca)/2;_quotes.Add(quote);
                }
                if(_events.Count==0||_quotes.Count==0)throw new FormatException("Inventory AND actual synchronized quote records required");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));_quotes.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch(Exception e){Print("T072_STOP,error={0}",e.Message);Stop();return;}
            Print("T072_START,inventory_events={0},received_quotes={1},primary_minutes=15,followup_minutes=15,max_source_age={2}",_events.Count,_quotes.Count,MaxSourceAgeSeconds);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            while(_nextQuote<_quotes.Count && _quotes[_nextQuote].Receipt<=now)_latest=_quotes[_nextQuote++];
            if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid)return;
            var brokerMid=(Symbol.Bid+Symbol.Ask)/2;
            for(int i=_samples.Count-1;i>=0;i--)
            {
                var sample=_samples[i];var primary=sample.Event.Receipt.AddMinutes(15);var followup=sample.Event.Receipt.AddMinutes(30);
                if(!sample.PrimaryDone && now>=primary)
                {
                    if((now-primary).TotalSeconds>2 || !Fresh(now))
                    {Print("T072_INVALID,receipt={0:O},reason=missing_primary_endpoint",sample.Event.Receipt);_samples.RemoveAt(i);continue;}
                    sample.RelativeSkew=Math.Log(_latest.DealerMid/sample.DealerStart)-Math.Log(_latest.CompositeMid/sample.CompositeStart);
                    sample.BrokerAt15=brokerMid;sample.PrimaryDone=true;
                    Print("T072_PRIMARY,receipt={0:O},quote={1:O},inventory={2},customer_flow={3},public_return={4},dealer_relative_log_change={5},broker_return={6}",sample.Event.Receipt,now,sample.Event.Inventory,sample.Event.CustomerFlow,sample.Event.PublicReturn,sample.RelativeSkew,brokerMid/sample.BrokerStart-1);
                }
                if(sample.PrimaryDone && now>=followup)
                {
                    if((now-followup).TotalSeconds>2)Print("T072_INVALID,receipt={0:O},reason=missing_followup_endpoint",sample.Event.Receipt);
                    else Print("T072_FOLLOWUP,receipt={0:O},relative_skew={1},later_broker_return={2},cross_product={3}",sample.Event.Receipt,sample.RelativeSkew,brokerMid/sample.BrokerAt15-1,sample.RelativeSkew*(brokerMid/sample.BrokerAt15-1));
                    _samples.RemoveAt(i);
                }
            }
            while(_nextEvent<_events.Count && _events[_nextEvent].Receipt<now)
            {
                var ev=_events[_nextEvent++];
                if((now-ev.Receipt).TotalSeconds>2 || !Fresh(now))
                {Print("T072_SKIP,receipt={0:O},reason=missing_timely_inventory_or_quote",ev.Receipt);continue;}
                _samples.Add(new Sample{Event=ev,DealerStart=_latest.DealerMid,CompositeStart=_latest.CompositeMid,BrokerStart=brokerMid});
                Print("T072_BEGIN,receipt={0:O},quote_receipt={1:O},quote_source={2:O},dealer_mid={3},composite_mid={4}",ev.Receipt,_latest.Receipt,_latest.Source,_latest.DealerMid,_latest.CompositeMid);
            }
        }
        private bool Fresh(DateTime now)=>_latest!=null && _latest.Receipt<=now && _latest.Source<=now && (now-_latest.Source).TotalSeconds<=MaxSourceAgeSeconds;
        private static DateTime Utc(string value)=>DateTime.ParseExact(value.Trim(),new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string value){var n=double.Parse(value.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite input");return n;}
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        protected override void OnStop(){foreach(var sample in _samples)Print("T072_CENSORED,receipt={0:O},primary_complete={1}",sample.Event.Receipt,sample.PrimaryDone);Print("T072_STOP,unreached_inventory={0}",_events.Count-_nextEvent);}
    }
}
