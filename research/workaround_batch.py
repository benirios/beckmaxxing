"""Frozen input preparation and native execution for the authorized research batch.
No credentials, live orders, parameter sweeps or fabricated market observations.
"""
from pathlib import Path
from datetime import date, datetime, timedelta, timezone
from zoneinfo import ZoneInfo
import hashlib,json,csv,re
ROOT=Path(__file__).resolve().parents[1]
REF=ROOT/'data/reference/workaround-20261111'
OUT=ROOT/'outputs/workaround-batch-20261111'
DATA=ROOT/'data/processed/workaround-20261111-v2'
OUT.mkdir(exist_ok=True)
START='2025-06-23'; END='2026-10-24'
NY=ZoneInfo('America/New_York'); LON=ZoneInfo('Europe/London'); TOK=ZoneInfo('Asia/Tokyo')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def utc(t):return t.astimezone(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
def clock(d,tm,zone):return datetime.combine(d,datetime.strptime(tm,'%H:%M').time(),zone).astimezone(timezone.utc)
def dates():
 d=date.fromisoformat(START)
 while d<date.fromisoformat(END):yield d;d+=timedelta(days=1)
def write_json(p,obj):p.write_text(json.dumps(obj,indent=2,allow_nan=False)+'\n')
def calendars():
 holidays=set('2025-07-04 2025-09-01 2025-11-27 2025-12-25 2026-01-01 2026-01-19 2026-02-16 2026-04-03 2026-05-25 2026-06-19 2026-07-03 2026-09-07'.split())
 early=set('2025-07-03 2025-11-28 2025-12-24'.split())
 nyse=[str(d) for d in dates() if d.weekday()<5 and str(d) not in holidays|early]
 china=set()
 for a,b in [('2025-10-01','2025-10-08'),('2026-01-01','2026-01-03'),('2026-02-15','2026-02-23'),('2026-04-04','2026-04-06'),('2026-05-01','2026-05-05'),('2026-06-19','2026-06-21'),('2026-09-25','2026-09-27'),('2026-10-01','2026-10-07')]:
  d=date.fromisoformat(a)
  while d<=date.fromisoformat(b):china.add(d);d+=timedelta(days=1)
 no_night=set(map(date.fromisoformat,'2025-09-30 2025-12-31 2026-02-13 2026-04-03 2026-04-30 2026-06-18 2026-09-24 2026-09-30'.split()))
 sessions=[d for d in dates() if d.weekday()<5 and d not in china];pairs=[]
 for pred,target in zip(sessions,sessions[1:]):
  if pred not in no_night and (target-pred).days<=3:pairs.append(f'{target}={pred}')
 result={'nyse_full_dates':nyse,'nyse_holidays':sorted(holidays),'nyse_early_closes':sorted(early),'shfe_pairs':pairs,
         'sources':json.loads((REF/'sources.json').read_text()),'shfe_rule':'Weekdays excluding official closures; night is previous business evening, no night on preholiday dates; no pairing across holiday gaps. IANA Asia/Shanghai.'}
 write_json(REF/'calendars.json',result);return result

def register(strategy,name,title,params,inputs,notes=(),kind='strategy_test'):
 mapping=json.loads((ROOT/'ctrader/catalog-coverage.json').read_text());source=mapping[strategy]
 p=ROOT/'configs/backtest-presets.json';registry=json.loads(p.read_text()) if p.exists() else {'presets':{}}
 item={'strategy':strategy,'source':source,'source_sha256':sha(ROOT/'ctrader'/source),'experiment':'WORKAROUND-'+strategy,'title':title,'marker':strategy+'_START','start':START,'end':END,'parameters':params,'inputs':{str(Path(x).relative_to(ROOT)):sha(x) for x in inputs},'review_notes':list(notes),'kind':kind}
 if name in registry['presets'] and registry['presets'][name]!=item:raise ValueError('Refusing to mutate an already frozen preset '+name)
 registry['presets'][name]=item;write_json(p,registry)
 return name

def calendar_presets():
 c=calendars();inputs=[REF/'calendars.json',REF/'nyse_2025_2027.pdf',REF/'nyse.html',REF/'shfe2025.html',REF/'shfe2026.html']
 for strategy,param,data,title in [('T023','NyseDates',','.join(c['nyse_full_dates']),'US midday to close'),('T022','SessionPairs',','.join(c['shfe_pairs']),'Shanghai night transfer')]:
  for variant,branch in enumerate(['follow','opposite','long','random']):
   register(strategy,f'{strategy.lower()}-{branch}',title+' '+branch,{param:data,'Variant':variant,'FixedVolumeInUnits':1},inputs,['Available-history exploratory replication; original multi-year and independent confirmation tests are not completed.'])
 return c
if __name__=='__main__':calendar_presets();print('Calendar presets frozen.')
