"""Past-only forecasting/control inputs for frozen native adapters."""
from research.prepare_workaround_features import *
from collections import defaultdict,deque
from bs4 import BeautifulSoup

def fomc_presets(q):
 statements=json.loads((REF/'fomc_statements.json').read_text());actual=[];events=set();rows=[]
 for ev in statements:
  day=datetime.strptime(ev['date'],'%Y%m%d').date()
  if '2:00 p.m.' in ev['header']:events.add(day);actual.append(utc(clock(day,'14:00',NY)))
  elif '10:00 a.m.' in ev['header']:actual.append(utc(clock(day,'10:00',NY)))
  else:raise ValueError('Unverified official statement publication time')
 for d in sorted(events):
  for day,is_event in [(d,1),(d-timedelta(days=7),0)]:
   if str(day) not in q.nyse or (not is_event and day in events):continue
   t=clock(day,'12:00',NY);stop=q.stop(t)
   if stop:rows.append(row(t,t+timedelta(minutes=115),stop,[is_event,840,1-is_event]))
 rows=sorted(set(rows));p=save_rows('t091-features',rows)
 for i,branch in enumerate(['primary','opposite','random','nonfomc','notrade']):
  register('T091','t091-'+branch,'Pre-FOMC '+branch,{'FeatureRows':'\n'.join(rows),'ReleaseReceiptsUtc':','.join(actual),'Variant':i,'RiskBudget':50},[DATA/'gold_m1.csv',DATA/'invalid_quotes.csv',REF/'fomc_statements.json',p],['Official publication clock proxy; no measured network delivery timestamps. Small sample; same-weekday controls selected by calendar only.'])

def sameclock_presets(q):
 grids=[('LDN0900',LON,'09:00'),('NY1000',NY,'10:00')];hist={g[0]:[] for g in grids};rows=[];pastrows=[];training_audit=[]
 for d in dates():
  if d.weekday()>=5:continue
  for block,zone,tm in grids:
   if zone==NY and str(d) not in q.nyse:continue
   t=clock(d,tm,zone);end=t+timedelta(minutes=30);stop=q.stop(t);history=hist[block]
   # Completed PREVIOUS session/day momentum, not today's future daily return.
   momentum=q.move(t-timedelta(hours=24),t)
   if momentum is None:
    # No gap fill or shortened-day stand-in. A momentum feature is unavailable.
    momentum=None
   eligible=[h for h in history if h['date']<d and h['available']<=t]
   latest=eligible[-60:];training=[h for h in eligible if h.get('x') is not None][-60:]
   x=None
   if len(latest)>=20:
    mean=float(np.mean([h['r'] for h in latest]));blockmean=float(np.mean([h['r'] for h in latest[-20:]]));adj=[h['adjacent'] for h in latest[-20:] if h['adjacent'] is not None]
    # Daily momentum uses prior completed same-local-clock 24h boundary return;
    # intervening maintenance is an audited calendar closure, not invented bars.
    old=q.boundary(t-timedelta(hours=24));cur=q.boundary(t)
    momentum=cur/old-1 if old and cur else None
    if momentum is not None:x=[momentum,blockmean-mean]
    if len(training)==60 and x is not None and len(adj)==20 and stop:
     _,_,beta,sd=ridge([h['x'] for h in training],[h['r'] for h in training],x)
     momcoef=float(beta[1]/sd[0]);blockcoef=max(0,float(beta[2]/sd[1]));seed=int(hashlib.sha256((str(d)+block).encode()).hexdigest()[:8],16)
     shuffled=float(np.mean(np.random.default_rng(seed).permutation([h['r'] for h in latest])[:20]));adjmean=float(np.mean(adj))
     rows.append('|'.join([utc(t),utc(end),fmt(stop),block]+[fmt(v) for v in [mean,momentum,momcoef,0,shuffled,adjmean,blockcoef]]))
     training_audit.append({'strategy':'T097','receipt':utc(t),'latest_training_outcome_available':utc(training[-1]['available']),'training_rows':60})
   # A completed return is appended only after the current feature row is fixed.
   actual=q.move(t,end);adjacent=q.move(t-timedelta(minutes=30),t)
   if actual is not None:
    history.append({'date':d,'available':end,'r':actual,'adjacent':adjacent,'x':x})
    pastrows.append('|'.join([utc(end),str(d),block,fmt(actual)]))
 p=save_rows('t097-features',rows);hp=save_rows('t097-completed-blocks',pastrows);write_json(OUT/'t097-training-audit.json',training_audit)
 grid=';'.join(block+'|'+zone.key+'|'+tm for block,zone,tm in grids)
 for i,branch in enumerate(['primary','opposite','random','constantclock','shuffled','adjacent']):register('T097','t097-'+branch,'Same-clock persistence '+branch,{'FeatureRows':'\n'.join(rows),'PastBlockReturns':'\n'.join(pastrows),'BlockGrid':grid,'Variant':i,'RiskBudget':50},[DATA/'gold_m1.csv',DATA/'invalid_quotes.csv',p,hp],['History includes all completed block records; canonical adapter explicitly filters completion availability and strictly earlier local dates.','Boundary daily momentum spans known broker maintenance; no bars are filled.'])

def stats(r):
 rv=float(np.sum(r*r));neg=float(np.sum(r[r<0]**2));bp=float(math.pi/2*np.sum(abs(r[1:])*abs(r[:-1])))
 jump=min(rv,max(0,rv-bp));return rv,neg,jump

def observation_presets(q):
 result={k:[] for k in ['T004','T011','T032','T040','T056','T082']};audits=[]
 # T004: prior15m predictor/variance, prior60-session same-clock median state.
 history={i:deque(maxlen=60) for i in [0,1,2]}
 for d in dates():
  if d.weekday()>=5:continue
  for group,zone,tm in [(0,TOK,'09:30'),(1,LON,'08:30'),(2,NY,'10:00')]:
   if group==2 and str(d) not in q.nyse:continue
   t=clock(d,tm,zone);r=q.returns(t-timedelta(minutes=15),t,1)
   if r is None:continue
   rv=float(np.sum(r*r));h=history[group]
   if len(h)==60:result['T004'].append(observations(t,[math.expm1(float(np.sum(r))),rv,int(rv>np.median(h)),group]))
   h.append(rv)
 # T011: actual received native reference M5 bars; positive/negative tails fixed.
 ref=pd.read_csv(DATA/'us500_m5.csv',encoding='utf-8-sig')
 for c in ['timestamp','observed_at_utc']:ref[c]=pd.to_datetime(ref[c],utc=True,format='ISO8601')
 prior=deque(maxlen=4680)
 for bar in ref.itertuples(index=False):
  local=bar.timestamp.tz_convert(NY);tm=local.hour*60+local.minute
  if str(local.date()) not in q.nyse or not 570<=tm<960:continue
  change=bar.close/bar.open-1
  if len(prior)==4680:
   ordered=sorted(prior);lo=ordered[math.ceil(.01*len(prior))-1];hi=ordered[math.ceil(.99*len(prior))-1]
   sign=-1 if change<0 and change<=lo else 1 if change>0 and change>=hi else 0
   if sign:
    close=bar.timestamp+5*M;t=max(close,bar.observed_at_utc).ceil('s');gold=q.move(bar.timestamp,close)
    if gold is not None and t<stamp(END):result['T011'].append(observations(t,[sign,change,abs(lo) if sign<0 else abs(hi),gold]))
  prior.append(change)
 # Valid, completed broker sessions for slow variance. NY18:00–next17:00,23h;
 # Friday early closure/holidays/missing minutes are not filled or inferred.
 closed=[]
 for d in dates():
  if d.weekday()>=5:continue
  end=clock(d,'17:00',NY);begin=clock(d-timedelta(days=1),'18:00',NY);w=q.window(begin,end)
  if w is not None and (stamp(end)-w.iloc[-1].last_utc).total_seconds()<=2:
   # Actual observed M1 close samples; no missing bars or clock interpolation.
   values=np.r_[float(w.iloc[0].open),w.close.to_numpy()];r=np.diff(np.log(values))
   closed.append({'end':end,'variance':float(np.sum(r*r)),'date':d})
 histories={k:deque(maxlen=60) for k in ['T032','T040','T056','T082']}
 # All-minute predictable jump labels for T056: variance from strictly preceding60.
 closes=q.m.close.where(q.m.valid);changes=np.log(closes).diff();contiguous=q.m.index.to_series().diff().eq(M)
 changes=changes.where(contiguous);pv=(changes**2).rolling(60,min_periods=60).mean().shift(1)
 arrivals=(abs(changes)>3.5*np.sqrt(pv)).astype(float).where(pv.notna())
 for d in dates():
  if str(d) not in q.nyse:continue
  t=clock(d,'11:00',NY);r=q.returns(t-timedelta(minutes=60),t,1)
  if r is None:continue
  rv,neg,jump=stats(r)
  if rv<=0:continue
  exc=2*math.sqrt(rv/2);minutevar=rv/60;features={}
  features['T032']={'raw':[rv,jump,neg,exc],'base':[math.log(rv),neg/rv],'aug':[math.log(rv),neg/rv,jump/rv]}
  r5=q.returns(clock(d,'09:00',NY),t,5)
  if r5 is not None:
   sd=float(np.std(r5));skew=float(np.mean((r5-r5.mean())**3)/sd**3) if sd>0 else 0;var5,n5,j5=stats(r5);ret=math.expm1(float(np.sum(r5)))
   base=[ret,math.log(max(var5,1e-16)),j5/max(var5,1e-16),n5/max(var5,1e-16)]
   features['T040']={'raw':[skew,ret,var5,j5/max(var5,1e-16),n5,.05],'base':base,'aug':base+[skew]}
  start=stamp(t)-360*M;arr=arrivals.loc[start:stamp(t)-M];context=q.window(start,t)
  if len(arr)==360 and arr.notna().all() and context is not None:
   ages=np.arange(len(arr)-1,-1,-1);intensity=float(np.sum(arr.to_numpy()*np.exp(-math.log(2)*ages/30)))
   pastmag=float(np.max(abs(r)));base=[math.log(rv),pastmag]
   features['T056']={'raw':[intensity,rv,pastmag],'base':base,'aug':base+[intensity]}
  previous=[v for v in closed if v['end']<t]
  if len(previous)>=5 and (t-previous[-1]['end']).total_seconds()<4*86400:
   dayvar=previous[-1]['variance'];weekvar=float(np.mean([v['variance'] for v in previous[-5:]]));age=(t-previous[-1]['end']).total_seconds()
   if dayvar>0 and weekvar>0:
    base=[math.log(rv),neg/rv,jump/rv];features['T082']={'raw':[rv,dayvar,weekvar,neg,jump/rv,1],'base':base,'aug':base+[math.log(dayvar),math.log(weekvar)],'age':age}
  for strategy,current in features.items():
   h=histories[strategy];train=[a for a in h if a['available']<t and a['date']<d]
   if len(train)==60:
    xb=[a['base'] for a in train];xa=[a['aug'] for a in train];y=[a['target'] for a in train]
    assert max(a['available'] for a in train)<t
    if strategy=='T032':
     pb=logistic(xb,y,current['base']);pa=logistic(xa,y,current['aug']);vals=current['raw']+[pb,pa]
    elif strategy=='T040':
     pred,res,_,_=ridge(xb,y,current['base']);pred2,res2,_,_=ridge(xa,y,current['aug']);vals=current['raw']+[pred+float(np.quantile(res,.05)),pred2+float(np.quantile(res2,.05))]
    elif strategy=='T056':
     pb=logistic(xb,y,current['base']);pa=logistic(xa,y,current['aug']);vals=current['raw']+[float(np.mean(y)),pb,pa,minutevar,3.5]
    else:
     pred,*_=ridge(xb,y,current['base']);pred2,*_=ridge(xa,y,current['aug']);vals=current['raw']+[math.exp(min(max(pred,-50),10)),math.exp(min(max(pred2,-50),10)),current['age'],current['age'],1]
    result[strategy].append(observations(t,vals));audits.append({'strategy':strategy,'receipt':utc(t),'latest_training_outcome_available':utc(max(a['available'] for a in train)),'rows':60})
   # Current future outcomes are used only to train LATER dates, never current rows.
   horizon=60 if strategy=='T040' else 15 if strategy=='T056' else 30;end=t+timedelta(minutes=horizon)
   future=q.window(t,end);fr=q.returns(t,end,1);initial=q.boundary(t);final=q.boundary(end)
   if future is None or fr is None or initial is None or final is None:continue
   if strategy=='T032':target=float(max(float(future.high.max())/initial-1,1-float(future.low.min())/initial)>=exc)
   elif strategy=='T040':target=final/initial-1
   elif strategy=='T056':target=float(np.max(abs(fr))>=math.sqrt(minutevar)*3.5)
   else:target=math.log(max(float(np.sum(fr*fr)),1e-16))
   h.append({'date':d,'available':end,'target':target,'base':current['base'],'aug':current['aug']})
 write_json(OUT/'observation-training-audit.json',audits)
 for strategy,rows in result.items():
  p=save_rows(strategy.lower()+'-observations',rows)
  register(strategy,strategy.lower()+'-observe',strategy+' native observation study',{'Observations':'\n'.join(rows)},[DATA/'gold_m1.csv',DATA/'invalid_quotes.csv',DATA/'us500_m5.csv',p],['Observation study: zero orders expected; score native outcomes, not account P/L.','Forecast training labels use tick-derived boundary samples; native strict-after quote measurements can differ slightly. No current/future outcome enters its own inputs.'],kind='observation_test')
 print('Native observation input rows:',{k:len(v) for k,v in result.items()},flush=True)

if __name__=='__main__':
 q=Quotes();fomc_presets(q);print('FOMC presets frozen.',flush=True)
 sameclock_presets(q);print('Same-clock presets frozen.',flush=True)
 observation_presets(q);print('Observation presets frozen.',flush=True)
