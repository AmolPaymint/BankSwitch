import { api } from './api.js';
import { pageHero,kpi } from '../components/layout.js';
import { mountTable, tag } from './utils.js';
const val=v=>v==null?'—':typeof v==='object'?JSON.stringify(v):String(v);
const arr=x=>Array.isArray(x)?x:Array.isArray(x?.items)?x.items:Array.isArray(x?.data)?x.data:[];
export async function liveModule(root,cfg){
 root.innerHTML=pageHero(cfg.eyebrow,cfg.title,cfg.description)+`<div class="card pad"><div class="muted">Loading live operational data…</div></div>`;
 try{
  const data=await Promise.all(cfg.sources.map(s=>api.request(s.path)));
  const cards=data.map((d,i)=>{const a=arr(d); const count=a.length || (typeof d==='object'&&d?Object.keys(d).length:0); return kpi(cfg.sources[i].label,String(count),cfg.sources[i].caption||'live backend');}).join('');
  let sections=''; data.forEach((d,i)=>{let a=arr(d); if(!a.length && d && typeof d==='object') a=[d]; const rows=a.slice(0,50); if(!rows.length){sections+=`<div class="card pad"><h2 class="section-title">${cfg.sources[i].label}</h2><div class="muted">No records returned.</div></div><div class="divider"></div>`;return;} const keys=Object.keys(rows[0]).slice(0,7); sections+=`<div class="card pad"><h2 class="section-title">${cfg.sources[i].label}</h2>${mountTable(keys.map(k=>k.replace(/([A-Z])/g,' $1')),rows.map(r=>keys.map(k=>{const v=r[k]; if(/status|state|health|decision/i.test(k)) return tag(val(v),/active|healthy|approved|pass|online|allow/i.test(val(v))?'green':/fail|block|critical|declin|offline/i.test(val(v))?'red':'yellow'); return val(v)})))}</div><div class="divider"></div>`; });
  root.innerHTML=pageHero(cfg.eyebrow,cfg.title,cfg.description)+`<div class="grid cols-${Math.min(cfg.sources.length,5)}">${cards}</div><div class="divider"></div>${sections}`;
 }catch(e){root.innerHTML=pageHero(cfg.eyebrow,cfg.title,cfg.description)+`<div class="card pad"><h2 class="section-title">Live API unavailable</h2><div class="muted">${e.message}</div><p>This screen does not fall back to mock production data.</p></div>`;}
}
