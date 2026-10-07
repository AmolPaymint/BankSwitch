import { $, toast } from './utils.js';

const KEY='bs_notifications_v1';
const MAX=100;
let items=[];
try{ items=JSON.parse(localStorage.getItem(KEY)||'[]'); if(!Array.isArray(items))items=[]; }catch{ items=[]; }

function persist(){ localStorage.setItem(KEY,JSON.stringify(items.slice(0,MAX))); updateBadge(); }
export function addNotification({title='BankSwitch event',message='',severity='info',source='Command Center',at=new Date().toISOString(),key=''}){
  if(key && items.some(x=>x.key===key)) return;
  items.unshift({id:crypto.randomUUID?.()||`${Date.now()}-${Math.random()}`,title,message,severity,source,at,key,read:false});
  items=items.slice(0,MAX); persist();
  if(severity==='critical'||severity==='high') toast(`${title}: ${message}`,'error');
}
export function unreadCount(){ return items.filter(x=>!x.read).length; }
export function updateBadge(){ const b=$('#notificationBadge'); if(b){const n=unreadCount();b.textContent=String(n);b.hidden=n===0;} }
export function wireNotificationButton(){ const b=$('#notificationBtn'); if(b)b.onclick=openDrawer; updateBadge(); }
export function ingestAlerts(payload){
  const list=Array.isArray(payload)?payload:(payload?.items||[]);
  list.slice(0,25).forEach(a=>{
    const sev=String(a.severity||a.level||'info').toLowerCase();
    const title=a.ruleName||a.name||a.title||'Operational alert';
    const msg=a.message||a.description||a.details||'BankSwitch operational event';
    addNotification({title,message:msg,severity:sev,source:'Operations',at:a.createdAt||a.occurredAt||new Date().toISOString(),key:String(a.id||`${title}-${a.createdAt||a.occurredAt||''}`)});
  });
}
function openDrawer(){
  const host=$('#notification-drawer-host');
  if(!host)return;
  host.innerHTML=`<div class="drawer-backdrop show" id="notificationBackdrop"><aside class="notification-drawer" role="dialog" aria-modal="true" aria-labelledby="notificationTitle"><div class="drawer-head"><div><div class="page-kicker">Real-time events</div><h2 id="notificationTitle" class="section-title">Notifications</h2></div><button id="closeNotifications" class="btn ghost" aria-label="Close notifications">✕</button></div><div class="drawer-actions"><button id="markAllRead" class="btn small">Mark all read</button><button id="clearNotifications" class="btn small danger">Clear</button></div><div class="notification-list">${items.length?items.map(renderItem).join(''):'<div class="empty">No notifications yet.</div>'}</div></aside></div>`;
  const backdrop=$('#notificationBackdrop');
  const trigger=$('#notificationBtn');
  const close=()=>{host.innerHTML='';trigger?.focus?.()};
  $('#closeNotifications').onclick=close;
  backdrop.onclick=e=>{if(e.target===backdrop)close();};
  $('#markAllRead').onclick=()=>{items=items.map(x=>({...x,read:true}));persist();close();setTimeout(openDrawer,0);};
  $('#clearNotifications').onclick=()=>{items=[];persist();close();setTimeout(openDrawer,0);};
  document.addEventListener('keydown',function esc(e){if(e.key==='Escape'){document.removeEventListener('keydown',esc);close();}},{once:false});
  $('#closeNotifications')?.focus();
}
function renderItem(x){
  const cls=x.read?'':' unread';
  return `<article class="notification-item${cls}"><div class="notification-severity ${escapeHtml(x.severity)}"></div><div><div class="notification-title">${escapeHtml(x.title)}</div><div class="notification-message">${escapeHtml(x.message)}</div><div class="notification-meta">${escapeHtml(x.source)} · ${new Date(x.at).toLocaleString()}</div></div></article>`;
}
function escapeHtml(v){return String(v??'').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));}
