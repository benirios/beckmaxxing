using cAlgo.API;
using cAlgo.Robots;
using System.Reflection;
static class TokyoWindowChecks
{
    static void BarEvent(Robot r)=>typeof(XauUsdTokyoPressureReversal).GetMethod("OnBar",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(r,null);
    public static int Run()
    {
        int checks=0;
        foreach(bool tickFirst in new[]{true,false})
        foreach(bool missing in new[]{false,true})
        {
            var r=new XauUsdTokyoPressureReversal {PositionLabel="test",FixedVolumeInUnits=1};
            r.Start();
            var d=new DateTime(2020,5,7,9,0,0);
            // Seed prior-only history so boundary handling must reach a real order.
            foreach(var field in new[]{"_priorAbsReturns","_priorRanges"})
            {
                var q=(Queue<double>)typeof(XauUsdTokyoPressureReversal).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(r);
                for(int j=0;j<(field=="_priorRanges"?20:60);j++)q.Enqueue(field=="_priorRanges"?10:0.0001);
            }
            r.Bars.Items.Add(new Bar {OpenTime=d.AddMinutes(-1),High=2001,Low=1999});
            for(int i=0;i<=30;i++)
            {
                if(missing && i==12)continue;
                r.Bars.Items.Add(new Bar {OpenTime=d.AddMinutes(i),High=2001+i,Low=1999+i});
                if(!tickFirst)BarEvent(r);
                r.Tick(d.AddMinutes(i),2000+i);
                if(tickFirst)BarEvent(r);
            }
            void Check(bool ok,string msg){if(!ok)throw new Exception(msg);checks++;}
            Check(r.Orders==(missing?0:1),"Tokyo boundary order count, tickFirst="+tickFirst+", missing="+missing);
            Check(r.Logs.Any(x=>x.Contains(missing?"incomplete_opening_window":"bars=30")),"Window completeness diagnostic");
            r.Tick(d.AddMinutes(30).AddSeconds(1),2030);
            Check(r.Orders==(missing?0:1),"No duplicate entry");
            if(!missing)Check(r.Positions[0].TradeType==TradeType.Sell,"Positive opening move is faded");
        }
        return checks;
    }
}
