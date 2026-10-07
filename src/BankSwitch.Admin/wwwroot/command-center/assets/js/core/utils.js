export const $ = (sel, root=document) => root.querySelector(sel);
export const $$ = (sel, root=document) => [...root.querySelectorAll(sel)];
export const html = (strings,...vals) => strings.map((s,i)=>s+(vals[i]??'')).join('');
export const money = n => new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',maximumFractionDigits:0}).format(n);
export const tag = (text, color='blue') => `<span class="tag ${color}">${text}</span>`;
export function toast(message, type='success'){
  const host = $('#toast-host'); const el = document.createElement('div'); el.className=`toast ${type}`; el.textContent=message; host.appendChild(el); setTimeout(()=>el.remove(),3200);
}
export function downloadCsv(filename, rows){
  const csv = rows.map(r=>r.map(v=>`"${String(v).replaceAll('"','""')}"`).join(',')).join('\n');
  const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([csv],{type:'text/csv'})); a.download = filename; a.click(); URL.revokeObjectURL(a.href);
}
export function mountTable(headers, rows){
  return `<div class="table-wrap"><table class="table"><thead><tr>${headers.map(h=>`<th>${h}</th>`).join('')}</tr></thead><tbody>${rows.map(r=>`<tr>${r.map(c=>`<td>${c}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`;
}
