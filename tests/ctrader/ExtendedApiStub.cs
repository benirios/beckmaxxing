namespace cAlgo.API
{
    public enum PositionCloseReason { Closed, StopLoss, TakeProfit }
    public class HistoricalTrade { public int PositionId; public long ClosingDealId; public DateTime ClosingTime; public double ClosingPrice,NetProfit,VolumeInUnits; }
    public class HistoryProvider { public List<HistoricalTrade> Trades=new(); public HistoricalTrade[] FindByPositionId(int id)=>Trades.Where(t=>t.PositionId==id).ToArray(); }
    public enum TimeFrame { Minute, Minute5, Minute15 }
    public class DataSeries : List<double> { }
    public class Bar { public DateTime OpenTime; public double High,Low; }
    public class Bars
    {
        public List<Bar> Items=new();
        public Bar this[int i]=>Items[i];
        public Bar LastBar=>Items[^1];
        public List<DateTime> OpenTimes=new();
        public DataSeries ClosePrices=new();
        public int Count=>Items.Count>0?Items.Count:OpenTimes.Count;
    }
    public class MarketDataProvider
    {
        public Dictionary<TimeFrame,Bars> Series=new();
        public Bars GetBars(TimeFrame timeframe,string symbol)=>Series[timeframe];
    }
    public class IndicatorProvider
    {
        public DataSeries EmaValues=new();
        public cAlgo.API.Indicators.ExponentialMovingAverage ExponentialMovingAverage(DataSeries series,int periods)=>new(){Result=EmaValues};
    }
    public class PositionOpenedEventArgs : EventArgs { public Position Position; }
    public class PendingOrder
    {
        public double VolumeInUnits=>Volume; public bool StopLimit,Triggered; public double Range;
        public int Id; public string Label,SymbolName; public TradeType TradeType;
        public double Volume,TargetPrice; public double? Stop,Target; public DateTime? Expiry;
    }
    public class PendingOrderFilledEventArgs : EventArgs { public PendingOrder PendingOrder; public Position Position; }
    public class PendingList : List<PendingOrder> { public event Action<PendingOrderFilledEventArgs> Filled; public void Notify(PendingOrder o,Position p)=>Filled?.Invoke(new(){PendingOrder=o,Position=p}); }
    public partial class Robot
    {
        public Bars Bars {get;}=new();
        public TimeFrame TimeFrame {get;set;}=TimeFrame.Minute;
        public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
        public HistoryProvider History {get;}=new();
        public MarketDataProvider MarketData {get;}=new();
        public IndicatorProvider Indicators {get;}=new();
        public PendingList PendingOrders {get;}=new();
        public bool FailCancel,FailStopLimit; public double StopLimitFillFraction=1;
        public int LimitSubmissions;
        protected TradeResult PlaceLimitOrder(TradeType side,string symbol,double units,double limit,string label,double? stop,double? target,ProtectionType? protection,DateTime? expiry,string comment)
        {
            LimitSubmissions++;PendingOrders.Add(new(){Id=LimitSubmissions,Label=label,SymbolName=symbol,TradeType=side,Volume=units,TargetPrice=limit,Stop=stop,Target=target,Expiry=expiry});
            return new(){IsSuccessful=true};
        }
        protected TradeResult PlaceStopLimitOrder(TradeType side,string symbol,double units,double trigger,double range,string label,double? stop,double? target,ProtectionType? protection,DateTime? expiry,string comment)
        {
            if(FailStopLimit)return new(){IsSuccessful=false,Error="SyntheticReject"};
            LimitSubmissions++;PendingOrders.Add(new(){Id=LimitSubmissions,Label=label,SymbolName=symbol,TradeType=side,Volume=units,TargetPrice=trigger,Range=range,StopLimit=true,Expiry=expiry});return new(){IsSuccessful=true};
        }
        protected TradeResult CancelPendingOrder(PendingOrder order){if(FailCancel)return new(){IsSuccessful=false,Error="SyntheticReject"};PendingOrders.Remove(order);return new(){IsSuccessful=true};}
        private void PrepareNative()
        {
            foreach(var position in Positions.ToArray())
            {
                var price=position.TradeType==TradeType.Buy?Symbol.Bid:Symbol.Ask;
                var side=position.TradeType==TradeType.Buy?1:-1;
                if(position.StopLoss.HasValue && side*(price-position.StopLoss.Value)<=0)
                    CloseModeled(position,position.VolumeInUnits,PositionCloseReason.StopLoss,true);
                else if(position.TakeProfit.HasValue && side*(price-position.TakeProfit.Value)>=0)
                    CloseModeled(position,position.VolumeInUnits,PositionCloseReason.TakeProfit,true);
            }

            foreach(var order in PendingOrders.ToArray())
            {
                if(order.Expiry.HasValue && Server.Time>=order.Expiry.Value){PendingOrders.Remove(order);continue;}
                if(order.StopLimit)
                {
                    var price=order.TradeType==TradeType.Buy?Symbol.Ask:Symbol.Bid;
                    order.Triggered|=order.TradeType==TradeType.Buy?price>=order.TargetPrice:price<=order.TargetPrice;
                    var within=order.TradeType==TradeType.Buy?price<=order.TargetPrice+order.Range*Symbol.PipSize:price>=order.TargetPrice-order.Range*Symbol.PipSize;
                    if(!order.Triggered||!within)continue;
                    var opposite=Positions.FirstOrDefault(p=>p.SymbolName==order.SymbolName&&p.TradeType!=order.TradeType);
                    if(Account.AccountType==cAlgo.API.Internals.AccountType.Netted&&opposite!=null)
                    {
                        var volume=Math.Min(order.Volume,opposite.VolumeInUnits)*StopLimitFillFraction;
                        if(volume<=0)continue;
                        CloseModeled(opposite,volume,PositionCloseReason.Closed,true);order.Volume-=volume;
                        if(order.Volume<=1e-9)PendingOrders.Remove(order);
                        PendingOrders.Notify(order,opposite);continue;
                    }
                    throw new Exception("Synthetic stop-limit requires explicit netting; unsupported model");
                }
                if(order.TradeType==TradeType.Buy?Symbol.Ask<=order.TargetPrice:Symbol.Bid>=order.TargetPrice)
                {
                    PendingOrders.Remove(order);Orders++;
                    Positions.Add(new(){Id=Orders,Label=order.Label,SymbolName=order.SymbolName,TradeType=order.TradeType,EntryTime=Server.Time,EntryPrice=order.TargetPrice,VolumeInUnits=order.Volume,StopLoss=order.Stop,TakeProfit=order.Target});
                }
            }
        }
    }
}
namespace cAlgo.API.Indicators
{
    public class ExponentialMovingAverage { public cAlgo.API.DataSeries Result; }
}
