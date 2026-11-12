"""Sequential frozen native backtests using the platform JSON agent protocol."""
from backtest_lab.agent import Client,ClientError
from research.workaround_batch import ROOT,OUT,START,END,write_json
from pathlib import Path
import json,time
c=Client();done={p.name.removesuffix('-result.json') for p in OUT.glob('*-result.json')};failed={p.name.removesuffix('-blocked.json') for p in OUT.glob('*-blocked.json')};note=ROOT/'04 - Planning/Workaround backtest batch - 2026-11-11.md'
print(json.dumps({'resuming':True,'already_terminal':len(done),'already_blocked':len(failed)}),flush=True)
while True:
 registry=json.loads((ROOT/'configs/backtest-presets.json').read_text())['presets']
 names=[n for n in registry if n not in done|failed]
 if not names:
  if (OUT/'queue-sealed.json').exists():break
  time.sleep(5);continue
 name=names[0];item=registry[name];strategy=item['strategy']
 pre=c.request('/api/v1/preflight',{'strategy':strategy,'preset':name})
 if not pre['can_launch']:
  if all(x.get('retryable') for x in pre['blockers']):time.sleep(5);continue
  write_json(OUT/(name+'-blocked.json'),pre);failed.add(name);print(json.dumps({'blocked':name,'blockers':pre['blockers']}),flush=True);continue
 request={'strategy':strategy,'preset':name,'start':START,'end':END,'branch':'primary','idempotency_key':'workaround-v1-'+name,'expected_source_sha256':pre['source_sha256']}
 jid=c.request('/api/v1/jobs',request)['id'];print(json.dumps({'launched':name,'id':jid}),flush=True)
 with note.open('a') as f:f.write(f'\n- **Launched {name}**: `{jid}`. Rules and inputs frozen before launch.\n')
 last=0
 while True:
  job=c.request('/api/v1/jobs/'+jid)
  if job['terminal']:break
  if time.monotonic()-last>45:
   progress=job.get('progress') or {}
   print(json.dumps({'running':name,'id':jid,'percent':progress.get('percent'),'phase':progress.get('phase'),
                     'equity_samples':len(progress.get('equity_curve') or []),'metrics':progress.get('metrics')}),flush=True)
   last=time.monotonic()
  time.sleep(5)
 result=c.request('/api/v1/jobs/'+jid+'/result');write_json(OUT/(name+'-result.json'),result)
 with note.open('a') as f:f.write(f"\n- **Terminal {name} — {job['status']}**: [[Lab run {jid} - {strategy}]]. Native summary `{json.dumps(job.get('summary',job.get('failure',{})))}`. Audit pending; terminal does not mean valid/passed.\n")
 print(json.dumps({'finished':name,'id':jid,'status':job['status'],'summary':job.get('summary'),'failure':job.get('failure')}),flush=True);done.add(name)
write_json(OUT/'queue-finished.json',{'terminal_presets':sorted(done),'blocked_presets':sorted(failed)})
print('FROZEN_WORKAROUND_QUEUE_FINISHED',flush=True)
