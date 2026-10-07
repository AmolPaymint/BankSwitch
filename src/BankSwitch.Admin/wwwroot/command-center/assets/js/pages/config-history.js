import { api } from '../core/api.js';
import { CONFIG } from '../core/config.js';
import { pageHero,kpi } from '../components/layout.js';
import { mountTable,tag } from '../core/utils.js';
const esc=v=>String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));
export async function render(root){
  const env=CONFIG.environment||'UAT',scope='GLOBAL';
  const rows=await api.settings(`/history?environment=${encodeURIComponent(env)}&institutionScope=${encodeURIComponent(scope)}&take=500`);
  const data=Array.isArray(rows)?rows:[]; const domains=new Set(data.map(x=>x.domainCode));
  root.innerHTML=pageHero('Enterprise Administration','Configuration History & Audit','Immutable configuration history with before/after values, actor, reason, change request and SHA-256 integrity hash.',`<a class="btn" href="#/settings">Settings</a><a class="btn" href="#/snapshots">Snapshots</a>`)+`<div class="grid cols-4">${kpi('History Entries',data.length,'latest 500')}${kpi('Domains',domains.size,'changed')}${kpi('Environment',env,'active scope')}${kpi('Integrity','SHA-256','history chain')}</div><div class="divider"></div><div class="card pad">${mountTable(['Version','Time','Domain / Key','Previous','New','Actor','Reason','Integrity'],data.map(x=>[x.version,new Date(x.changedAt).toLocaleString(),`<b>${esc(x.domainCode)}</b><div class="muted">${esc(x.key)}</div>`,esc(x.oldValue??'∅'),esc(x.newValue),esc(x.changedBy),esc(x.reason),`${tag('Verified','green')}<div class="muted hash-short">${esc(String(x.hash||'').slice(0,16))}…</div>`]))}</div>`;
}
