import { api } from '../core/api.js';
import { CONFIG } from '../core/config.js';
import { pageHero,kpi,hasPolicy } from '../components/layout.js';
import { mountTable,tag,toast } from '../core/utils.js';
const env=CONFIG.environment||'UAT', scope='GLOBAL';
export async function render(root){
  let rows=await api.settings(`/snapshots?environment=${encodeURIComponent(env)}&institutionScope=${encodeURIComponent(scope)}`);rows=Array.isArray(rows)?rows:[];
  root.innerHTML=pageHero('Enterprise Administration','Configuration Snapshots & Rollback','Signed point-in-time configuration baselines for controlled recovery and release promotion.',hasPolicy('ConfigChecker')?`<button id="createSnapshot" class="btn primary">Create Snapshot</button>`:'')+`<div class="grid cols-4">${kpi('Snapshots',rows.length,'available baselines')}${kpi('Environment',env,'active')}${kpi('Scope',scope,'institution')}${kpi('Latest Version',rows[0]?.version??'—','configuration')}</div><div class="divider"></div><div class="card pad">${mountTable(['Created','Name','Version','Created By','Checksum','Action'],rows.map(x=>[new Date(x.createdAt).toLocaleString(),x.name,x.version,x.createdBy,`<code>${String(x.checksum||'').slice(0,20)}…</code>`,hasPolicy('ConfigChecker')?`<button class="btn small danger restore-snap" data-id="${x.id}">Restore</button>`:'View only']))}</div>`;
  if(document.getElementById('createSnapshot'))document.getElementById('createSnapshot').onclick=async()=>{const name=prompt('Snapshot name:');if(!name)return;await api.settings('/snapshots',{method:'POST',body:JSON.stringify({name,environment:env,institutionScope:scope})});toast('Snapshot created');render(root)};
  document.querySelectorAll('.restore-snap').forEach(b=>b.onclick=async()=>{if(!confirm('Restore this configuration snapshot? A controlled deployment will be recorded.'))return;const reason=prompt('Restore reason:')||'Controlled snapshot restore';const ticket=prompt('Ticket reference:')||'UNTRACKED';await api.settings(`/snapshots/${b.dataset.id}/restore`,{method:'POST',body:JSON.stringify({reason,ticketReference:ticket})});toast('Snapshot restored');render(root)});
}
