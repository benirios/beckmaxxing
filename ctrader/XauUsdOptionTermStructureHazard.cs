using System;
using System.Collections.Generic;
using System.Globalization;
using cAlgo.API;

namespace cAlgo.Robots
{
    /// <summary>
    /// T089: Received pre-10:00 NY GC option term-structure inversion adds 10:00-11:00 jump hazard beyond IV level, skew, own variance and known events. Require matched forward-adjusted moneyness, explicit actual maturities and a frozen quote-age cutoff. Compare earlier-trained level-only and slope-augmented probabilities. The declared minute-return jump test uses predictable continuous variance and prior-calibrated window significance, not future outcome-window variance.
    /// Observation cBot: measures the stated risk/forecast hypothesis without inventing alpha.
    /// Input: UTC receipt | short_expiry_iv,long_expiry_iv,skew,prior_gold_variance,event_flag,iv_level_probability,slope_probability,predecision_minute_continuous_variance,frozen_critical_z,surface_age_seconds,frozen_max_surface_age_seconds,same_moneyness_forward_adjusted,short_maturity_years,long_maturity_years; one event per line or semicolon.
    /// Receipt is when ALL inputs were known, not an exchange or article creation timestamp.
    /// Forecasts are decimal-return variance (or decimal excursion for T030), not dollars.
    /// Requires historical broker ticks. M1/M5 OHLC cannot validate this observation stream.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XauUsdOptionTermStructureHazard : Robot
    {
        [Parameter("Point-in-time observations", DefaultValue = "")]
        public string Observations { get; set; }
        private sealed class Event
        {
            public DateTime Receipt;
            public double[] Values;
        }
        private sealed class Sample
        {
            public Event Event;
            public DateTime Deadline, NextMinute, LastTick;
            public double StartMid, LastMinuteMid, High, Low, Rv, MaxSpread, SpreadSum, MaxAbsMinute;
            public int Intervals, Ticks;
            public bool Valid = true;
        }
        private readonly List<Event> _events = new List<Event>();
        private readonly List<Sample> _active = new List<Sample>();
        private int _next;
        protected override void OnStart()
        {
            if (RunningMode == RunningMode.RealTime)
            {
                Print("T089_STOP,reason=research_only"); Stop(); return;
            }
            try
            {
                var seen = new HashSet<DateTime>();
                foreach (var row in (Observations ?? "").Split(new[]{';','\n','\r'}, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cells = row.Trim().Split('|');
                    DateTime receipt;
                    if (cells.Length != 14 + 1 || !DateTime.TryParseExact(cells[0].Trim(), new[] { "yyyy-MM-ddTHH:mm:ss'Z'", "yyyy-MM-ddTHH:mm:ss.FFFFFFF'Z'" }, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out receipt) || !seen.Add(receipt))
                        throw new FormatException("Invalid/duplicate receipt or input column count: " + row);
                    var values = new double[14];
                    for (int i=0;i<values.Length;i++)
                        if (!double.TryParse(cells[i+1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                            throw new FormatException("Input must be finite invariant-culture decimal");
                    var v=values;
                    var ny=TimeZoneInfo.ConvertTimeFromUtc(receipt,TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));if(ny.TimeOfDay!=new TimeSpan(10,0,0)||v[0]<=0||v[1]<=0||v[3]<0||(v[4]!=0&&v[4]!=1)||v[5]<0||v[5]>1||v[6]<0||v[6]>1||v[7]<=0||v[8]<=0||v[9]<=0||v[10]<=0||v[9]>v[10]||v[11]!=1||v[12]<=0||v[13]<=v[12])throw new FormatException("Predecision matched option surfaces and valid forecasts required");
                    _events.Add(new Event { Receipt=receipt, Values=values });
                }
                if (_events.Count == 0) throw new FormatException("Verified point-in-time inputs required: short_expiry_iv,long_expiry_iv,skew,prior_gold_variance,event_flag,iv_level_probability,slope_probability,predecision_minute_continuous_variance,frozen_critical_z,surface_age_seconds,frozen_max_surface_age_seconds,same_moneyness_forward_adjusted,short_maturity_years,long_maturity_years");
                _events.Sort((a,b)=>a.Receipt.CompareTo(b.Receipt));
            }
            catch (Exception e) { Print("T089_STOP,reason=configuration,error={0}",e.Message); Stop(); return; }
            Print("T089_START,build=V1_20261107,events={0},horizon_minutes=60,fields=short_expiry_iv,long_expiry_iv,skew,prior_gold_variance,event_flag,iv_level_probability,slope_probability,predecision_minute_continuous_variance,frozen_critical_z,surface_age_seconds,frozen_max_surface_age_seconds,same_moneyness_forward_adjusted,short_maturity_years,long_maturity_years",_events.Count);
        }
        protected override void OnTick()
        {
            var now=DateTime.SpecifyKind(Server.Time,DateTimeKind.Utc);
            if (Symbol.Bid<=0 || Symbol.Ask<Symbol.Bid || double.IsNaN(Symbol.Ask) || double.IsNaN(Symbol.Bid))
            {
                foreach(var active in _active) active.Valid=false;
                return;
            }
            var mid=(Symbol.Bid+Symbol.Ask)/2;
            var spread=Symbol.Ask-Symbol.Bid;
            for(int i=_active.Count-1;i>=0;i--)
            {
                var sample=_active[i];
                if (now>=sample.Deadline)
                {
                    // The endpoint quote is not part of intrawindow highs/lows unless timely.
                    if ((now-sample.Deadline).TotalSeconds>2) sample.Valid=false;
                    else Update(sample,now,mid,spread);
                    Finish(sample,now,mid);
                    _active.RemoveAt(i);
                }
                else Update(sample,now,mid,spread);
            }
            while(_next<_events.Count && _events[_next].Receipt<now)
            {
                var ev=_events[_next++];
                if((now-ev.Receipt).TotalSeconds>2)
                {
                    Print("T089_SKIP,receipt={0:O},reason=missing_timely_start_quote,quote={1:O}",ev.Receipt,now); continue;
                }
                _active.Add(new Sample { Event=ev,Deadline=ev.Receipt.AddMinutes(60),NextMinute=ev.Receipt.AddMinutes(1),
                    LastTick=now,StartMid=mid,LastMinuteMid=mid,High=mid,Low=mid,MaxSpread=spread,SpreadSum=spread,Ticks=1 });
                Print("T089_BEGIN,receipt={0:O},quote={1:O},mid={2},inputs={3}",ev.Receipt,now,mid,string.Join("|",Array.ConvertAll(ev.Values,x=>x.ToString("R",CultureInfo.InvariantCulture))));
            }
        }
        private void Update(Sample sample,DateTime now,double mid,double spread)
        {
            if((now-sample.LastTick).TotalSeconds>60) sample.Valid=false;
            sample.LastTick=now;
            sample.High=Math.Max(sample.High,mid); sample.Low=Math.Min(sample.Low,mid);
            sample.MaxSpread=Math.Max(sample.MaxSpread,spread); sample.SpreadSum+=spread; sample.Ticks++;
            if(now>=sample.NextMinute)
            {
                if((now-sample.NextMinute).TotalSeconds>2) sample.Valid=false;
                var change=Math.Log(mid/sample.LastMinuteMid);
                sample.MaxAbsMinute=Math.Max(sample.MaxAbsMinute,Math.Abs(change));sample.Rv+=change*change; sample.LastMinuteMid=mid; sample.Intervals++;
                sample.NextMinute=sample.NextMinute.AddMinutes(1);
            }
        }
        private void Finish(Sample sample,DateTime now,double endMid)
        {
            if(!sample.Valid || sample.Intervals!=60)
            {
                Print("T089_INVALID,receipt={0:O},quote={1:O},intervals={2},reason=quote_coverage",sample.Event.Receipt,now,sample.Intervals);return;
            }
            var rv=sample.Rv;
            Print("T089_OUTCOME,receipt={0:O},end_quote={1:O},return={2},realized_variance={3},up_excursion={4},down_excursion={5},max_spread={6},tick_mean_spread={7},ticks={8}",
                sample.Event.Receipt,now,endMid/sample.StartMid-1,rv,sample.High/sample.StartMid-1,1-sample.Low/sample.StartMid,sample.MaxSpread,sample.SpreadSum/sample.Ticks,sample.Ticks);
            var hit=sample.MaxAbsMinute>=Math.Sqrt(sample.Event.Values[7])*sample.Event.Values[8]?1.0:0.0;for(int i=5;i<=6;i++)Print("T089_HAZARD,receipt={0:O},inversion={1},model={2},jump_test={3},brier={4}",sample.Event.Receipt,sample.Event.Values[0]>sample.Event.Values[1],i,hit,Math.Pow(hit-sample.Event.Values[i],2));
        }
        protected override void OnStop()
        {
            foreach(var sample in _active) Print("T089_CENSORED,receipt={0:O},reason=run_ended_before_complete_outcome",sample.Event.Receipt);
            Print("T089_STOP,unreached_events={0}",_events.Count-_next);
        }
    }
}
