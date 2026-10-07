import { CONFIG } from '../core/config.js';
import { store } from '../core/store.js';
import { $, toast } from '../core/utils.js';
import { wireNotificationButton } from '../core/notifications.js';

const policyMap = {
  Viewer:['Viewer','Operations','CardOperations','AgencyManager','CorporateManager','RiskAnalyst','RiskManager','FinanceOfficer','ReconciliationOfficer','ConfigMaker','ConfigChecker','SecurityAdmin','Auditor','SuperAdmin'],
  Operations:['Operations','CardOperations','AgencyManager','CorporateManager','FinanceOfficer','ReconciliationOfficer','SuperAdmin'],
  RiskOperations:['RiskAnalyst','RiskManager','SuperAdmin'],
  ConfigMakerOrChecker:['ConfigMaker','ConfigChecker','RiskManager','FinanceOfficer','ReconciliationOfficer','SuperAdmin'],
  FinanceOfficer:['FinanceOfficer','SuperAdmin'],
  ReconciliationOfficer:['ReconciliationOfficer','SuperAdmin'],
  SecurityAdmin:['SecurityAdmin','SuperAdmin'],
  Auditor:['Auditor','SuperAdmin']
};
export function hasPolicy(policy){
  if(!policy) return true;
  const roles = store.state.session?.roles || [];
  return (policyMap[policy] || [policy]).some(r=>roles.includes(r));
}
export function renderSidebar(){
  const sidebar = $('#sidebar');
  const groups = CONFIG.nav.map(([section,items]) => {
    const visible = items.filter(([, , , policy])=>hasPolicy(policy));
    if(!visible.length) return '';
    return `<div class="nav-section">${section}</div>${visible.map(([id,label,color])=>`<a href="#/${id}" class="nav-link" data-page="${id}"><span class="nav-dot" style="background:${color}"></span>${label}</a>`).join('')}`;
  }).join('');
  sidebar.innerHTML = `<div class="brand"><div class="brand-title">${CONFIG.company}</div><div class="brand-sub">${CONFIG.appName} · ${CONFIG.version}</div></div>${groups}`;
}
export function renderTopbar(){
  const s=store.state.session||{};
  $('#topbar').innerHTML = `<div class="topbar-left"><button class="btn ghost menu-btn" id="menuToggle" aria-label="Open navigation" aria-expanded="false">☰</button><span class="env-badge">${CONFIG.environment.toUpperCase()}</span><div class="global-search"><label class="sr-only" for="globalSearch">Search BankSwitch</label><input class="input" id="globalSearch" autocomplete="off" placeholder="Search RRN, STAN, merchant, ATM, terminal..." /></div></div><div class="top-actions"><div class="live-chip" role="status" aria-live="polite"><span class="pulse" id="connectionPulse"></span><span id="connectionLabel">Connecting…</span></div><button class="btn small notification-btn" id="notificationBtn" aria-label="Open notifications">Alerts <span id="notificationBadge" class="notification-badge" hidden>0</span></button><button class="btn small" id="themeToggle" aria-label="Toggle light and dark theme">Theme</button><button class="btn small" id="syncBtn">Sync</button><a class="btn small" href="#/approvals">Approvals</a><a class="btn primary small" href="/Account/Logout">${s.user||'Operator'}</a></div>`;
  $('#menuToggle')?.addEventListener('click',e=>{ const open=$('#sidebar').classList.toggle('open'); e.currentTarget.setAttribute('aria-expanded',String(open)); });
  $('#themeToggle')?.addEventListener('click',()=>{ const next = document.documentElement.dataset.theme === 'light' ? 'dark' : 'light'; document.documentElement.dataset.theme = next; localStorage.setItem('bs_theme', next); });
  $('#syncBtn')?.addEventListener('click',()=>{ window.dispatchEvent(new CustomEvent('bankswitch:sync')); toast('Refreshing live operational data'); });
  $('#globalSearch')?.addEventListener('keydown', e=>{ if(e.key==='Enter') { localStorage.setItem('bs_global_search',e.target.value); location.hash = '#/transactions'; }});
  wireNotificationButton();
}
export function setConnection(ok,label=''){ const el=$('#connectionLabel'); const pulse=$('#connectionPulse'); if(el) el.textContent=label||(ok?'Realtime connected':'REST fallback'); if(pulse) pulse.classList.toggle('degraded',!ok); }
export function setActiveNav(page){ document.querySelectorAll('.nav-link').forEach(a=>a.classList.toggle('active', a.dataset.page===page)); }
export function pageHero(kicker,title,desc,actions=''){ return `<div class="page-hero"><div><div class="page-kicker">${kicker}</div><h1 class="page-title">${title}</h1><p class="page-desc">${desc}</p></div><div style="display:flex;gap:10px;flex-wrap:wrap">${actions}</div></div>`; }
export function kpi(label,value,trend='',color=''){ return `<div class="card kpi-card"><div class="kpi-label">${label}</div><div class="kpi-value" style="${color?`color:${color}`:''}">${value}</div><div class="kpi-trend">${trend}</div></div>`; }
