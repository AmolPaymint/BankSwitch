import { $ } from '../core/utils.js';

export function openModal(title, body){
  const previous=document.activeElement;
  let host=$('#modalHost');
  if(!host){host=document.createElement('div');host.id='modalHost';host.className='modal-backdrop';document.body.appendChild(host);}
  host.innerHTML=`<div class="modal" role="dialog" aria-modal="true" aria-labelledby="modalTitle"><div class="modal-head"><strong id="modalTitle">${escapeHtml(title)}</strong><button class="btn small ghost" id="closeModal" aria-label="Close dialog">✕</button></div><div class="modal-body">${body}</div></div>`;
  host.classList.add('show');
  const close=()=>{host.classList.remove('show');host.innerHTML='';previous?.focus?.();document.removeEventListener('keydown',onKey);};
  const onKey=e=>{if(e.key==='Escape')close();if(e.key==='Tab')trapFocus(e,host);};
  $('#closeModal').onclick=close;
  host.onclick=e=>{if(e.target===host)close();};
  document.addEventListener('keydown',onKey);
  $('#closeModal')?.focus();
  return close;
}
function trapFocus(e,root){const nodes=[...root.querySelectorAll('button,[href],input,select,textarea,[tabindex]:not([tabindex="-1"])')].filter(x=>!x.disabled);if(!nodes.length)return;const first=nodes[0],last=nodes[nodes.length-1];if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}}
function escapeHtml(v){return String(v??'').replace(/[&<>'"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));}
