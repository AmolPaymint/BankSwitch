import { api } from '../core/api.js';
import { CONFIG } from '../core/config.js';
import { pageHero,kpi } from '../components/layout.js';
import { mountTable,tag,toast } from '../core/utils.js';
import { openModal } from '../components/modal.js';

const state={environment:CONFIG.environment||'UAT',scope:'GLOBAL',domain:'',query:'',domains:[],definitions:[],values:[],completeness:null};
const esc=v=>String(v??'').replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));
const colorForSensitivity=s=>({Critical:'red',Sensitive:'yellow',Operational:'blue',Informational:'green'})[s]||'blue';

async function load(){
  const [domains,definitions,diagnostics,requests,completeness]=await Promise.all([
    api.settings('/domains'), api.settings('/definitions'), api.settings('/diagnostics').catch(()=>[]),
    api.settings('/change-requests').catch(()=>[]), api.settings('/completeness').catch(()=>null)
  ]);
  state.domains=Array.isArray(domains)?domains:[];
  state.definitions=Array.isArray(definitions)?definitions:[];
  state.completeness=completeness;
  return {diagnostics:Array.isArray(diagnostics)?diagnostics:[],requests:Array.isArray(requests)?requests:[]};
}
async function loadValues(){
  const q=new URLSearchParams({environment:state.environment,institutionScope:state.scope});
  if(state.domain)q.set('domain',state.domain);
  state.values=await api.settings(`/values?${q}`); return state.values;
}
function currentFor(def){return state.values.find(v=>String(v.definitionId).toLowerCase()===String(def.id).toLowerCase());}
function domainLabel(code){return state.domains.find(d=>d.code===code)?.name||code;}
function completionFor(def){return state.completeness?.items?.find(x=>x.domainCode===def.domainCode&&x.key===def.key);}
function filteredDefinitions(){
  const q=state.query.trim().toLowerCase();
  return state.definitions.filter(d=>(!state.domain||d.domainCode===state.domain)&&(!q||`${d.displayName} ${d.key} ${d.domainCode} ${d.description}`.toLowerCase().includes(q)));
}
function configRows(){
  return filteredDefinitions().map(d=>{const v=currentFor(d);const c=completionFor(d);return [
    `<b>${esc(d.displayName)}</b><div class="muted">${esc(d.key)}</div><div class="muted" style="font-size:11px">${esc(d.description||'')}</div>`,
    esc(domainLabel(d.domainCode)),
    v?esc(d.isSecret?'Configured reference':v.value):`<span class="muted">${esc(d.defaultValue??'Not set')}</span>`,
    `${tag(d.sensitivity,colorForSensitivity(d.sensitivity))} ${d.productionLocked?tag('PROD locked','red'):''}`,
    `${tag(d.reloadPolicy,'cyan')}<div class="muted" style="font-size:10px">${c?.runtimeBehavior?esc(c.runtimeBehavior):''}</div>`,
    c?tag(c.status,c.status==='Complete'?'green':'yellow'):'',
    `<button class="btn small edit-setting" data-id="${d.id}">Change</button>`
  ];});
}
function bindChangeButtons(){document.querySelectorAll('.edit-setting').forEach(b=>b.onclick=()=>openChange(b.dataset.id));}
function parseAllowed(d){try{return JSON.parse(d.allowedValuesJson||'[]')}catch{return []}}
function inputFor(d,value){
  const allowed=parseAllowed(d); const id='cfgNew'; const val=value??d.defaultValue??'';
  if(allowed.length) return `<select id="${id}" class="select">${allowed.map(x=>`<option value="${esc(x)}" ${String(x)===String(val)?'selected':''}>${esc(x)}</option>`).join('')}</select>`;
  if(d.valueType==='Boolean') return `<select id="${id}" class="select"><option value="true" ${String(val).toLowerCase()==='true'?'selected':''}>true</option><option value="false" ${String(val).toLowerCase()==='false'?'selected':''}>false</option></select>`;
  if(d.valueType==='Integer'||d.valueType==='Decimal') return `<input id="${id}" type="number" ${d.valueType==='Decimal'?'step="any"':'step="1"'} class="input" value="${esc(val)}" ${d.minimumValue!=null?`min="${d.minimumValue}"`:''} ${d.maximumValue!=null?`max="${d.maximumValue}"`:''}>`;
  if(d.valueType==='Json') return `<textarea id="${id}" class="input" rows="7" spellcheck="false">${esc(val)}</textarea>`;
  if(d.valueType==='Uri') return `<input id="${id}" type="url" class="input" value="${esc(val)}" placeholder="https://...">`;
  if(d.valueType==='SecretReference') return `<input id="${id}" class="input" value="${esc(val)}" placeholder="vault://, kv:// or hsm:// reference" autocomplete="off">`;
  if(d.valueType==='CertificateReference') return `<input id="${id}" class="input" value="${esc(val)}" placeholder="Certificate inventory reference">`;
  if(d.valueType==='Duration') return `<input id="${id}" class="input" value="${esc(val)}" placeholder="00:05:00">`;
  return `<input id="${id}" class="input" value="${esc(val)}" ${d.validationPattern?`pattern="${esc(d.validationPattern)}"`:''}>`;
}
function openChange(id){
  const d=state.definitions.find(x=>x.id===id); if(!d)return; const v=currentFor(d); const c=completionFor(d);
  const current=d.isSecret?(v?'Configured reference':'Not configured'):(v?.value??d.defaultValue??'Not set');
  openModal(`Change ${esc(d.displayName)}`,`<div class="form-grid">
    <div class="field"><label>Domain</label><input class="input" value="${esc(domainLabel(d.domainCode))}" disabled></div>
    <div class="field"><label>Setting Key</label><input class="input" value="${esc(d.key)}" disabled></div>
    <div class="field"><label>Current Value</label><input class="input" value="${esc(current)}" disabled></div>
    <div class="field"><label>Value Type</label><input class="input" value="${esc(d.valueType)}" disabled></div>
    <div class="field" style="grid-column:1/-1"><label>New Value ${d.isSecret?'(reference only — secret value is never accepted)':''}</label>${inputFor(d,v?.value)}</div>
    <div class="field" style="grid-column:1/-1"><label>Description / Operator Guidance</label><div class="card pad muted">${esc(d.description||'No guidance provided.')}</div></div>
    <div class="field"><label>Sensitivity</label><input class="input" value="${esc(d.sensitivity)}${d.productionLocked?' / PROD locked':''}" disabled></div>
    <div class="field"><label>Reload Policy</label><input class="input" value="${esc(d.reloadPolicy)}" disabled></div>
    <div class="field"><label>Minimum</label><input class="input" value="${esc(d.minimumValue??'—')}" disabled></div>
    <div class="field"><label>Maximum</label><input class="input" value="${esc(d.maximumValue??'—')}" disabled></div>
    <div class="field"><label>Reason</label><input id="cfgReason" class="input" placeholder="Business / operational reason"></div>
    <div class="field"><label>Ticket</label><input id="cfgTicket" class="input" placeholder="CHG-12345"></div>
    <div class="field"><label>Effective At (optional)</label><input id="cfgEffective" type="datetime-local" class="input"></div>
    <div class="field"><label>Runtime Behavior</label><input class="input" value="${esc(c?.runtimeBehavior||d.reloadPolicy)}" disabled></div>
  </div><div class="divider"></div>
  <div style="display:flex;justify-content:flex-end;gap:10px"><button class="btn" id="validateCfg">Validate</button><button class="btn primary" id="draftCfg">Create Draft</button></div>
  <div id="validationResult" class="muted" style="margin-top:12px"></div>`);
  const payload=()=>({environment:state.environment,institutionScope:state.scope,reason:document.getElementById('cfgReason').value||'Configuration update',ticketReference:document.getElementById('cfgTicket').value||'UNTRACKED',effectiveAt:document.getElementById('cfgEffective').value?new Date(document.getElementById('cfgEffective').value).toISOString():null,items:[{domainCode:d.domainCode,key:d.key,newValue:document.getElementById('cfgNew').value}]});
  document.getElementById('validateCfg').onclick=async()=>{try{const r=await api.settings('/validate',{method:'POST',body:JSON.stringify(payload())});document.getElementById('validationResult').innerHTML=r.isValid?`${tag('Valid','green')} Ready for maker-checker submission.`:(r.issues||[]).map(i=>`<div>${tag(i.severity,i.severity==='Error'?'red':'yellow')} ${esc(i.message)}</div>`).join('');}catch(e){document.getElementById('validationResult').innerHTML=`${tag('Error','red')} ${esc(e.message)}`;}};
  document.getElementById('draftCfg').onclick=async()=>{try{const validation=await api.settings('/validate',{method:'POST',body:JSON.stringify(payload())});if(!validation.isValid){document.getElementById('validationResult').innerHTML=(validation.issues||[]).map(i=>`<div>${tag(i.severity,'red')} ${esc(i.message)}</div>`).join('');return;}const r=await api.settings('/change-requests',{method:'POST',body:JSON.stringify(payload())});toast(`Draft ${r.correlationId} created`);document.getElementById('modalHost')?.classList.remove('show');location.hash='#/approvals';}catch(e){toast(e.message,'error')}};
}
export async function render(root){
  const {diagnostics,requests}=await load(); await loadValues();
  const pending=requests.filter(x=>['Submitted','Approved','Scheduled'].includes(x.state));
  const critical=state.definitions.filter(x=>x.sensitivity==='Critical').length;
  const comp=state.completeness;
  root.innerHTML=pageHero('Enterprise Administration','Configuration Control Plane','Complete field-level BankSwitch configuration registry with typed controls, validation, maker-checker governance, runtime application policy, versioning and rollback.',`<button id="refreshSettings" class="btn">Refresh</button><a class="btn primary" href="#/approvals">Maker-Checker Inbox</a>`)
   +`<div class="grid cols-5">${kpi('Domains',comp?.domainCount??state.domains.length,'enterprise domains')}${kpi('Definitions',comp?.definitionCount??state.definitions.length,'field-level settings')}${kpi('Completeness',comp?`${comp.completionPercent}%`:'—','control coverage','var(--green)')}${kpi('Pending',pending.length,'approval / deployment','var(--yellow)')}${kpi('Diagnostics',diagnostics.length,'runtime probes')}</div>
   <div class="divider"></div><div class="card pad"><div class="admin-toolbar"><div class="field"><label>Environment</label><select id="cfgEnv" class="select">${['DEV','SIT','UAT','PREPROD','PROD','DR'].map(x=>`<option ${x===state.environment?'selected':''}>${x}</option>`).join('')}</select></div><div class="field"><label>Institution Scope</label><input id="cfgScope" class="input" value="${esc(state.scope)}"></div><div class="field"><label>Domain</label><select id="cfgDomain" class="select"><option value="">All Domains</option>${state.domains.map(d=>`<option value="${esc(d.code)}" ${d.code===state.domain?'selected':''}>${esc(d.name)}</option>`).join('')}</select></div><div class="field"><label>Search</label><input id="cfgSearch" class="input" value="${esc(state.query)}" placeholder="setting, key or description"></div><div class="field admin-action"><label>&nbsp;</label><button id="applyFilter" class="btn primary">Load Settings</button></div></div></div>
   <div class="divider"></div><div class="card pad"><h2 class="section-title">Configuration Registry</h2><p class="section-subtitle">Every setting exposes type, sensitivity, runtime reload behavior, maker-checker control and secret-safe handling. Restart-required changes are never falsely reported as hot-applied.</p>${mountTable(['Setting','Domain','Current Value','Sensitivity','Runtime','Coverage','Action'],configRows())}</div>`;
  bindChangeButtons();
  document.getElementById('applyFilter').onclick=async()=>{state.environment=document.getElementById('cfgEnv').value;state.scope=document.getElementById('cfgScope').value||'GLOBAL';state.domain=document.getElementById('cfgDomain').value;state.query=document.getElementById('cfgSearch').value||'';await render(root)};
  document.getElementById('cfgSearch').addEventListener('keydown',e=>{if(e.key==='Enter')document.getElementById('applyFilter').click()});
  document.getElementById('refreshSettings').onclick=()=>render(root);
}
