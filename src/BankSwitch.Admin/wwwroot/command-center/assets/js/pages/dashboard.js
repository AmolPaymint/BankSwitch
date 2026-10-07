import { api } from '../core/api.js';
import { pageHero, kpi, hasPolicy } from '../components/layout.js';
import { drawLineChart, drawDonut } from '../components/charts.js';
import { mountTable, tag } from '../core/utils.js';
import { store } from '../core/store.js';

const n=v=>Number(v||0);
const pct=v=>`${n(v).toFixed(1)}%`;
let activeRoot=null;
let unsubscribe=null;

export async function render(root){
  activeRoot=root;
  if(unsubscribe){unsubscribe();unsubscribe=null;}
  const overview=store.state.realtimeOverview || await api.overview();
  let alerts=Array.isArray(store.state.realtimeAlerts)?store.state.realtimeAlerts:[];
  if(!alerts.length && hasPolicy('Operations')){ try{ alerts=await api.alerts(8); }catch{} }
  const tx=await api.transactions({pageSize:8});
  paint(root,overview,alerts,tx);
  unsubscribe=store.subscribe(state=>{
    if(state.page!=='dashboard'||!activeRoot?.isConnected||!state.realtimeOverview)return;
    updateLive(activeRoot,state.realtimeOverview,state.tpsSamples||[]);
  });
}

function paint(root,overview,alerts,tx){
  const metrics=overview.metrics||{};
  const ops=overview.operations||{};
  const devices=Array.isArray(overview.devices)?overview.devices:[];
  const approval=tx.totalCount ? (tx.approvedCount/tx.totalCount*100) : 0;
  root.innerHTML=pageHero('Enterprise Command Center','BankSwitch Operations Dashboard','Live backend-connected operational view for switch health, transactions, devices, incidents and enterprise services.',`<a class="btn primary" href="/Monitoring">Monitoring</a><a class="btn" href="/ConfigChanges">Approval Queue</a>`)
  + `<div class="grid cols-5"><div id="kpiTps">${kpi('Live TPS',n(metrics.tpsLast60Seconds).toLocaleString(),'rolling 60 seconds','var(--green)')}</div>${kpi('Approval Rate',pct(approval),`${tx.approvedCount||0} approved`)}<div id="kpiLatency">${kpi('Avg Latency',`${n(metrics.averageLatencyMs).toFixed(0)} ms`,'live metric','var(--cyan)')}</div><div id="kpiQueue">${kpi('Queue Depth',n(metrics.queueDepth).toLocaleString(),'transaction queue')}</div><div id="kpiIncidents">${kpi('Open Incidents',n(ops.openIncidents),`${n(ops.criticalIncidents)} critical`,'var(--yellow)')}</div></div>
  <div class="divider"></div><div class="split"><div class="card pad"><h2 class="section-title">Operational Throughput</h2><p class="section-subtitle">Actual samples received from the BankSwitch realtime monitoring channel. No synthetic values are plotted.</p><canvas id="tpsChart" class="mini-chart" aria-label="Live TPS trend"></canvas></div><div class="card pad"><h2 class="section-title">Transaction Mix</h2><canvas id="approvalChart" class="mini-chart" aria-label="Approved versus declined transactions"></canvas></div></div>
  <div class="divider"></div><div class="grid cols-2"><div class="card pad"><h2 class="section-title">Device / Node Health</h2><div id="deviceHealth" class="status-grid">${deviceTiles(devices)}</div></div><div class="card pad"><h2 class="section-title">Operational Alerts</h2><div class="timeline">${alertRows(alerts)}</div></div></div>
  <div class="divider"></div><div class="card pad"><h2 class="section-title">Recent Transactions</h2>${mountTable(['RRN','MTI','PAN','Amount','Scheme','Route','Response','State'],(tx.items||[]).map(t=>[t.rrn,t.mti,t.pan,`${t.currency||''} ${Number(t.amount||0).toLocaleString()}`,t.scheme,t.route,t.response,tag(t.state,t.response==='00'?'green':'yellow')]))}</div>`;
  const initial=[...(store.state.tpsSamples||[])]; if(!initial.length)initial.push(n(metrics.tpsLast60Seconds));
  drawLineChart(document.getElementById('tpsChart'),initial,'#4d8fff');
  drawDonut(document.getElementById('approvalChart'),[tx.approvedCount||0,tx.declinedCount||0],['#00e5a0','#ff4560']);
}

function updateLive(root,overview,samples){
  const metrics=overview.metrics||{};const ops=overview.operations||{};const devices=Array.isArray(overview.devices)?overview.devices:[];
  root.querySelector('#kpiTps .kpi-value')?.replaceChildren(document.createTextNode(n(metrics.tpsLast60Seconds).toLocaleString()));
  root.querySelector('#kpiLatency .kpi-value')?.replaceChildren(document.createTextNode(`${n(metrics.averageLatencyMs).toFixed(0)} ms`));
  root.querySelector('#kpiQueue .kpi-value')?.replaceChildren(document.createTextNode(n(metrics.queueDepth).toLocaleString()));
  root.querySelector('#kpiIncidents .kpi-value')?.replaceChildren(document.createTextNode(String(n(ops.openIncidents))));
  const health=root.querySelector('#deviceHealth');if(health)health.innerHTML=deviceTiles(devices);
  const canvas=root.querySelector('#tpsChart');if(canvas)drawLineChart(canvas,samples.length?samples:[n(metrics.tpsLast60Seconds)],'#4d8fff');
}
function deviceTiles(devices){return devices.slice(0,12).map(h=>`<div class="status-tile"><div class="status-name">${esc(h.name||h.nodeId)}</div><div class="status-value">${esc(h.status)}</div><div class="muted">${n(h.averageLatencyMilliseconds).toFixed(0)} ms · ${esc(h.direction)}</div></div>`).join('')||'<div class="empty">No node health records available.</div>';}
function alertRows(alerts){return alerts.slice(0,8).map(a=>`<div class="timeline-item"><div class="timeline-time">${new Date(a.firedAt||a.createdAt||a.occurredAt||Date.now()).toLocaleTimeString()}</div><div class="timeline-title">${esc(a.title||a.ruleName||a.name||'Operational alert')}</div>${tag(a.severity||'Alert',String(a.severity).toLowerCase().includes('critical')?'red':'yellow')}</div>`).join('')||'<div class="empty">No accessible active alerts.</div>';}
function esc(v){return String(v??'').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));}
