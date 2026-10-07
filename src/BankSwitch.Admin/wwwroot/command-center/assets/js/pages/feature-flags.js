import { api } from '../core/api.js';
import { CONFIG } from '../core/config.js';
import { pageHero,kpi,hasPolicy } from '../components/layout.js';
import { mountTable,tag,toast } from '../core/utils.js';
const env=CONFIG.environment||'UAT',scope='GLOBAL';
export async function render(root){
 let rows=await api.settings(`/feature-flags?environment=${encodeURIComponent(env)}&institutionScope=${encodeURIComponent(scope)}`);rows=Array.isArray(rows)?rows:[];
 root.innerHTML=pageHero('Enterprise Administration','Feature Flags','Controlled rollout of switch capabilities by environment and institution scope.',`<a class="btn" href="#/history">Audit History</a>`)+`<div class="grid cols-4">${kpi('Flags',rows.length,'registered')}${kpi('Enabled',rows.filter(x=>x.enabled).length,'active','var(--green)')}${kpi('Disabled',rows.filter(x=>!x.enabled).length,'inactive')}${kpi('Environment',env,scope)}</div><div class="divider"></div><div class="card pad">${mountTable(['Flag','Description','Enabled','Environment / Scope','Updated','Action'],rows.map(x=>[x.key,x.description||'',tag(x.enabled?'Enabled':'Disabled',x.enabled?'green':'red'),`${x.environment} / ${x.institutionScope}`,new Date(x.updatedAt).toLocaleString(),hasPolicy('ConfigChecker')?`<button class="btn small flag-toggle" data-key="${x.key}">${x.enabled?'Disable':'Enable'}</button>`:'View only']))}</div>`;
 document.querySelectorAll('.flag-toggle').forEach(b=>b.onclick=async()=>{const x=rows.find(r=>r.key===b.dataset.key);await api.settings(`/feature-flags/${encodeURIComponent(x.key)}`,{method:'PUT',body:JSON.stringify({...x,enabled:!x.enabled,updatedAt:new Date().toISOString()})});toast('Feature flag updated');render(root)});
}
