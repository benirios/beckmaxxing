namespace cAlgo.API.Internals
{
    public enum AccountType { Hedged, Netted }
    public class TestAsset { public string Name="USD"; }
    public class TestAccount
    {
        public TestAsset Asset=new();
        public AccountType AccountType=AccountType.Hedged;
        public double Balance=10000,CommissionPerUnitRoundtrip;
        public Func<double> Floating;
        public double Equity=>Balance+(Floating?.Invoke()??0);
    }
}
namespace cAlgo.API
{
    public partial class Robot
    {
        public cAlgo.API.Internals.TestAccount Account {get;}=new();
        public Robot()
        {
            Account.Floating=()=>Positions.Sum(p=>((p.TradeType==TradeType.Buy?Symbol.Bid-p.EntryPrice:p.EntryPrice-Symbol.Ask)-Account.CommissionPerUnitRoundtrip)*p.VolumeInUnits);
        }
        private TradeResult CloseModeled(Position position,double volume,PositionCloseReason reason,bool native)
        {
            if((FailClose&&!native)||volume<=0||volume>position.VolumeInUnits)return new(){IsSuccessful=false,Error="SyntheticReject"};
            var exit=position.TradeType==TradeType.Buy?Symbol.Bid:Symbol.Ask;
            var net=((position.TradeType==TradeType.Buy?1:-1)*(exit-position.EntryPrice)-Account.CommissionPerUnitRoundtrip)*volume;
            Account.Balance+=net;History.Trades.Add(new HistoricalTrade{PositionId=position.Id,ClosingPrice=exit,ClosingDealId=History.Trades.Count+1,ClosingTime=Server.Time,NetProfit=net,VolumeInUnits=volume});
            if(volume>=position.VolumeInUnits-1e-9)
            {
                position.NetProfit=net;Positions.Remove(position);Positions.Notify(position,reason);
            }
            else position.VolumeInUnits-=volume;
            return new(){IsSuccessful=true,Position=position};
        }
    }
}
