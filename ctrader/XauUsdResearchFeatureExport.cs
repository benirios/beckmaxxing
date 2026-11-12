using System;
using System.Globalization;
using System.IO;
using cAlgo.API;
namespace cAlgo.Robots
{
    // Read-only aggregation of native historical bid/ask ticks. No order methods.
    [Robot(TimeZone=TimeZones.UTC, AccessRights=AccessRights.FullAccess)]
    public class XauUsdResearchFeatureExport : Robot
    {
        [Parameter("Output directory", DefaultValue="/Users/beni/Dev/XAUUSD/data/processed/workaround-20261111-v2")]
        public string OutputDirectory {get;set;}
        private StreamWriter _writer,_reference,_invalidWriter;
        private Bars _equity;
        private int _lastRef=-1;
        private DateTime _minute=DateTime.MinValue,_first,_last;
        private double _o,_h,_l,_c,_bo,_bh,_bl,_bc,_ao,_ah,_al,_ac,_sum,_sum2,_maxGap;
        private long _n,_total,_minutes,_invalidCount;
        private string N(double x) {return x.ToString("R",CultureInfo.InvariantCulture);}
        private string D(DateTime x) {return DateTime.SpecifyKind(x,DateTimeKind.Utc).ToString("O",CultureInfo.InvariantCulture);}
        protected override void OnStart()
        {
            if(RunningMode==RunningMode.RealTime) {Print("FEATURE_EXPORT_ERROR,research_only");Stop();return;}
            Directory.CreateDirectory(OutputDirectory);
            // Never overwrite an earlier immutable extraction.
            _writer=new StreamWriter(new FileStream(Path.Combine(OutputDirectory,"gold_m1.csv"),FileMode.CreateNew),System.Text.Encoding.UTF8);
            _writer.WriteLine("timestamp,first_utc,last_utc,open,high,low,close,bid_open,bid_high,bid_low,bid_close,ask_open,ask_high,ask_low,ask_close,ticks,sum_mid,sum_mid_squared,max_gap_seconds,complete");
            _invalidWriter=new StreamWriter(new FileStream(Path.Combine(OutputDirectory,"invalid_quotes.csv"),FileMode.CreateNew),System.Text.Encoding.UTF8);
            _invalidWriter.WriteLine("timestamp,bid,ask");
            _reference=new StreamWriter(new FileStream(Path.Combine(OutputDirectory,"us500_m5.csv"),FileMode.CreateNew),System.Text.Encoding.UTF8);
            _reference.WriteLine("timestamp,observed_at_utc,open,high,low,close");
            _equity=MarketData.GetBars(TimeFrame.Minute5,"US500.cash");
            Print("FEATURE_EXPORT_START,build=AGGREGATE_BID_ASK_V2,orders=none,output={0}",OutputDirectory);
        }
        protected override void OnTick()
        {
            var t=Server.TimeInUtc;var b=Symbol.Bid;var a=Symbol.Ask;
            if(!double.IsFinite(b)||!double.IsFinite(a)||b<=0||a<b){_invalidWriter.WriteLine(D(t)+","+N(b)+","+N(a));_invalidCount++;return;}
            var m=new DateTime(t.Year,t.Month,t.Day,t.Hour,t.Minute,0,DateTimeKind.Utc);var mid=(a+b)/2;
            if(_minute!=m)
            {
                if(_minute!=DateTime.MinValue)Flush(true);
                _minute=m;_first=t;_n=0;_maxGap=0;
                _o=_h=_l=_c=mid;_bo=_bh=_bl=_bc=b;_ao=_ah=_al=_ac=a;_sum=_sum2=0;
            }
            if(_n>0)_maxGap=Math.Max(_maxGap,(t-_last).TotalSeconds);
            _h=Math.Max(_h,mid);_l=Math.Min(_l,mid);_c=mid;
            _bh=Math.Max(_bh,b);_bl=Math.Min(_bl,b);_bc=b;
            _ah=Math.Max(_ah,a);_al=Math.Min(_al,a);_ac=a;
            _last=t;_sum+=mid;_sum2+=mid*mid;_n++;_total++;
        }
        protected override void OnBar()
        {
            if(_equity==null)return;
            for(int i=_lastRef+1;i<_equity.Count-1;i++)
            {
                var t=_equity.OpenTimes[i];if(t.AddMinutes(5)>Server.TimeInUtc)break;
                _reference.WriteLine(string.Join(",",D(t),D(Server.TimeInUtc),N(_equity.OpenPrices[i]),N(_equity.HighPrices[i]),N(_equity.LowPrices[i]),N(_equity.ClosePrices[i])));
                _lastRef=i;
            }
        }
        private void Flush(bool complete)
        {
            _writer.WriteLine(string.Join(",",D(_minute),D(_first),D(_last),N(_o),N(_h),N(_l),N(_c),N(_bo),N(_bh),N(_bl),N(_bc),N(_ao),N(_ah),N(_al),N(_ac),_n,N(_sum),N(_sum2),N(_maxGap),complete?1:0));
            _minutes++;
        }
        protected override void OnStop()
        {
            if(_writer!=null){if(_n>0)Flush(false);_writer.Dispose();}
            if(_reference!=null)_reference.Dispose();
            if(_invalidWriter!=null)_invalidWriter.Dispose();
            Print("FEATURE_EXPORT_INVALID_QUOTES,count={0},excluded_windows_require_sidecar=1",_invalidCount);
            Print("FEATURE_EXPORT_DONE,minutes={0},ticks={1},reference_bars={2},orders=none",_minutes,_total,_lastRef+1);
        }
    }
}
