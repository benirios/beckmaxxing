// Synthetic event harness only. Production builds reference the real cTrader API.
namespace cAlgo.API
{
    [AttributeUsage(AttributeTargets.Class)] public class RobotAttribute : Attribute { public string TimeZone {get;set;} public AccessRights AccessRights {get;set;} }
    [AttributeUsage(AttributeTargets.Property)] public class ParameterAttribute : Attribute { public ParameterAttribute(string name) {} public object DefaultValue {get;set;} public double MinValue {get;set;} public double Step {get;set;} public string Group {get;set;} }
    public static class TimeZones { public const string UTC="UTC"; public const string TokyoStandardTime="Tokyo"; }
    public enum AccessRights { None }
    public enum RunningMode { RealTime, SilentBacktesting }
    public enum TradeType { Buy, Sell }
    public enum ProtectionType { Absolute }
    public enum RoundingMode { Down }
    public class ServerClock { public DateTime Time; }
    public class Quote
    {
        public double Bid=2000, Ask=2000.2, TickSize=0.01, PipSize=0.01, VolumeInUnitsMin=1, VolumeInUnitsMax=100;
        public double VolumeForFixedRisk(double risk,double pips,RoundingMode mode)=>Math.Floor(risk/(pips*PipSize));
        public double AmountRisked(double volume,double pips)=>volume*pips*PipSize;
        public double NormalizeVolumeInUnits(double v,RoundingMode m)=>Math.Floor(v);
    }
    public class Position { public string Label, SymbolName; public int Id; public double EntryPrice, NetProfit, VolumeInUnits; public DateTime EntryTime; public double? StopLoss, TakeProfit; public TradeType TradeType; }
    public class PositionClosedEventArgs : EventArgs { public Position Position; public PositionCloseReason Reason=PositionCloseReason.Closed; }
    public class PositionList : List<Position>
    {
        public event Action<PositionClosedEventArgs> Closed;
        public event Action<PositionOpenedEventArgs> Opened;
        public new void Add(Position p){base.Add(p);Opened?.Invoke(new PositionOpenedEventArgs{Position=p});}
        public Position[] FindAll(string label,string symbol)=>this.Where(p=>p.Label==label && p.SymbolName==symbol).ToArray();
        public void Notify(Position p,PositionCloseReason reason=PositionCloseReason.Closed)=>Closed?.Invoke(new PositionClosedEventArgs{Position=p,Reason=reason});
    }
    public class TradeResult { public bool IsSuccessful; public Position Position; public string Error; }
    public partial class Robot
    {
        public RunningMode RunningMode {get;set;}=RunningMode.SilentBacktesting;
        public ServerClock Server {get;}=new();
        public Quote Symbol {get;}=new();
        public PositionList Positions {get;}=new();
        public string SymbolName=>"XAUUSD";
        public List<string> Logs=new();
        public bool Stopped, FailClose;
        public int Orders;
        protected virtual void OnBar(){} protected virtual void OnStart(){} protected virtual void OnTick(){} protected virtual void OnStop(){}
        public void Start()=>OnStart(); public void Tick(DateTime time,double mid=2000){Server.Time=time;Symbol.Bid=mid-0.1;Symbol.Ask=mid+0.1;PrepareNative();OnTick();} public void End()=>OnStop();
        protected void Stop()=>Stopped=true;
        protected void Print(string format,params object[] args)=>Logs.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,format,args));
        protected TradeResult ExecuteMarketOrder(TradeType side,string symbol,double volume,string label,double? stop,double? target,string comment)
        {
            Orders++;var p=new Position {Id=Orders,EntryTime=Server.Time,TakeProfit=target.HasValue?(double?)(side==TradeType.Buy?Symbol.Ask+target.Value*Symbol.PipSize:Symbol.Bid-target.Value*Symbol.PipSize):null,StopLoss=stop.HasValue?(double?)(side==TradeType.Buy?Symbol.Ask-stop.Value*Symbol.PipSize:Symbol.Bid+stop.Value*Symbol.PipSize):null,VolumeInUnits=volume,Label=label,SymbolName=symbol,TradeType=side,EntryPrice=side==TradeType.Buy?Symbol.Ask:Symbol.Bid};Positions.Add(p);
            return new TradeResult{IsSuccessful=true,Position=p};
        }
        protected TradeResult ModifyPosition(Position p,double? stop,double? target,ProtectionType type){p.StopLoss=stop;p.TakeProfit=target;return new TradeResult{IsSuccessful=true,Position=p};}
        protected TradeResult ClosePosition(Position position)=>CloseModeled(position,position.VolumeInUnits,PositionCloseReason.Closed,false);
        protected TradeResult ClosePosition(Position position,double volume)=>CloseModeled(position,volume,PositionCloseReason.Closed,false);

    }
}
