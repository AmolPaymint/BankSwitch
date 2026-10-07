import { api } from '../core/api.js';
import { pageHero, kpi } from '../components/layout.js';
import { toast } from '../core/utils.js';

const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const fmt=d=>d?new Date(d).toLocaleString():'—';

export async function render(root){
  const [packages,deployments]=await Promise.all([api.request('/atm-lod/packages'),api.request('/atm-lod/deployments')]);
  const approved=packages.filter(x=>x.status==='Approved').length;
  const applied=deployments.filter(x=>x.status==='Applied').length;
  root.innerHTML=`${pageHero('ATM Configuration','NCR NDC LOD Manager','Parse, approve, version and deploy Advanced NDC/NDC+ LOD packages to managed ATMs.','<button class="btn primary" id="newLod">Upload LOD</button>')}
  <div class="kpi-grid">${kpi('Packages',packages.length,'persistent repository')}${kpi('Approved',approved,'maker-checker complete')}${kpi('Deployments',deployments.length,'all terminals')}${kpi('Applied',applied,'acknowledged & active')}</div>
  <div class="card pad"><div class="section-header"><div><h2 class="section-title">LOD Packages</h2><p class="subtle">Raw packages are checksum-bound; parsed metadata is stored separately for audit.</p></div></div>
    <div class="table-wrap"><table class="table"><thead><tr><th>Name</th><th>Version</th><th>File</th><th>Size</th><th>Status</th><th>SHA-256</th><th>Uploaded</th><th>Actions</th></tr></thead><tbody>${packages.map(p=>`<tr><td>${esc(p.name)}</td><td>${esc(p.version)}</td><td>${esc(p.fileName)}</td><td>${p.sizeBytes}</td><td><span class="badge">${esc(p.status)}</span></td><td><code>${esc(p.sha256?.slice(0,16))}…</code></td><td>${fmt(p.uploadedAt)}</td><td>${actions(p)}</td></tr>`).join('')||'<tr><td colspan="8" class="empty">No LOD packages uploaded.</td></tr>'}</tbody></table></div>
  </div>
  <div class="card pad" style="margin-top:16px"><div class="section-header"><div><h2 class="section-title">ATM Deployments</h2><p class="subtle">Deployment is block-based and activation is allowed only after all blocks are acknowledged.</p></div></div>
    <div class="table-wrap"><table class="table"><thead><tr><th>Terminal</th><th>Protocol</th><th>Status</th><th>Blocks</th><th>Created</th><th>Actions</th></tr></thead><tbody>${deployments.map(d=>`<tr><td>${esc(d.terminalId)}</td><td>${esc(d.protocol)}</td><td>${esc(d.status)}</td><td>${d.acknowledgedBlocks}/${d.totalBlocks}</td><td>${fmt(d.createdAt)}</td><td>${deploymentActions(d)}</td></tr>`).join('')||'<tr><td colspan="6" class="empty">No deployments scheduled.</td></tr>'}</tbody></table></div>
  </div>`;
  root.querySelector('#newLod')?.addEventListener('click',()=>showUpload(root));
  root.querySelectorAll('[data-action]').forEach(b=>b.addEventListener('click',()=>handleAction(b)));
  root.querySelectorAll('[data-deploy]').forEach(b=>b.addEventListener('click',()=>showDeploy(b.dataset.deploy)));
  root.querySelectorAll('[data-daction]').forEach(b=>b.addEventListener('click',()=>handleDeploymentAction(b)));
}

function actions(p){
  const a=[];
  if(p.status==='Validated') a.push(`<button class="btn small" data-action="submit" data-id="${p.id}">Submit</button>`);
  if(p.status==='PendingApproval') a.push(`<button class="btn small" data-action="approve" data-id="${p.id}">Approve</button>`);
  if(p.status==='Approved') a.push(`<button class="btn small primary" data-deploy="${p.id}">Deploy</button>`);
  return a.join(' ')||'—';
}
function deploymentActions(d){
  const a=[];
  if(d.status==='Scheduled') a.push(`<button class="btn small" data-daction="frames" data-id="${d.id}">Generate Frames</button>`);
  if(['Ready','Transferring','AwaitingAcknowledgement'].includes(d.status)&&d.totalBlocks>0&&d.acknowledgedBlocks<d.totalBlocks) a.push(`<button class="btn small" data-daction="ackall" data-id="${d.id}" data-total="${d.totalBlocks}">ACK All</button>`);
  if(d.totalBlocks>0&&d.acknowledgedBlocks>=d.totalBlocks&&d.status!=='Applied') a.push(`<button class="btn small primary" data-daction="activate" data-id="${d.id}">Activate</button>`);
  if(d.status==='Applied') a.push(`<button class="btn small" data-daction="rollback" data-id="${d.id}">Rollback</button>`);
  return a.join(' ')||'—';
}
function overlay(html){const d=document.createElement('div');d.className='modal-backdrop';d.innerHTML=`<div class="modal card pad" role="dialog" aria-modal="true">${html}</div>`;document.body.appendChild(d);d.addEventListener('click',e=>{if(e.target===d)d.remove();});return d;}
function showUpload(){const d=overlay(`<h2 class="section-title">Upload NCR NDC LOD</h2><form id="lodForm"><div class="form-grid"><label>Name<input class="input" name="name" required value="ATM Production Load"></label><label>Version<input class="input" name="version" required></label><label>Vendor<input class="input" name="vendor" value="NCR"></label><label>ATM Model<input class="input" name="atmModel" value="APTRA/NDC"></label></div><label>LOD file<input class="input" type="file" name="file" accept=".lod" required></label><div style="margin-top:16px"><button class="btn primary">Parse & Validate</button></div></form>`);d.querySelector('#lodForm').addEventListener('submit',async e=>{e.preventDefault();const f=e.currentTarget;const file=f.file.files[0];const contentBase64=await toBase64(file);await api.request('/atm-lod/packages',{method:'POST',body:JSON.stringify({name:f.name.value,version:f.version.value,fileName:file.name,contentBase64,vendor:f.vendor.value,atmModel:f.atmModel.value})});toast('LOD parsed and validated');d.remove();window.dispatchEvent(new CustomEvent('bankswitch:sync'));});}
function showDeploy(packageId){const d=overlay(`<h2 class="section-title">Schedule ATM Deployment</h2><form id="deployForm"><label>Terminal ID<input class="input" name="terminal" required></label><label>Protocol<select class="input" name="protocol"><option value="NdcPlus">NDC+</option><option value="Ndc">NDC</option></select></label><label>Block Size<input class="input" type="number" min="128" max="4096" name="block" value="1024"></label><div style="margin-top:16px"><button class="btn primary">Schedule</button></div></form>`);d.querySelector('#deployForm').addEventListener('submit',async e=>{e.preventDefault();const f=e.currentTarget;await api.request('/atm-lod/deployments',{method:'POST',body:JSON.stringify({packageId,terminalId:f.terminal.value,protocol:f.protocol.value,blockSize:Number(f.block.value),correlationId:crypto.randomUUID()})});toast('LOD deployment scheduled');d.remove();window.dispatchEvent(new CustomEvent('bankswitch:sync'));});}
async function handleAction(b){await api.request(`/atm-lod/packages/${b.dataset.id}/${b.dataset.action}`,{method:'POST'});toast(`LOD ${b.dataset.action} completed`);window.dispatchEvent(new CustomEvent('bankswitch:sync'));}
async function handleDeploymentAction(b){const id=b.dataset.id;const a=b.dataset.daction;if(a==='frames')await api.request(`/atm-lod/deployments/${id}/frames`,{method:'POST'});else if(a==='ackall'){for(let i=1;i<=Number(b.dataset.total);i++)await api.request(`/atm-lod/deployments/${id}/ack/${i}`,{method:'POST'});}else await api.request(`/atm-lod/deployments/${id}/${a}`,{method:'POST'});toast(`Deployment ${a} completed`);window.dispatchEvent(new CustomEvent('bankswitch:sync'));}
function toBase64(file){return new Promise((resolve,reject)=>{const r=new FileReader();r.onerror=reject;r.onload=()=>resolve(String(r.result).split(',')[1]);r.readAsDataURL(file);});}
