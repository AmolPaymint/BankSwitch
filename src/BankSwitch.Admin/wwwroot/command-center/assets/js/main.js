import { renderSidebar, renderTopbar, setActiveNav, hasPolicy, setConnection } from './components/layout.js';
import { store } from './core/store.js';
import { api } from './core/api.js';
import { $, toast } from './core/utils.js';
import { RealtimeClient } from './core/realtime.js';
import { addNotification, ingestAlerts } from './core/notifications.js';

const pages = {
  dashboard: ['Viewer',()=>import('./pages/dashboard.js')], switch:['Viewer',()=>import('./pages/switch.js')], routing:['ConfigMakerOrChecker',()=>import('./pages/routing.js')], transactions:['Viewer',()=>import('./pages/transactions.js')],
  cards:['Operations',()=>import('./pages/cards.js')], atm:['Operations',()=>import('./pages/atm.js')], 'atm-lod':['ConfigMakerOrChecker',()=>import('./pages/atm-lod.js')], pos:['Operations',()=>import('./pages/pos.js')], ecommerce:['Operations',()=>import('./pages/ecommerce.js')],
  settlement:['FinanceOfficer',()=>import('./pages/settlement.js')], reconciliation:['ReconciliationOfficer',()=>import('./pages/reconciliation.js')], disputes:['Operations',()=>import('./pages/disputes.js')], hsm:['SecurityAdmin',()=>import('./pages/hsm.js')],
  risk:['RiskOperations',()=>import('./pages/risk.js')], certification:['Operations',()=>import('./pages/certification.js')], integrations:['Viewer',()=>import('./pages/integrations.js')], compliance:['Auditor',()=>import('./pages/compliance.js')],
  reports:['Viewer',()=>import('./pages/reports.js')], settings:['ConfigMakerOrChecker',()=>import('./pages/settings.js')],
  approvals:['ConfigMakerOrChecker',()=>import('./pages/approvals.js')], history:['ConfigMakerOrChecker',()=>import('./pages/config-history.js')], snapshots:['ConfigMakerOrChecker',()=>import('./pages/snapshots.js')],
  'feature-flags':['ConfigMakerOrChecker',()=>import('./pages/feature-flags.js')], 'security-admin':['SecurityAdmin',()=>import('./pages/security-admin.js')], diagnostics:['Viewer',()=>import('./pages/diagnostics.js')]
};

let routeGeneration=0;
async function route(){
  const generation=++routeGeneration;
  let page=(location.hash.replace('#/','') || 'dashboard').split('?')[0];
  const root=$('#page-root');
  if(!pages[page]) page='dashboard';
  if(!hasPolicy(pages[page][0])){
    root.innerHTML='<div class="card pad"><h2 class="section-title">Access denied</h2><p class="subtle">Your BankSwitch role does not grant access to this module.</p></div>';
    root.focus({preventScroll:true});
    return;
  }
  root.setAttribute('aria-busy','true');
  root.innerHTML='<div class="empty" role="status">Loading live BankSwitch module...</div>';
  try{
    const mod=await pages[page][1]();
    if(generation!==routeGeneration) return;
    await mod.render(root);
    store.set({page,lastSync:new Date()});
    setActiveNav(page);
    $('#sidebar').classList.remove('open');
    $('#menuToggle')?.setAttribute('aria-expanded','false');
    root.removeAttribute('aria-busy');
    root.focus({preventScroll:true});
  }catch(err){
    console.error(err);
    root.removeAttribute('aria-busy');
    root.innerHTML=`<div class="card pad error-panel" role="alert"><h2 class="section-title">Module Error</h2><p class="subtle">${escapeHtml(err.message)}</p><p class="muted">Check backend API health, network connectivity and your assigned role.</p><button class="btn primary" id="retryModule">Retry</button></div>`;
    $('#retryModule')?.addEventListener('click',route);
    toast('Unable to load live module','error');
  }
}

function escapeHtml(v){return String(v??'').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));}

function startRealtime(){
  const rt=new RealtimeClient();
  rt.addEventListener('state',e=>{
    const state=e.detail.state;
    const connected=state==='connected';
    store.set({realtimeState:state,realtimeConnectionId:e.detail.connectionId||null});
    setConnection(connected,connected?'Realtime connected':state==='connecting'?'Connecting…':'REST fallback');
  });
  rt.addEventListener('overview',e=>{
    const current=e.detail||{};
    const sample=Number(current.metrics?.tpsLast60Seconds||0);
    const samples=[...(store.state.tpsSamples||[]),sample].slice(-60);
    store.set({realtimeOverview:current,tpsSamples:samples,lastRealtime:new Date()});
  });
  rt.addEventListener('operations',e=>store.set({realtimeOperations:e.detail,lastRealtime:new Date()}));
  rt.addEventListener('alerts',e=>{store.set({realtimeAlerts:e.detail,lastRealtime:new Date()});ingestAlerts(e.detail);});
  rt.addEventListener('error',e=>console.warn('Realtime channel:',e.detail));
  rt.start().catch(err=>console.warn('Realtime unavailable; REST fallback enabled.',err));
  window.addEventListener('beforeunload',()=>rt.stop(),{once:true});
}

async function boot(){
  document.documentElement.dataset.theme=store.state.theme;
  try{
    const session=await api.session();
    store.set({session,connected:true,tpsSamples:[]});
  }catch(err){ console.error(err); return; }
  renderSidebar();
  renderTopbar();
  startRealtime();
  await route();
  window.addEventListener('hashchange',route);
  window.addEventListener('bankswitch:sync',route);
  document.addEventListener('keydown',e=>{
    if(e.key==='/' && !['INPUT','TEXTAREA','SELECT'].includes(document.activeElement?.tagName)){e.preventDefault();$('#globalSearch')?.focus();}
    if(e.key==='Escape' && $('#sidebar')?.classList.contains('open')){$('#sidebar').classList.remove('open');$('#menuToggle')?.setAttribute('aria-expanded','false');$('#menuToggle')?.focus();}
  });
  setInterval(()=>{
    if(document.visibilityState==='visible' && store.state.realtimeState!=='connected' && store.state.page==='dashboard') route();
  },Math.max(10000,Number(store.state.refreshMs||10000)));
  addNotification({title:'Command Center ready',message:'Secure REST integration active; realtime channel is starting.',severity:'info',source:'System',key:`boot-${location.pathname}-${document.body.dataset.bsVersion}`});
}
boot();
