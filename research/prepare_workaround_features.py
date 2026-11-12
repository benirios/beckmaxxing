"""Derive frozen, past-only inputs from native tick aggregates. No result-led choices."""
from research.workaround_batch import *
import pandas as pd
import numpy as np
from collections import deque,Counter
import math,itertools

M=pd.Timedelta(minutes=1)
def stamp(t):
 t=pd.Timestamp(t)
 return t.tz_localize('UTC') if t.tzinfo is None else t.tz_convert('UTC')
def fmt(x):return format(float(x),'.17g')
def row(t,exit,stop,vals):return '|'.join([utc(t),utc(exit),fmt(stop)]+[fmt(x) for x in vals])
def observations(t,vals):return '|'.join([utc(t)]+[fmt(x) for x in vals])

class Quotes:
 def __init__(self,path=DATA):
  self.m=pd.read_csv(path/'gold_m1.csv',encoding='utf-8-sig')
  for c in ['timestamp','first_utc','last_utc']:self.m[c]=pd.to_datetime(self.m[c],utc=True,format='ISO8601')
  self.m=self.m.set_index('timestamp');assert self.m.index.is_monotonic_increasing and self.m.index.is_unique
  invalid=pd.read_csv(path/'invalid_quotes.csv',encoding='utf-8-sig')
  self.invalid=set(pd.to_datetime(invalid.timestamp,utc=True,format='ISO8601').dt.floor('min'))
  self.bad=self.m.index.isin(self.invalid)|(self.m.complete!=1)|(self.m.ticks<1)
  self.m['valid']=~self.bad
  self.cal=json.loads((REF/'calendars.json').read_text())
  self.nyse=set(self.cal['nyse_full_dates'])
 def window(self,a,b):
  a,b=stamp(a),stamp(b);n=int((b-a)/M)
  if n<1:return None
  w=self.m.loc[a:b-M]
  if len(w)!=n or not w.valid.all() or w.index[0]!=a or w.index[-1]!=b-M:return None
  return w
 def boundary(self,t,strict_age=2):
  t=stamp(t)
  try:r=self.m.loc[t-M]
  except KeyError:return None
  if not r.valid or not 0<=(t-r.last_utc).total_seconds()<=strict_age:return None
  return float(r.close)
 def returns(self,a,b,step=1):
  w=self.window(a,b)
  if w is None:return None
  times=pd.date_range(stamp(a),stamp(b),freq=f'{step}min')
  vals=[self.boundary(t) for t in times]
  if any(x is None for x in vals):return None
  return np.diff(np.log(vals))
 def stop(self,t,minutes=60):
  w=self.window(stamp(t)-minutes*M,t)
  if w is None:return None
  value=float(w.high.max()-w.low.min())
  return value if value>0 else None
 def move(self,a,b):
  if self.window(a,b) is None:return None
  x,y=self.boundary(a),self.boundary(b)
  return None if x is None or y is None else y/x-1
 def spread(self,t):
  t=stamp(t)
  try:r=self.m.loc[t-M]
  except KeyError:return None
  return float(r.ask_close-r.bid_close) if self.boundary(t) else None

def save_rows(name,rows):
 p=OUT/(name+'.txt');p.write_text('\n'.join(rows)+'\n');return p

def feature_presets(q):
 inputs=[DATA/'gold_m1.csv',DATA/'invalid_quotes.csv',REF/'calendars.json']
 notes=['Features derived from native bid/ask tick aggregates; invalid/missing prior windows excluded, no interpolated bars.', 'Available-history exploratory test; no untouched confirmation or live deployment.']
 # Completed same-broker session predictors; signed events are independent of trade outcomes.
 session_signals={};ny_candidates=[]
 for label,zone,tm in [('tokyo',TOK,'09:00'),('london',LON,'08:00'),('ny',NY,'09:30')]:
  events=[]
  for d in dates():
   if d.weekday()>=5 or (label=='ny' and str(d) not in q.nyse):continue
   a=clock(d,tm,zone);t=a+timedelta(minutes=30);ret=q.move(a,t)
   if ret is None or ret==0:continue
   sign=1 if ret>0 else -1;events.append((t,sign))
   if label=='ny':ny_candidates.append((t,sign))
  session_signals[label]=events
  text=','.join(utc(t)+'='+('+' if s>0 else '')+str(s) for t,s in events);p=save_rows('t003-'+label,[text])
  for variant,branch in enumerate(['follow','opposite','random']):register('T003',f't003-{label}-{branch}',f'Session continuation {label} {branch}',{'SignedReceiptsUtc':text,'Variant':variant,'FixedVolumeInUnits':1},inputs+[p],notes)
 # Macro continuation with actual, verified archive release clocks.
 releases=json.loads((REF/'bls_releases.json').read_text())['records'];hist={'cpi':deque(maxlen=6),'empsit':deque(maxlen=6)};signals=[];audit=[]
 for ev in sorted(releases,key=lambda r:(r['release_date'],r['kind'])):
  if '8:30' not in ev['header']:raise ValueError('Unverified BLS release time')
  a=clock(date.fromisoformat(ev['release_date']),'08:30',NY);t=a+timedelta(minutes=5);ret=q.move(a,t);history=hist[ev['kind']]
  if ret is None:audit.append({**ev,'state':'missing prior tick window'});continue
  threshold=sorted(history)[math.ceil(.75*len(history))-1] if len(history)==6 else None
  if threshold is not None and abs(ret)>threshold and ret!=0:signals.append(utc(t)+'='+('+1' if ret>0 else '-1'))
  audit.append({**ev,'move':ret,'threshold':threshold,'warmup':len(history)});history.append(abs(ret))
 text=','.join(signals);p=save_rows('t001-receipts',[text]);write_json(OUT/'t001-eligibility.json',audit)
 if signals:
  for variant,branch in enumerate(['follow','opposite','random']):register('T001','t001-'+branch,'Post-release continuation '+branch,{'SignedReceiptsUtc':text,'Variant':variant,'FixedVolumeInUnits':1},inputs+[p,REF/'bls_releases.json'],notes+['Official publication clock is a documented availability proxy, not measured receipt latency.'])
 # One slow SMA setting, chosen from economic horizon, never a search.
 rows=[]
 for d in dates():
  if str(d) not in q.nyse:continue
  t=clock(d,'10:00',NY);stop=q.stop(t)
  if stop:rows.append(row(t,t+timedelta(hours=1),stop,[48,288]))
 p=save_rows('t037-features',rows)
 for variant,branch in enumerate(['primary','opposite','random']):register('T037','t037-'+branch,'Slow SMA 4h/24h '+branch,{'FeatureRows':'\n'.join(rows),'Variant':variant,'RiskBudget':50,'MaxTradesPerDay':1},inputs+[p,REF/'ma_full.pdf'],notes+['Canonical SMA uses native broker BID M5 closes; stop-range features use midpoint.'])
 # Matched stop and exit comparisons, identical original NY continuation opportunities.
 h_rv=deque(maxlen=60);h_neg=deque(maxlen=60);risk={'T007':[],'T018':[],'T039':[]}
 for t,sign in ny_candidates:
  r=q.returns(t-timedelta(minutes=30),t,5);mid=q.boundary(t);stop=q.stop(t,30)
  if r is None or mid is None or not stop:continue
  rv=float(np.sum(r*r));neg=float(np.sum(r[r<0]**2));pos=float(np.sum(r[r>=0]**2));end=t+timedelta(minutes=30);original=mid-sign*stop
  if len(h_rv)==60 and rv>0 and neg>0 and np.mean(h_neg)>0:
   scaled7=stop*math.sqrt(np.mean(h_rv)/rv);scaled18=stop*math.sqrt(neg/np.mean(h_neg))
   common=[utc(t),utc(end),str(sign),fmt(original),'0','1']
   risk['T007'].append('|'.join(common+[fmt(mid-sign*scaled7),fmt(np.mean(h_rv))]))
   risk['T018'].append('|'.join(common+[fmt(mid-sign*scaled18),fmt(neg),fmt(pos)]))
   risk['T039'].append('|'.join(common+['1']))
  h_rv.append(rv);h_neg.append(neg)
 for strategy,rows in risk.items():
  p=save_rows(strategy.lower()+'-opportunities',rows)
  for variant,branch in enumerate(['baseline','gated']):register(strategy,strategy.lower()+'-'+branch,strategy+' matched '+branch,{'Opportunities':'\n'.join(rows),'Variant':variant,'DollarRisk':50},inputs+[p],notes)
 # Tick-weighted session VWAP and explicitly frozen placebo anchor; no exchange volume claim.
 rows=[]
 for d in dates():
  if d.weekday()>=5:continue
  start=clock(d,'08:00',LON);anchor=clock(d,['07:00','07:15','07:30','07:45'][int(hashlib.sha256(str(d).encode()).hexdigest()[:8],16)%4],LON)
  for k in range(12,85):
   t=start+timedelta(minutes=5*k);w=q.window(start,t);wp=q.window(anchor,t);stop=q.stop(t);last=q.boundary(t)
   if w is None or wp is None or stop is None or last is None:continue
   def vwap(x):
    n=float(x.ticks.sum());v=float(x.sum_mid.sum())/n;s=math.sqrt(max(0,float(x.sum_mid_squared.sum())/n-v*v));return v,s
   v,s=vwap(w);vp,sp=vwap(wp)
   if min(s,sp)<=0:continue
   rows.append(row(t,t+timedelta(hours=1),stop,[last,v,s,1,vp,sp]))
 p=save_rows('t080-features',rows)
 for variant,branch in enumerate(['primary','opposite','random','placebo']):register('T080','t080-'+branch,'Tick-proxy VWAP reversion '+branch,{'FeatureRows':'\n'.join(rows),'Variant':variant,'RiskBudget':50,'AcknowledgeTickVolumeProxy':True},inputs+[p],notes+['Quote-tick-weighted VWAP proxy, not actual exchange volume; branch eligibility may differ for placebo.'])
 return session_signals

# Standardized ridge estimators fit only on earlier completed local dates.
def ridge(x,y,new):
 x=np.asarray(x,float);y=np.asarray(y,float);new=np.asarray(new,float)
 mu=x.mean(axis=0);sd=x.std(axis=0);sd[sd<1e-12]=1
 a=np.column_stack([np.ones(len(x)),(x-mu)/sd]);b=np.r_[1,(new-mu)/sd];pen=np.eye(a.shape[1])*1e-6;pen[0,0]=0
 beta=np.linalg.solve(a.T@a+pen,a.T@y)
 return float(b@beta),y-a@beta,beta,sd

def logistic(x,y,new):
 x=np.asarray(x,float);y=np.asarray(y,float);new=np.asarray(new,float)
 mu=x.mean(axis=0);sd=x.std(axis=0);sd[sd<1e-12]=1;a=np.column_stack([np.ones(len(x)),(x-mu)/sd]);b=np.r_[1,(new-mu)/sd];beta=np.zeros(a.shape[1]);pen=np.eye(a.shape[1])*1e-3;pen[0,0]=0
 for _ in range(20):
  z=np.clip(a@beta,-25,25);p=1/(1+np.exp(-z));w=np.maximum(p*(1-p),1e-6)
  delta=np.linalg.solve(a.T@(a*w[:,None])+pen,a.T@(p-y)+pen@beta);beta-=delta
  if np.linalg.norm(delta)<1e-7:break
 return float(1/(1+np.exp(-np.clip(b@beta,-25,25))))

def entropy(values):
 patterns=Counter(tuple(np.argsort(values[i:i+3],kind='stable')) for i in range(len(values)-2));n=sum(patterns.values())
 return -sum((v/n)*math.log(v/n) for v in patterns.values())/math.log(6)

def entropy_presets(q):
 session_sequences=deque(maxlen=20);entropies=deque(maxlen=60);daily_state={};source_rows=[];shuffled=[];predictor_history=[]
 for d in dates():
  if d.weekday()>=5:continue
  before=clock(d,'08:00',LON)
  ent=entropy(np.concatenate(session_sequences)) if len(session_sequences)==20 else None
  cutoff=float(np.median(entropies)) if len(entropies)==60 else None
  if ent is not None and cutoff is not None and str(d) in q.nyse:
   t=clock(d,'10:00',NY);r=q.returns(t-timedelta(minutes=60),t,5);stop=q.stop(t);spread=q.spread(t)
   if r is not None and stop and spread is not None:
    last=math.expm1(r[-1]);var=float(np.sum(r*r));jumpfreq=float(np.mean(abs(r)>3.5*math.sqrt(max(var/len(r),1e-16))))
    vals=[last,ent,cutoff,var,spread,jumpfreq];source_rows.append(row(t,t+timedelta(minutes=5),stop,vals))
    if predictor_history:
     seed=int(hashlib.sha256(str(d).encode()).hexdigest()[:8],16);past=predictor_history[-60:];vals[0]=past[seed%len(past)]
     shuffled.append(row(t,t+timedelta(minutes=5),stop,vals))
    predictor_history.append(last)
  if ent is not None:entropies.append(ent)
  seq=q.returns(before,clock(d,'16:00',LON),5)
  if seq is not None:session_sequences.append(seq)
 p=save_rows('t041-features',source_rows);ps=save_rows('t041-shuffled',shuffled)
 for variant,branch in enumerate(['primary','opposite','random']):register('T041','t041-'+branch,'Entropy one-lag '+branch,{'FeatureRows':'\n'.join(source_rows),'Variant':variant,'RiskBudget':50},[DATA/'gold_m1.csv',DATA/'invalid_quotes.csv',p],['Pre-open entropy computed only from prior sessions; no state chosen after results.'])
 register('T041','t041-shuffled','Entropy past-predictor shuffle',{'FeatureRows':'\n'.join(shuffled),'Variant':0,'RiskBudget':50},[DATA/'gold_m1.csv',DATA/'invalid_quotes.csv',ps],['Derived past-data predictor placebo, not a new observed signal or independent market sample.'])

if __name__=='__main__':
 q=Quotes();print('Valid minute aggregates',len(q.m),'invalid minutes',len(q.invalid),flush=True)
 feature_presets(q);print('Session, macro, SMA, risk and VWAP inputs frozen.',flush=True)
 entropy_presets(q);print('Entropy inputs frozen.',flush=True)
