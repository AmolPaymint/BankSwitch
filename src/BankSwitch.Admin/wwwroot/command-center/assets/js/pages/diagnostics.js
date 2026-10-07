import { api } from '../core/api.js';
import { pageHero,kpi } from '../components/layout.js';
import { mountTable,tag } from '../core/utils.js';
export async function render(root){
 let d=await api.settings('/diagnostics'); d=Array.isArray(d)?d:[]; const healthy=d.filter(x=>String(x.status).toLowerCase().includes('healthy')||String(x.status).toLowerCase()==='up').length;
 root.innerHTML=pageHero('Enterprise Administration','Runtime Diagnostics','Control-plane health across database, repositories, HSM, network and integration dependencies.',`<button class="btn primary" id="probeAgain">Run Probes</button>`)+`<div class="grid cols-4">${kpi('Components',d.length,'probed')}${kpi('Healthy',healthy,'available','var(--green)')}${kpi('Degraded',d.length-healthy,'requires review',(d.length-healthy)?'var(--yellow)':'var(--green)')}${kpi('Last Probe',new Date().toLocaleTimeString(),'current')}</div><div class="divider"></div><div class="card pad">${mountTable(['Component','Status','Latency','Checked','Message'],d.map(x=>[x.component,tag(x.status,String(x.status).toLowerCase().includes('healthy')?'green':'yellow'),x.latencyMs==null?'—':`${Number(x.latencyMs).toFixed(0)} ms`,new Date(x.checkedAt).toLocaleString(),x.message||'']))}</div>`;document.getElementById('probeAgain').onclick=()=>render(root);
}
