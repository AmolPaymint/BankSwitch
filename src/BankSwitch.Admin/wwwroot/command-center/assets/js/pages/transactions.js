import { api } from '../core/api.js';
import { pageHero,kpi } from '../components/layout.js';
import { mountTable,tag,downloadCsv } from '../core/utils.js';

export async function render(root){
  const saved=localStorage.getItem('bs_global_search')||''; localStorage.removeItem('bs_global_search');
  let report=await api.transactions({pageSize:100});
  const draw=()=>{
    const tx=report.items||[];
    root.innerHTML=pageHero('Transaction Search','ISO8583 Transaction Explorer','Live transaction reporting from the v44 backend. PAN values are masked; searches operate on approved reporting fields.',`<button id="exportCsv" class="btn primary">Export CSV</button>`)
      +`<div class="grid cols-4">${kpi('Volume',Number(report.totalCount||0).toLocaleString(),'current filter')}${kpi('Approved',Number(report.approvedCount||0).toLocaleString(),report.totalCount?`${(report.approvedCount/report.totalCount*100).toFixed(1)}%`: '0%')}${kpi('Declined',Number(report.declinedCount||0).toLocaleString(),'current filter','var(--yellow)')}${kpi('Avg Latency',`${Number(report.averageLatencyMilliseconds||0).toFixed(0)} ms`,'report aggregate')}</div>
      <div class="divider"></div><div class="card pad"><div class="form-grid"><input id="searchText" class="input" placeholder="RRN / STAN / masked PAN / token" value="${saved}"><select id="mti" class="select"><option value="">All MTIs</option><option>0100</option><option>0200</option><option>0220</option><option>0400</option><option>0420</option></select><select id="response" class="select"><option value="">All Response Codes</option><option value="00">00 Approved</option><option value="51">51 Insufficient Funds</option><option value="91">91 Issuer Unavailable</option><option value="94">94 Duplicate</option></select><button id="searchBtn" class="btn primary">Search</button></div></div>
      <div class="divider"></div><div class="card pad">${mountTable(['Time','RRN','STAN','MTI','PAN','Amount','Scheme','Route','Response','Latency','State'],tx.map(t=>[new Date(t.time).toLocaleString(),t.rrn,t.stan,t.mti,t.pan,`${t.currency||''} ${Number(t.amount||0).toLocaleString()}`,t.scheme,t.route,t.response,`${t.latencyMs} ms`,tag(t.state,t.response==='00'?'green':'yellow')]))}</div>`;
    document.getElementById('exportCsv').onclick=()=>downloadCsv('bankswitch-transactions.csv',[['Time','RRN','STAN','MTI','PAN','Amount','Currency','Scheme','Route','Response','LatencyMs','State'],...tx.map(t=>[t.time,t.rrn,t.stan,t.mti,t.pan,t.amount,t.currency,t.scheme,t.route,t.response,t.latencyMs,t.state])]);
    document.getElementById('searchBtn').onclick=async()=>{
      report=await api.transactions({mti:document.getElementById('mti').value,responseCode:document.getElementById('response').value,pageSize:250});
      const q=document.getElementById('searchText').value.trim().toLowerCase();
      if(q){ report={...report,items:(report.items||[]).filter(t=>[t.rrn,t.stan,t.pan,t.panToken,t.correlationId].some(v=>String(v||'').toLowerCase().includes(q)))}; }
      draw();
    };
  };
  draw();
}
