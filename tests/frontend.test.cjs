// Synthetic native-report fixtures verify the interface, not market performance.
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const {JSDOM,VirtualConsole}=require('jsdom');
const root=path.resolve(__dirname,'..');
const read=name=>fs.readFileSync(path.join(root,name),'utf8');
const turn=()=>new Promise(resolve=>setImmediate(resolve));
function nativeReport(){return {
  main:{cBotName:'Fixture',symbol:'XAUUSD',depositAsset:'USD',netProfit:100,roi:1,startingCapital:10000,endingEquity:10100,testingPeriod:{startDate:0,endDate:10*86400000,formatted:'10 calendar days'},data:{type:'tickDataFromServer'}},
  tradeStatistics:{totalTrades:{all:10,long:6,short:4},winningTrades:{all:6},losingTrades:{all:4},profitFactor:{all:1.5},averageTrade:{all:10}},
  equity:{maxEquityDrawdownPercent:2,maxEquityDrawdownAbsolute:200,points:[{timestamp:0,balance:10000,minEquity:10000,maxEquity:10000},{timestamp:86400000,balance:10100,minEquity:9950,maxEquity:10150}]}
};}
function run(id,roi,drawdown,extra={}){return {id,report:`outputs/${id}/report.html`,roi,drawdown,trades:10,net:roi*100,status:'needs_review',...extra};}
async function setup(t,runs=[],report=nativeReport()){
  const errors=[],requests=[],vc=new VirtualConsole();vc.on('jsdomError',error=>errors.push(error));
  const dom=new JSDOM(read('backtest_lab/index.html'),{runScripts:'outside-only',url:'http://127.0.0.1:8765',virtualConsole:vc});t.after(()=>dom.window.close());
  const w=dom.window;
  const state={catalog:[],legacy_sources:[],coverage:[],runs,jobs:[],current_job:null,connection:{status:'not_connected',message:''},cli_installed:false};
  w.setInterval=()=>0;w.matchMedia=()=>({matches:false});w.HTMLElement.prototype.scrollIntoView=()=>{};
  w.fetch=async (url,options)=>{requests.push({url,options});return url==='/api/state'?{ok:true,json:async()=>state}:{ok:true,text:async()=>`<script type="application/json" id="backtesting-report">${JSON.stringify(report)}</script>`};};
  // Keep application internals private in production; expose an evaluator only in this harness.
  w.eval(read('backtest_lab/app.js')+'\nwindow.inspect=code=>eval(code);');await turn();await turn();
  assert.deepEqual(errors,[]);
  return {w,state,requests,inspect:w.inspect};
}
test('ranks risk-adjusted curves consistently across selector and report ledger',async t=>{
  const {w,state,inspect}=await setup(t,[run('rough',40,20),run('steady',20,2),run('losing',-5,5),run('invalid',100,1,{status:'invalid_attempt'}),run('probe',100,1,{kind:'data_probe'}),run('empty',0,0,{trades:0}),run('unknown',1,null)]);
  const order=Array.from(inspect('rankedRuns()'),r=>r.id);assert.deepEqual(order.slice(0,3),['steady','rough','losing']);
  assert.equal(state.runs[0].id,'rough','sort must not mutate API state');
  assert.equal(w.document.querySelector('#run-select option').value,'outputs/steady/report.html');
  assert(w.document.querySelector('#results-table td').textContent.includes('steady'));
  assert.equal(inspect('curveScore(state.runs[0])'),2);
  assert.equal(inspect('curveScore({...state.runs[0],drawdown:0})'),Infinity);
  assert.equal(inspect('curveScore({...state.runs[0],status:"failed"})'),null);
});
test('What’s next navigation displays proposals and returns to saved analytics',async t=>{
  const {w}=await setup(t,[run('saved',1,2)]);
  w.document.querySelector('[data-view="next"]').click();
  assert.equal(w.document.getElementById('next-view').hidden,false);
  assert.equal(w.document.getElementById('runs-view').hidden,true);
  assert.equal(w.document.querySelector('[data-view="next"]').getAttribute('aria-pressed'),'true');
  assert.equal(w.document.querySelectorAll('.roadmap-card').length,6);
  w.document.querySelector('[data-view="strategies"]').click();assert.equal(w.document.getElementById('strategies-view').hidden,false);assert.equal(w.document.getElementById('next-view').hidden,true);
  w.document.querySelector('[data-view="runs"]').click();await turn();
  assert.equal(w.document.getElementById('runs-view').hidden,false);assert.equal(w.document.querySelectorAll('.metric-card').length,8);
});
test('native report metrics use closed counts and elapsed calendar days',async t=>{
  const {w,inspect,requests}=await setup(t,[run('native',1,2)]);
  const values=Array.from(w.document.querySelectorAll('.metric-card strong'),n=>n.textContent);
  assert.deepEqual(values.slice(3),['60.00%','10','6','4','1.00']);
  assert.equal(inspect('reportMetrics('+JSON.stringify(nativeReport())+').tradesPerDay'),1);
  assert(w.document.querySelector('svg .equity-range'));assert(w.document.querySelector('svg .balance-line'));
  assert(requests.every(r=>!r.options?.method||r.options.method==='GET'),'page reads must never start a backtest');
});
test('zero trades and missing equity remain explicit',async t=>{
  const report=nativeReport();report.tradeStatistics={totalTrades:{all:0},winningTrades:{all:0},losingTrades:{all:0}};report.equity={};
  const {w}=await setup(t,[run('zero',0,0,{trades:0})],report);
  const values=Array.from(w.document.querySelectorAll('.metric-card strong'),n=>n.textContent);
  assert.equal(values[1],'—');assert.equal(values[3],'—');assert.equal(values[4],'0');assert.equal(values[7],'0.00');
  assert(w.document.getElementById('run-analysis').textContent.includes('does not include an equity series'));
});
test('strategy summaries resolve exact job identity and distinguish archive controls',async t=>{
  const {state,inspect}=await setup(t);
  state.catalog=[{id:'T007',source:'XauUsdClockVolatilityStopRisk.cs',contract:'Fixed versus volatility stops.'}];
  state.jobs=[{directory:'outputs/test',strategy:'T007',branch:'fixed'}];
  const summary=inspect('strategySynopsis({id:"test",report:"outputs/test/report.html"},{main:{cBotName:"Generic"}})');
  assert(summary.text.includes('equal initial dollar risk'));assert.equal(summary.job.branch,'fixed');
  assert(inspect('strategySynopsis({id:"EXP-009-always-long-2026",report:"archive"},{main:{}})').text.includes('always buys'));
  assert(inspect('strategySynopsis({id:"unknown",report:"archive"},{main:{}})').text.includes('No strategy description'));
});
test('missing report shows retry and a late response cannot overwrite a new selection',async t=>{
  const {w,state,inspect}=await setup(t,[run('first',2,1),run('second',1,1)]);
  w.fetch=async()=>({ok:false});state.runs.push(run('missing',1,2));
  await inspect('selectedReport=state.runs[2].report;loadRunAnalysis()');
  assert(w.document.querySelector('#run-analysis .error-banner'));assert.equal(w.document.querySelector('#run-analysis button').textContent,'Retry report');
  let resolveSlow;state.runs.push(run('slow',1,2));
  w.fetch=()=>new Promise(resolve=>{resolveSlow=()=>resolve({ok:true,text:async()=>JSON.stringify(nativeReport())});});
  const slow=inspect('selectedReport=state.runs[3].report;loadRunAnalysis()');
  await inspect('selectedReport=state.runs[0].report;loadRunAnalysis()');resolveSlow();await slow;
  assert(w.document.querySelector('.selected-run-heading p').textContent.includes('first'));
});
test('empty report list clears analytics and disables selection',async t=>{
  const {w,state,inspect}=await setup(t,[run('first',2,1)]);state.runs=[];inspect('syncRunSelector()');
  assert.equal(w.document.getElementById('run-select').disabled,true);assert(w.document.getElementById('run-analysis').textContent.includes('No native reports yet'));
});
test('stylesheet parses with dark controls and reduced-motion rules',t=>{
  const errors=[],vc=new VirtualConsole();vc.on('jsdomError',error=>errors.push(error));
  const dom=new JSDOM('<style>'+read('backtest_lab/styles.css')+'</style>',{virtualConsole:vc});t.after(()=>dom.window.close());
  assert.deepEqual(errors,[]);assert(dom.window.document.styleSheets[0].cssRules.length>0);
  assert(read('backtest_lab/styles.css').includes('color-scheme:dark'));assert(read('backtest_lab/styles.css').includes('prefers-reduced-motion:reduce'));
});
