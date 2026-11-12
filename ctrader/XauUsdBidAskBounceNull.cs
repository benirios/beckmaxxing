using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;
namespace cAlgo.Robots
{
    /// <summary>
    /// T088 execution null: compare consecutive ACTUAL GC transaction changes
    /// with synchronized midquote changes and next-broker-quote hypothetical
    /// fade PnL. A CFD bid bar cannot substitute for transaction prints.
    /// Rows: receiptUTC|sourceUTC|sequence|tradePrice|midquote|tradeSign(+1/-1)|realTransactionSeries(1)|roundtripCommissionPerOz|slippagePerSidePerOz.
    /// Use one actual contract per run; preserve exchange ordering within batches.
    /// No orders are submitted and hypothetical quote-side prices are not fills.
    /// </summary>
    [Robot(TimeZone=TimeZones.UTC,AccessRights=AccessRights.None)]
    public class XauUsdBidAskBounceNull : Robot
    {
        [Parameter("Received trade/quote rows",DefaultValue="")] public string TradeRows {get;set;}
        [Parameter("Actual GC contract",DefaultValue="")] public string ContractIdentifier {get;set;}
        private class TradePrint { public DateTime Receipt,Source; public long Sequence; public double Price,Mid,Sign,Commission,Slippage; }
        private class Roundtrip { public long EntryTick,Sequence; public int Side; public double Entry,Commission,Slippage; public string Predictor; }
        private readonly List<TradePrint> _trades=new List<TradePrint>();
        private readonly List<Roundtrip> _roundtrips=new List<Roundtrip>();
        private TradePrint _last;
        private double _lastTradeReturn,_lastMidReturn;
        private bool _hasPreviousReturn;
        private int _next;
        private long _tick;
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime){Print("T088_STOP,reason=research_only");Stop();return;}
            try
            {
                if(string.IsNullOrWhiteSpace(ContractIdentifier))throw new FormatException("Actual unadjusted contract required");
                var sequences=new HashSet<long>();
                foreach(var row in (TradeRows??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
                {
                    var c=row.Trim().Split('|');if(c.Length!=9)throw new FormatException("Nine columns required");
                    var t=new TradePrint{Receipt=Utc(c[0]),Source=Utc(c[1]),Sequence=long.Parse(c[2].Trim(),CultureInfo.InvariantCulture),Price=Number(c[3]),Mid=Number(c[4]),Sign=Number(c[5]),Commission=Number(c[7]),Slippage=Number(c[8])};
                    if(t.Source>t.Receipt||!sequences.Add(t.Sequence)||t.Price<=0||t.Mid<=0||(t.Sign!=1&&t.Sign!=-1)||Number(c[6])!=1||t.Commission<0||t.Slippage<0)throw new FormatException("Real sequenced trade/quote data and explicit costs required");
                    _trades.Add(t);
                }
                if(_trades.Count<3)throw new FormatException("At least three actual prints needed for lag comparison");
                _trades.Sort((a,b)=>a.Receipt==b.Receipt?a.Sequence.CompareTo(b.Sequence):a.Receipt.CompareTo(b.Receipt));
                for(int i=1;i<_trades.Count;i++)if(_trades[i].Sequence<=_trades[i-1].Sequence||_trades[i].Source<_trades[i-1].Source)throw new FormatException("Exchange sequence/source chronology must be monotonic");
            }
            catch(Exception e){Print("T088_STOP,error={0}",e.Message);Stop();return;}
            Print("T088_START,contract={0},prints={1},orders_submitted=0",ContractIdentifier,_trades.Count);
        }
        protected override void OnTick()
        {
            _tick++;
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            if(!Finite(Symbol.Bid)||!Finite(Symbol.Ask)||Symbol.Bid<=0||Symbol.Ask<Symbol.Bid)return;
            for(int i=_roundtrips.Count-1;i>=0;i--)
            {
                var r=_roundtrips[i];if(_tick<=r.EntryTick)continue;
                var exit=r.Side>0?Symbol.Bid:Symbol.Ask;
                var net=r.Side*(exit-r.Entry)-r.Commission-2*r.Slippage;
                Print("T088_HYPOTHETICAL_NEXT_QUOTE,sequence={0},predictor={1},side={2},net_per_oz={3},quote={4:O},actual_orders=0",r.Sequence,r.Predictor,r.Side,net,now);
                _roundtrips.RemoveAt(i);
            }
            while(_next<_trades.Count && _trades[_next].Receipt<now)
            {
                var t=_trades[_next++];
                Print("T088_RAW,receipt={0:O},source={1:O},sequence={2},transaction_price={3},midquote={4},trade_sign={5},source_age_seconds={6},broker_spread={7},effective_spread={8}",t.Receipt,t.Source,t.Sequence,t.Price,t.Mid,t.Sign,(t.Receipt-t.Source).TotalSeconds,Symbol.Ask-Symbol.Bid,2*Math.Abs(t.Price-t.Mid));
                if(_last!=null)
                {
                    var tradeReturn=t.Price/_last.Price-1;var midReturn=t.Mid/_last.Mid-1;
                    if(_hasPreviousReturn)
                        Print("T088_LAG_PAIR,sequence={0},previous_trade_return={1},next_trade_return={2},previous_mid_return={3},next_mid_return={4},trade_cross_product={5},mid_cross_product={6},trade_sign={7}",t.Sequence,_lastTradeReturn,tradeReturn,_lastMidReturn,midReturn,_lastTradeReturn*tradeReturn,_lastMidReturn*midReturn,t.Sign);
                    _lastTradeReturn=tradeReturn;_lastMidReturn=midReturn;_hasPreviousReturn=true;
                    if((now-t.Receipt).TotalSeconds<=2)
                    {
                        Add(t,-Math.Sign(tradeReturn),"transaction");Add(t,-Math.Sign(midReturn),"midquote");
                    }
                    else Print("T088_SKIP_HYPOTHETICAL,sequence={0},reason=late_quote",t.Sequence);
                }
                _last=t;
            }
        }
        private void Add(TradePrint t,int side,string predictor)
        {
            if(side==0){Print("T088_NO_PREDICTOR,sequence={0},predictor={1},opportunity_pnl=0",t.Sequence,predictor);return;}
            _roundtrips.Add(new Roundtrip{EntryTick=_tick,Sequence=t.Sequence,Side=side,Entry=side>0?Symbol.Ask:Symbol.Bid,Commission=t.Commission,Slippage=t.Slippage,Predictor=predictor});
        }
        private static DateTime Utc(string s)=>DateTime.ParseExact(s.Trim(),new[]{"yyyy-MM-ddTHH:mm:ss'Z'","yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);
        private static double Number(string s){var n=double.Parse(s.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture);if(!Finite(n))throw new FormatException("Nonfinite value");return n;}
        private static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        protected override void OnStop(){Print("T088_STOP,censored_next_quote_samples={0},unreached_prints={1}",_roundtrips.Count,_trades.Count-_next);}
    }
}
