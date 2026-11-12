'use strict';
const $ = id => document.getElementById(id);
let state, selected = 'T002', filter = 'ready', search = '', requesting = false, refreshing = false;
let catalogSignature = '', reportSignature = '', coverageSignature = '';
const drafts = {};
const equitySamples = new Map();
const names = {pending:'NOT RUN',running:'RUNNING',failed_attempt:'FAILED ATTEMPT',diagnostic:'DIAGNOSTIC / NO TRADES',result_recorded:'RESULT · REVIEW',prior_result:'PRIOR RESULT',needs_inputs:'NEEDS INPUTS'};
function el(tag,text,cls){const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;}
function num(v,d=2){return typeof v==='number'&&Number.isFinite(v)?v.toLocaleString('en-US',{minimumFractionDigits:d,maximumFractionDigits:d}):'—';}
function toast(s){$('toast').textContent=s;$('toast').hidden=false;clearTimeout(toast.timer);toast.timer=setTimeout(()=>$('toast').hidden=true,6000);}
async function api(path,data){
  const request=()=>fetch(path,{method:data===undefined?'GET':'POST',headers:{'X-Lab-Token':window.LAB_TOKEN,...(data===undefined?{}:{'Content-Type':'application/json'})},...(data===undefined?{}:{body:JSON.stringify(data)})});
  let r=await request();
  if(r.status===403){const text=await(await fetch('/session.js',{cache:'no-store'})).text();const match=text.match(/^window\.LAB_TOKEN=("[^"]+");$/);if(match){window.LAB_TOKEN=JSON.parse(match[1]);r=await request();}}
  const body=await r.json();if(!r.ok)throw Error(body.error||'Request failed');return body;
}
async function download(path){try{const r=await fetch('/api/artifact?path='+encodeURIComponent(path),{headers:{'X-Lab-Token':window.LAB_TOKEN}});if(!r.ok)throw Error('Artifact unavailable');const u=URL.createObjectURL(await r.blob());const a=el('a');a.href=u;a.download=path.split('/').pop();a.click();setTimeout(()=>URL.revokeObjectURL(u),1000);}catch(e){toast(e.message);}}
function allRows(){return [...state.catalog,...(state.legacy_sources||[])];}
function openConnection(){const f=$('connection-form'),p=state?.profile||{};for(const k of ['ctid','account'])f.elements[k].value=p[k]||'';f.elements.broker.value=p.broker||'FTMO Platform';$('verify-saved').hidden=!state?.saved_login;$('connection-message').textContent=state?.connection.message||'';$('connection-dialog').showModal();}
function renderCatalog(){
  let rows=filter==='legacy'?(state.legacy_sources||[]):filter==='recorded'?allRows().filter(r=>r.execution?.attempt_count||r.execution?.prior_experiment):state.catalog.filter(r=>filter==='all'||r.status===filter);
  rows=rows.filter(r=>`${r.id} ${r.name} ${r.source}`.toLowerCase().includes(search.toLowerCase()));
  const list=$('strategy-list');list.replaceChildren();
  if(!rows.length){list.append(el('p','No sources match this view.','empty'));$('strategy-detail').replaceChildren();return;}
  if(!rows.some(r=>r.id===selected))selected=rows[0].id;
  for(const r of rows){const c=el('button',undefined,`strategy-card${r.id===selected?' selected':''}`);c.type='button';c.setAttribute('aria-pressed',String(r.id===selected));const top=el('div',undefined,'card-top');top.append(el('span',r.id.startsWith('legacy:')?'LEGACY':r.id,'strategy-id'),el('span',names[r.execution?.status]||'NOT RUN',`badge ${r.status}`));c.append(top,el('h3',r.name),el('span',r.source,'source-name'));if(r.execution?.latest_result)c.append(el('p',`Latest: $${num(r.execution.latest_result.net)} · ${num(r.execution.latest_result.trades,0)} trades`,'card-result'));c.onclick=()=>{selected=r.id;renderCatalog();renderDetail();};list.append(c);}
}
function resultLine(s){return `Net $${num(s.net)} · PF ${num(s.profit_factor)} · ${num(s.trades,0)} trades · ${s.data_mode||'unverified data'}`;}
function renderDetail(){
  const r=allRows().find(r=>r.id===selected),p=$('strategy-detail');p.replaceChildren();if(!r)return;
  p.append(el('div',`${r.id} / ${r.experiment||'RESEARCH CONTRACT'}`,'detail-kicker'),el('h2',r.name),el('p',r.contract,'contract'));
  const x=r.execution||{};
  if(x.latest_result)p.append(el('p',resultLine(x.latest_result),'result-observation'));
  if(x.prior_experiment)p.append(el('p',`${x.prior_experiment.id}: ${x.prior_experiment.result}`,'result-observation'));
  if(r.status!=='ready'){
    const box=el('div',undefined,'blocked-copy');box.append(el('p',x.prior_experiment?'Prior experiment linked. Inspect its original evidence and data mode; this does not establish that the current source hash was tested.':'This source needs audited inputs before a meaningful market test. Empty-input starts do not count as results.'));
    for(const k of r.required_inputs)box.append(el('span',k,'input-chip'));p.append(box);return;
  }
  for(const [k,v]of [['INSTRUMENT','FTMO / XAUUSD'],['REPLAY','Historical server ticks'],['EXPOSURE','1 unit · fixed'],['REQUESTED COSTS','1 pip + $30 / million']]){const line=el('div',undefined,'rule-line');line.append(el('span',k),el('b',v));p.append(line);}
  const form=el('form',undefined,'run-form'), dates=el('div',undefined,'date-fields');
  const saved=drafts[r.id]||{start:'2020-05-06',end:'2026-10-24'};
  for(const [key,title]of [['start','START DATE'],['end','END DATE']]){const label=el('label',title),input=el('input');input.type='date';input.name=key;input.required=true;input.value=saved[key];input.oninput=()=>{drafts[r.id]={start:form.elements.start.value,end:form.elements.end.value};};label.append(input);dates.append(label);}
  const button=el('button',undefined,'primary');button.type='submit';button.disabled=!!state.current_job||requesting;button.append(el('span',state.connection.status==='connected'?'Run tick backtest':'Connect to run backtest'),el('span','↗'));form.append(dates,button);
  if(r.id==='T015'){const b=el('button','Run non-round placebo','text-button');b.type='button';b.disabled=!!state.current_job||requesting;b.onclick=()=>launch(form,r,'placebo');form.append(b);}
  form.onsubmit=e=>{e.preventDefault();launch(form,r,'primary');};p.append(form,el('p','Results are recorded after every attempt. Dates shown are the original exposed development window; data availability and actual costs require verification.','run-caption'));
}
async function launch(form,r,branch){if(state.connection.status!=='connected'){openConnection();return;}requesting=true;try{await api('/api/backtest',{strategy:r.id,branch,start:form.elements.start.value,end:form.elements.end.value,idempotency_key:'ui-'+crypto.randomUUID(),expected_source_sha256:r.sha256});setView('runs');await refresh();toast('Backtest started. The current-run panel follows native observations.');}catch(e){toast(e.message);}finally{requesting=false;renderDetail();}}
function renderCurrent(){
  const p=$('current-run');p.replaceChildren();const j=state.current_job;
  if(!j){const last=state.jobs[0];p.append(el('div','RUN CONTROL','detail-kicker'),el('h2','No backtest is running.'));if(last)p.append(el('p',`Last attempt: ${last.strategy} · ${last.title} · ${last.status.replaceAll('_',' ')}${last.summary?' · '+resultLine(last.summary):''}`));return;}
  const head=el('div',undefined,'current-head');head.append(el('div',`${j.strategy} / ${j.kind==='data_probe'?'DATA DIAGNOSTIC':j.branch.toUpperCase()}`,'detail-kicker'),el('span','RUNNING','badge'));p.append(head,el('h2',j.title));
  const progress=j.progress||{},percent=progress.percent;
  p.append(el('p',`${progress.phase||'Starting'}${percent===null||percent===undefined?'':` · native progress ${num(percent,1)}%`} · ${j.start} → ${j.end}`));
  const meter=el('progress');meter.max=100;if(percent!==null&&percent!==undefined)meter.value=percent;meter.setAttribute('aria-label','Native backtest progress');p.append(meter);
  const metrics=progress.metrics;
  const samples=equitySamples.get(j.id)||[];
  for(const point of progress.equity_curve||[])if(!samples.some(s=>s.utc===point.utc))samples.push({...point});
  for(const match of (j.log||'').matchAll(/LAB_LIVE_V1,utc=([^,\r\n]+),balance=([^,]+),equity=([^,]+),net_profit=([^,]+),closed_positions=(\d+)/g)){
    const point={utc:match[1],balance:Number(match[2]),equity:Number(match[3]),net_profit:Number(match[4]),closed_positions:Number(match[5])};
    if(Object.values(point).every((v,i)=>i===0||Number.isFinite(v))&&!samples.some(s=>s.utc===point.utc))samples.push(point);
  }
  if(metrics){
    const last=samples.at(-1);
    if(!last||last.utc!==metrics.utc){samples.push({...metrics});equitySamples.set(j.id,samples);}
  }
  if(samples.length){
    samples.sort((a,b)=>Date.parse(a.utc)-Date.parse(b.utc));equitySamples.set(j.id,samples);
    const current=metrics||samples.at(-1);
    const grid=el('div',undefined,'live-metrics');for(const [label,value]of [['ACCOUNT EQUITY','$'+num(current.equity)],['BALANCE','$'+num(current.balance)],['REALIZED NET','$'+num(current.net_profit)],['CLOSED POSITIONS',num(current.closed_positions,0)]]){const n=el('div');n.append(el('span',label),el('strong',value));grid.append(n);}p.append(grid);
    const chart=el('div',undefined,'equity-chart');chart.append(el('div','OBSERVED ACCOUNT EQUITY','equity-chart-label'));
    const canvas=el('canvas');canvas.setAttribute('role','img');canvas.setAttribute('aria-label',`Observed account equity over the running backtest, ${samples.length} native snapshots`);chart.append(canvas);p.append(chart,el('p',`${samples.length} native snapshots · Last: ${current.utc} UTC. Equity includes floating P/L at each observed snapshot; unsampled intervals are not interpolated.`,'run-caption'));
    drawEquity(canvas,samples);
  } else p.append(el('p','Waiting for the first native account snapshot. This run’s package may not include live observation. No interim P/L is estimated.','run-caption'));
}
function drawEquity(canvas,samples){
  const width=Math.max(280,canvas.clientWidth),height=190,dpr=window.devicePixelRatio||1;
  canvas.width=width*dpr;canvas.height=height*dpr;canvas.style.height=height+'px';
  const c=canvas.getContext('2d');c.scale(dpr,dpr);c.clearRect(0,0,width,height);
  const vals=samples.map(s=>s.equity).filter(Number.isFinite);if(!vals.length)return;
  const lo=Math.min(...vals),hi=Math.max(...vals),pad=14,span=hi-lo||1;
  c.strokeStyle='#2c343d';c.lineWidth=1;c.beginPath();c.moveTo(pad,height-pad);c.lineTo(width-pad,height-pad);c.stroke();
  c.strokeStyle='#d9b76e';c.lineWidth=2;c.beginPath();
  samples.forEach((s,i)=>{const x=pad+(width-2*pad)*(samples.length<2?1:i/(samples.length-1));const y=height-pad-(s.equity-lo)/span*(height-2*pad);if(i)c.lineTo(x,y);else c.moveTo(x,y);});c.stroke();
  c.fillStyle='#d9b76e';samples.forEach((s,i)=>{const x=pad+(width-2*pad)*(samples.length<2?1:i/(samples.length-1));const y=height-pad-(s.equity-lo)/span*(height-2*pad);c.beginPath();c.arc(x,y,2.5,0,Math.PI*2);c.fill();});
  c.fillStyle='#97a3b2';c.font='10px monospace';c.fillText('$'+num(hi),pad,11);c.fillText('$'+num(lo),pad,height-2);
}
// Native artifacts are read in the browser; no strategy or server state is changed.
let selectedReport = '', analysisKey = '', analysisRequest = 0;
const reportCache = new Map();
const finite = value => typeof value === 'number' && Number.isFinite(value);
const nativeMetric = value => value && typeof value === 'object' ? value.all : value;
// Recovery ratio: observed return divided by maximum equity drawdown.
// Keep diagnostics, invalid attempts and missing results outside the ranking.
function curveScore(run){
  if(run.kind==='data_probe'||['invalid_attempt','failed','interrupted'].includes(run.status)||
    !finite(run.trades)||run.trades<=0||!finite(run.roi)||!finite(run.drawdown)||run.drawdown<0)return null;
  if(run.drawdown===0)return run.roi>0?Infinity:run.roi<0?-Infinity:0;
  return run.roi/run.drawdown;
}
function rankedRuns(){
  return state.runs.slice().sort((a,b)=>{
    const left=curveScore(a),right=curveScore(b);
    if(left===null||right===null)return left===right?a.id.localeCompare(b.id):left===null?1:-1;
    if(left!==right)return left>right?-1:1;
    return b.roi-a.roi||a.drawdown-b.drawdown||b.trades-a.trades||a.id.localeCompare(b.id);
  });
}
function curveScoreLabel(run){const score=curveScore(run);return score===null?'Unranked':score===Infinity?'∞':score===-Infinity?'−∞':num(score)+'×';}

function reportMetrics(report){
  const main=report.main||{}, stats=report.tradeStatistics||{}, equity=report.equity||{};
  const get=key=>nativeMetric(stats[key]);
  const total=get('totalTrades'), wins=get('winningTrades'), losses=get('losingTrades');
  const period=main.testingPeriod||{}, start=period.startDate, end=period.endDate;
  // Elapsed calendar days, including weekends, over the native test window.
  const days=finite(start)&&finite(end)&&end>start?(end-start)/86400000:null;
  return {main,equity,total,wins,losses,get,days,winRate:finite(wins)&&finite(total)&&total>0?wins/total*100:null,
    tradesPerDay:finite(total)&&days?total/days:null};
}
function metricCard(label,value,caption,tone=''){
  const card=el('div',undefined,'metric-card '+tone);
  card.append(el('span',label,'metric-label'),el('strong',value),el('span',caption,'metric-caption'));
  return card;
}
function money(value,currency='USD'){return finite(value)?`${num(value)} ${currency}`:'—';}
function percent(value){return finite(value)?num(value)+'%':'—';}
function dateLabel(value){return finite(value)?new Date(value).toISOString().slice(0,10):'Unknown';}
function svgElement(tag,attrs={}){const node=document.createElementNS('http://www.w3.org/2000/svg',tag);for(const [key,value]of Object.entries(attrs))node.setAttribute(key,String(value));return node;}
function nativeEquityChart(report){
  const panel=el('div',undefined,'performance-chart');
  const top=el('div',undefined,'chart-heading');top.append(el('h3','Balance & equity'),el('span','NATIVE REPORT SAMPLES','detail-kicker'));panel.append(top);
  const points=(report.equity?.points||[]).filter(p=>finite(p.timestamp)&&finite(p.balance)).slice().sort((a,b)=>a.timestamp-b.timestamp);
  if(!points.length){panel.append(el('p','This report does not include an equity series.','empty'));return panel;}
  const legend=el('div',undefined,'chart-legend');legend.append(el('span','● Balance','balance-key'),el('span','━ Equity min / max','range-key'));panel.append(legend);
  const w=1000,h=300,left=85,right=22,topPad=22,bottom=40;
  let lo=Infinity,hi=-Infinity;
  for(const p of points)for(const v of [p.balance,p.minEquity,p.maxEquity])if(finite(v)){lo=Math.min(lo,v);hi=Math.max(hi,v);}
  const margin=(hi-lo)*.1||1;lo-=margin;hi+=margin;
  const first=points[0].timestamp,last=points.at(-1).timestamp;
  const x=p=>left+(p.timestamp-first)/(last-first||1)*(w-left-right),y=v=>topPad+(hi-v)/(hi-lo)*(h-topPad-bottom);
  const svg=svgElement('svg',{viewBox:`0 0 ${w} ${h}`,role:'img','aria-label':`Native balance curve with sampled equity range, ${points.length} points from ${dateLabel(first)} to ${dateLabel(last)}`});
  const title=svgElement('title');title.textContent='Balance and sampled minimum / maximum equity. The shaded range is not a continuous equity path.';svg.append(title);
  for(let i=0;i<5;i++){const value=lo+(hi-lo)*i/4,yp=y(value);svg.append(svgElement('line',{x1:left,y1:yp,x2:w-right,y2:yp,class:'chart-grid'}));const text=svgElement('text',{x:left-12,y:yp+4,'text-anchor':'end',class:'chart-axis'});text.textContent=num(value,0);svg.append(text);}
  const range=points.filter(p=>finite(p.minEquity)&&finite(p.maxEquity));
  if(range.length){const path=range.map((p,i)=>`${i?'L':'M'}${x(p)},${y(p.maxEquity)}`).join(' ')+range.slice().reverse().map(p=>`L${x(p)},${y(p.minEquity)}`).join(' ')+'Z';svg.append(svgElement('path',{d:path,class:'equity-range'}));}
  const path=points.map((p,i)=>`${i?'L':'M'}${x(p)},${y(p.balance)}`).join(' ');svg.append(svgElement('path',{d:path,class:'balance-line','vector-effect':'non-scaling-stroke'}));
  for(const [stamp,anchor]of [[first,'start'],[last,'end']]){const text=svgElement('text',{x:x({timestamp:stamp}),y:h-10,'text-anchor':anchor,class:'chart-axis'});text.textContent=dateLabel(stamp);svg.append(text);}
  panel.append(svg,el('p',`${points.length.toLocaleString()} native samples · ${report.main?.depositAsset||'deposit currency'}. The shaded area shows sampled equity minima and maxima; it does not reconstruct intratrade movements.`,'run-caption'));
  return panel;
}
const strategyBriefs = {
  XauUsdTokyoPressureReversal:'Fades unusually large gold moves during the first 30 minutes of the Tokyo session, looking for a reversal of the opening price pressure.',
  XauUsdEquityDrawdownFlightToGold:'Tests whether gold rises after an extreme negative five-minute move in the broker’s S&P 500 index CFD.',
  XauUsdClockVolatilityStopRisk:'Compares a fixed stop with a stop scaled to historical volatility at the same time of day. Both versions use the same frozen entries, targets and exits, with equal initial dollar risk.',
  XauUsdRoundNumberRejection:'Fades the first crossing of an untouched $100 gold price level during the New York morning. Uses the preceding 15-minute range for its stop and exits after 15 minutes; the placebo shifts levels by $37.50.',
  XauUsdExtremeMoveReversal:'Tests reversal after an unusually large New York opening move: it measures the 09:30–10:00 move and looks for the opposite direction into 10:30 New York time.',
  XauUsdGcCloseMomentum:'Tests whether earlier gold price momentum predicts the direction of the final half-hour of the COMEX gold session, using XAUUSD as the traded instrument.',
  XauUsdGcDaytimeShort:'Tests the negative COMEX daytime-return hypothesis by shorting gold during the declared daytime window. A long-side control tests the opposite direction.',
  XauUsdGcDaytimeShortOnly:'Shorts gold during the declared COMEX daytime window to test the negative daytime-return hypothesis. This version fixes the direction to short.',
  XauUsdGldR5CloseMomentum:'Uses the return in the fifth half-hour of the NYSE session to choose the direction for gold in the final NYSE half-hour.',
  XauUsdLondonRsiOrb:'Trades breakouts of the first 30-minute London gold range, with Wilder RSI(14) as an additional entry filter.',
  XauUsdNy15ImmediateStopOrb:'Places stop entries at the boundaries of the first 15-minute New York gold range, testing immediate breakout execution.',
  XauUsdNy15RetestOrb:'Waits for a breakout of the first 15-minute New York gold range, then enters on the first retest of the breakout boundary.',
  XauUsdNy15OrbBaseline:'Trades breakouts of the first 15-minute New York gold range as an opening-range baseline.',
  XauUsdNy30OrbBaseline:'Trades breakouts of the first 30-minute New York gold range as an opening-range benchmark.',
  XauUsdNy30WidthFilteredOrb:'Trades the New York 30-minute opening-range breakout only when the range width passes the strategy’s historical-width filter.',
  XauUsdNy5OrbBenchmark:'Trades breakouts of the first five-minute New York gold range as a shorter opening-range benchmark.',
  XauUsdClockVolatilityStopRiskDiagnostics:'Measures the next 30 minutes of adverse price movement and compares fixed-stop and same-clock volatility-scaled stop touches. This is a risk diagnostic rather than a standalone entry strategy.',
  XauUsdSemivarianceStopRiskDiagnostics:'Measures whether recent downside versus upside variance changes the risk of an adverse move and stop touch over the next 30 minutes. This is a diagnostic rather than a standalone trading signal.'
};
function strategySynopsis(run,report){
  const job=state.jobs.find(j=>j.directory+'/report.html'===run.report);
  const rows=allRows();
  let strategy=rows.find(r=>r.id===(job?.strategy||run.strategy));
  if(!strategy){
    // Archived reports often use a generic cBot name. Match their explicit experiment instead.
    const archiveSources=[
      [/^EXP-002-/, 'XauUsdNy15OrbBaseline'],[/^EXP-003-/, 'XauUsdNy30OrbBaseline'],[/^EXP-004-/, 'XauUsdNy5OrbBenchmark'],
      [/^EXP-005-/, 'XauUsdNy30WidthFilteredOrb'],[/^EXP-006-/, 'XauUsdNy15RetestOrb'],
      [/^EXP-007-/, 'XauUsdNy15ImmediateStopOrb'],[/^EXP-008-/, 'XauUsdGcCloseMomentum'],
      [/^EXP-009-exp008-/, 'XauUsdGcCloseMomentum'],[/^EXP-011-/, 'XauUsdGcDaytimeShort'],
      [/^EXP-012-/, 'XauUsdGldR5CloseMomentum'],[/^EXP-013-/, 'XauUsdExtremeMoveReversal'],
      [/^EXP-014-/, 'XauUsdLondonRsiOrb'],[/^EXP-017-/, 'XauUsdTokyoPressureReversal']
    ];
    const source=archiveSources.find(([pattern])=>pattern.test(run.id))?.[1];
    strategy=source?rows.find(r=>r.source===source+'.cs'):rows.find(r=>r.source===report.main?.cBotName+'.cs');
    if(source&&!strategy)strategy={source:source+'.cs'};
  }
  let text;
  if(/^EXP-009-always-long-/.test(run.id))text='A control that always buys gold on the experiment’s scheduled opportunities, regardless of the momentum signal. It tests whether the signal adds value beyond a constant long direction.';
  else if(/^EXP-009-always-short-/.test(run.id))text='A control that always sells gold on the experiment’s scheduled opportunities, regardless of the momentum signal. It tests the constant short direction.';
  else if(/^EXP-009-deterministic-random-/.test(run.id))text='A control that chooses long or short with a deterministic random rule on the experiment’s scheduled opportunities, to compare the signal with direction chosen without momentum.';
  else if(/^EXP-011-long-control-/.test(run.id))text='A long-side control for the gold daytime-short experiment. It buys during the same declared daytime window to compare the opposite direction.';
  else {
    const source=strategy?.source?.replace(/\.cs$/,'');
    text=strategyBriefs[source];
    if(!text&&strategy?.contract)text=strategy.contract.replace(/^T\d+\s*(?:V\d+)?\s*:\s*/,'').split(/(?<=[.!?])\s+(?=[A-Z])/).slice(0,2).join(' ');
  }
  return {text:text||'No strategy description is linked to this historical report. Inspect the native report and original experiment for its entry and exit rules.',strategy,job};
}
function renderStrategySynopsis(run,report){
  const {text,strategy,job}=strategySynopsis(run,report),section=el('section',undefined,'strategy-synopsis');
  section.setAttribute('aria-label','How the strategy works');
  section.append(el('div','HOW THE STRATEGY WORKS','detail-kicker'),el('p',text));
  const notes=[];
  if(strategy?.id)notes.push(strategy.id.startsWith('legacy:')?'Legacy strategy':strategy.id);
  if(job?.branch)notes.push('Run variant: '+job.branch);
  if(run.kind==='data_probe'||job?.kind==='data_probe')notes.push('This run is a data-availability diagnostic.');
  if(run.status==='invalid_attempt')notes.push('This attempt is marked invalid; the summary describes the intended strategy.');
  if(strategy?.contract)notes.push('Catalog summary; run artifacts retain the exact tested settings.');
  if(notes.length)section.append(el('div',notes.join(' · '),'run-caption'));
  return section;
}

function renderReportAnalysis(run,report){
  const panel=$('run-analysis');panel.replaceChildren();
  const {main,equity,total,wins,losses,get,days,winRate,tradesPerDay}=reportMetrics(report),currency=main.depositAsset||'USD';
  const heading=el('div',undefined,'selected-run-heading');
  const identity=el('div');identity.append(el('h3',main.cBotName||run.id),el('p',`${run.id} · ${main.symbol||'Unknown symbol'} · ${main.testingPeriod?.formatted||run.period||'Period unavailable'}`));
  heading.append(identity,el('span',(run.kind==='data_probe'?'DATA DIAGNOSTIC':run.status||'ARCHIVED OBSERVATION').replaceAll('_',' ').toUpperCase(),'badge'));panel.append(heading);
  panel.append(renderStrategySynopsis(run,report));
  const evidence=el('div',undefined,'evidence-strip');evidence.append(el('span',`CURVE SCORE  ${curveScoreLabel(run)}`),el('span',`DATA  ${main.data?.type||'Unverified'}`),el('span',`CAPITAL  ${money(main.startingCapital,currency)}`),el('span',`PERIOD  ${days?num(days,1)+' calendar days':'Unavailable'}`));panel.append(evidence);
  panel.append(el('p','Native observations · Manual review required. Diagnostics, failed attempts and invalid experiments do not establish a strategy edge.','analysis-notice'));
  const grid=el('div',undefined,'performance-metrics');
  const net=main.netProfit, pf=get('profitFactor');
  grid.append(
    metricCard('NET PROFIT',money(net,currency),`Return ${percent(main.roi)}`,finite(net)?net<0?'negative':'positive':''),
    metricCard('PROFIT FACTOR',num(pf),'Gross profit / gross loss',finite(pf)?pf<1?'negative':'positive':''),
    metricCard('MAX EQUITY DRAWDOWN',percent(equity.maxEquityDrawdownPercent),money(equity.maxEquityDrawdownAbsolute,currency),'negative'),
    metricCard('WIN RATE',percent(winRate),'Winning trades / total closed trades'),
    metricCard('TOTAL TRADES',num(total,0),'Closed trades in native report'),
    metricCard('WINNING TRADES',num(wins,0),'Native winning trade count','positive'),
    metricCard('LOSING TRADES',num(losses,0),'Native losing trade count','negative'),
    metricCard('AVG. TRADES / DAY',num(tradesPerDay),'Per elapsed calendar day, incl. weekends')
  );panel.append(grid,nativeEquityChart(report));
  const lower=el('div',undefined,'analysis-lower');
  const stats=el('section',undefined,'secondary-metrics');stats.append(el('h3','Trade quality & costs'));
  for(const [label,value]of [
    ['Average trade / expectancy',money(get('averageTrade'),currency)],
    ['Largest winning trade',money(get('largestWinningTrade'),currency)],
    ['Largest losing trade',money(get('largestLosingTrade'),currency)],
    ['Longest winning streak',num(get('maxConsecutiveWinningTrades'),0)],
    ['Longest losing streak',num(get('maxConsecutiveLosingTrades'),0)],
    ['Commissions',money(get('commissions'),currency)],['Swaps',money(get('swaps'),currency)],
    ['Ending equity',money(main.endingEquity,currency)]
  ]){const row=el('div',undefined,'rule-line');row.append(el('span',label),el('b',value));stats.append(row);}
  const directions=el('section',undefined,'direction-metrics');directions.append(el('h3','Direction breakdown'));
  const table=el('table'),thead=el('thead'),tr=el('tr');for(const label of ['SIDE','TRADES','NET P/L','PF'])tr.append(el('th',label));thead.append(tr);table.append(thead);
  const tbody=el('tbody');for(const side of ['long','short']){const row=el('tr');for(const value of [side.toUpperCase(),num(report.tradeStatistics?.totalTrades?.[side],0),money(report.tradeStatistics?.netProfit?.[side],currency),num(report.tradeStatistics?.profitFactor?.[side])])row.append(el('td',value));tbody.append(row);}table.append(tbody);const scroll=el('div',undefined,'table-scroll');scroll.append(table);directions.append(scroll,el('p','— indicates a field the native report did not provide. No missing statistics are estimated.','run-caption'));
  const button=el('button','Download native report ↗','text-button');button.onclick=()=>download(run.report);directions.append(button);lower.append(stats,directions);panel.append(lower);
}
async function loadRunAnalysis(force=false){
  const run=state.runs.find(r=>r.report===selectedReport);if(!run)return;
  const key=JSON.stringify(run);if(!force&&analysisKey===key)return;analysisKey=key;
  const request=++analysisRequest,panel=$('run-analysis');panel.replaceChildren(el('p','Reading native report…','empty'));panel.setAttribute('aria-busy','true');
  try{
    let report=reportCache.get(key);
    if(!report){
      const response=await fetch('/api/artifact?path='+encodeURIComponent(run.report),{headers:{'X-Lab-Token':window.LAB_TOKEN}});
      if(!response.ok)throw Error('Native report unavailable.');
      const text=await response.text();
      const match=text.match(/<script\b[^>]*id=["']backtesting-report["'][^>]*>([\s\S]*?)<\/script>/i);
      report=JSON.parse(match?match[1]:text);
      if(!report.main||typeof report.main!=='object')throw Error('Native report format is not supported.');
      reportCache.set(key,report);
    }
    if(request===analysisRequest)renderReportAnalysis(run,report);
  }catch(error){if(request===analysisRequest){panel.replaceChildren(el('p',error.message+' Summary metrics remain available in the report ledger below.','error-banner'));const retry=el('button','Retry report','text-button');retry.onclick=()=>loadRunAnalysis(true);panel.append(retry);}}
  finally{if(request===analysisRequest)panel.removeAttribute('aria-busy');}
}
function syncRunSelector(){
  const select=$('run-select'),runs=rankedRuns();
  const signature=JSON.stringify(runs.map(r=>[r.report,r.id,curveScoreLabel(r)]));
  if(select.dataset.signature!==signature){
    select.dataset.signature=signature;select.replaceChildren();
    for(const [index,run]of runs.entries()){const option=el('option',`${curveScore(run)===null?'Unranked':String(index+1).padStart(2,'0')} · ${run.id} · ${curveScoreLabel(run)}`);option.value=run.report;select.append(option);}
  }
  if(!state.runs.some(r=>r.report===selectedReport))selectedReport=runs[0]?.report||'';
  select.value=selectedReport;select.disabled=!state.runs.length;
  if(!selectedReport){analysisRequest++;analysisKey='';$('run-analysis').removeAttribute('aria-busy');$('run-analysis').replaceChildren(el('p','No native reports yet. Completed runs with report artifacts will appear here.','empty'));return;}
  if(!$('runs-view').hidden)loadRunAnalysis();
}
function inspectReport(path){selectedReport=path;setView('runs');syncRunSelector();$('run-analysis').scrollIntoView({behavior:window.matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth',block:'start'});}
$('run-select').onchange=e=>{selectedReport=e.target.value;loadRunAnalysis();};

function renderCoverage(){
  const signature=JSON.stringify(state.coverage);if(signature===coverageSignature)return;coverageSignature=signature;
  const table=$('coverage-table');table.replaceChildren();
  for(const r of state.coverage||[]){const tr=el('tr');const name=el('td',`${r.id.startsWith('legacy:')?'LEGACY':r.id} · ${r.name}`);name.append(el('br'),el('span',r.source,'source-name'));const status=el('td',names[r.status]||r.status);let text=r.latest_result?resultLine(r.latest_result):r.prior_experiment?`${r.prior_experiment.id}: ${r.prior_experiment.result}`:r.required_inputs.length?'Needs: '+r.required_inputs.join(', '):r.attempt_count?'No verified strategy result yet':'Not run';const result=el('td',text);if(r.latest_job)result.append(el('br'),el('span',r.latest_job,'artifact'));tr.append(name,status,result);table.append(tr);}
}
function renderRuns(){
  const old={};document.querySelectorAll('.job[data-job]').forEach(n=>{old[n.dataset.job]={open:n.querySelector('details')?.open,scroll:n.querySelector('pre')?.scrollTop||0};});
  const list=$('active-jobs');list.replaceChildren();
  const ordered=rankedRuns(),rank=new Map(ordered.map((run,index)=>[run.report,index]));
  const jobs=state.jobs.slice().sort((a,b)=>(a.status==='running'?0:1)-(b.status==='running'?0:1)||(rank.get(a.directory+'/report.html')??Infinity)-(rank.get(b.directory+'/report.html')??Infinity));
  for(const j of jobs){const node=el('article',undefined,'job');node.dataset.job=j.id;const head=el('div',undefined,'job-head');head.append(el('h3',`${j.strategy} / ${j.title} · ${j.branch}`),el('span',j.kind==='data_probe'?'DATA DIAGNOSTIC':j.status.replaceAll('_',' ').toUpperCase(),'badge'));node.append(head,el('p',j.message));if(j.summary){node.append(el('p',resultLine(j.summary)));const report=state.runs.find(r=>r.report===j.directory+'/report.html');if(report){const inspect=el('button','View run analytics ↗','text-button');inspect.onclick=()=>inspectReport(report.report);node.append(inspect);}}for(const issue of j.audit_issues||[])node.append(el('p','Review: '+issue));node.append(el('div',j.note||j.directory,'artifact'));
    for(const [label,path]of [['Download run manifest',j.directory+'/job.json'],...(j.summary?[['Download native report',j.directory+'/report.html']]:[])]){const b=el('button',label+' ↗','text-button');b.onclick=()=>download(path);node.append(b);}
    if(j.log){const details=el('details');details.open=old[j.id]?.open??j.status==='running';const pre=el('pre',j.log);details.append(el('summary','Native runner log'),pre);node.append(details);list.append(node);pre.scrollTop=old[j.id]?.scroll||0;}else list.append(node);
  }
  if(!state.jobs.length)list.append(el('p','No new platform attempts yet.','empty'));
  const sig=JSON.stringify(state.runs);if(sig===reportSignature)return;reportSignature=sig;const table=$('results-table');table.replaceChildren();
  for(const [index,run]of ordered.entries()){const tr=el('tr');const label=run.kind==='data_probe'?'DATA DIAGNOSTIC':(run.status||'archived_observation').replaceAll('_',' ');const values=[`${curveScore(run)===null?'Unranked':String(index+1).padStart(2,'0')} · ${run.id}`,`${run.data_mode||'unverified'} / ${label}`,`$${num(run.net)}`,num(run.profit_factor),num(run.trades,0),`${num(run.drawdown)}%`,curveScoreLabel(run)];values.forEach((v,i)=>{const td=el('td',v,i===2&&run.net<0?'negative':'');if(i===0){td.title=run.period||'';const b=el('button','View analytics ↗','text-button');b.onclick=()=>inspectReport(run.report);td.append(el('br'),b);}tr.append(td);});table.append(tr);}
}
function render(){
  $('count-catalog').textContent=state.catalog.length;$('count-ready').textContent=state.catalog.filter(r=>r.status==='ready').length;$('count-blocked').textContent=state.catalog.filter(r=>r.status==='needs_data').length;$('count-reports').textContent=state.runs.length;
  $('connection-label').textContent={not_connected:'Connect cTrader',checking:'Checking account…',connected:'Account connected',error:'Connection needs attention'}[state.connection.status];$('engine-status').textContent=state.cli_installed?'Installed':'Not installed';$('connection-message').textContent=state.connection.message;
  const sig=JSON.stringify([state.catalog,state.legacy_sources]);if(sig!==catalogSignature){catalogSignature=sig;renderCatalog();renderDetail();}
  renderCurrent();renderCoverage();renderRuns();syncRunSelector();
}
function setView(view){for(const name of ['strategies','runs','next'])$(name+'-view').hidden=name!==view;document.querySelectorAll('[data-view]').forEach(b=>{const active=b.dataset.view===view;b.classList.toggle('active',active);b.setAttribute('aria-pressed',String(active));});if(state&&view==='runs')syncRunSelector();}
async function refresh(){if(refreshing)return;refreshing=true;try{state=await api('/api/state');render();$('last-sync').textContent='UPDATED '+new Date().toLocaleTimeString('en-GB');}catch(e){$('last-sync').textContent='LOCAL SERVER UNAVAILABLE';if(!state)toast(e.message);}finally{refreshing=false;}}
for(const b of document.querySelectorAll('[data-filter]'))b.onclick=()=>{filter=b.dataset.filter;document.querySelectorAll('[data-filter]').forEach(n=>n.classList.toggle('active',n===b));renderCatalog();renderDetail();};
for(const b of document.querySelectorAll('[data-view]'))b.onclick=()=>setView(b.dataset.view);
$('search').oninput=e=>{search=e.target.value;renderCatalog();renderDetail();};$('connection-open').onclick=openConnection;$('connection-close').onclick=()=>$('connection-dialog').close();
$('connection-form').onsubmit=async e=>{e.preventDefault();const form=e.currentTarget;$('connect-submit').disabled=true;try{await api('/api/connect',Object.fromEntries(new FormData(form)));form.elements.password.value='';await refresh();}catch(error){$('connection-message').textContent=error.message;}finally{$('connect-submit').disabled=false;}};
$('verify-saved').onclick=async()=>{try{await api('/api/verify',{});await refresh();}catch(e){$('connection-message').textContent=e.message;}};
refresh();setInterval(refresh,3000);
