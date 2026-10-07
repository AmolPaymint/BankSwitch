export function drawLineChart(canvas, data, color){
  if(!canvas) return;
  const values=(Array.isArray(data)?data:[]).map(Number).filter(Number.isFinite);
  const ctx=canvas.getContext('2d');
  const dpr=Math.max(1,window.devicePixelRatio||1);
  const cssW=Math.max(240,canvas.clientWidth||600), cssH=Math.max(140,canvas.clientHeight||210);
  const w=canvas.width=Math.round(cssW*dpr), h=canvas.height=Math.round(cssH*dpr);
  ctx.clearRect(0,0,w,h);
  const pad=24*dpr;
  ctx.strokeStyle='rgba(136,152,200,.25)';ctx.lineWidth=1*dpr;
  for(let i=0;i<4;i++){const y=pad+(h-pad*2)*i/3;ctx.beginPath();ctx.moveTo(pad,y);ctx.lineTo(w-pad,y);ctx.stroke();}
  if(!values.length){drawEmpty(ctx,w,h,dpr,'Awaiting live samples');return;}
  const max=Math.max(...values),min=Math.min(...values);
  const range=max-min||Math.max(1,Math.abs(max)*.1);
  ctx.strokeStyle=color||'#4d8fff';ctx.lineWidth=3*dpr;ctx.lineJoin='round';ctx.lineCap='round';ctx.beginPath();
  values.forEach((v,i)=>{const x=values.length===1?w/2:pad+(w-pad*2)*i/(values.length-1);const y=h-pad-((v-min)/range)*(h-pad*2);if(i)ctx.lineTo(x,y);else ctx.moveTo(x,y);});ctx.stroke();
  if(values.length>1){const grad=ctx.createLinearGradient(0,pad,0,h-pad);grad.addColorStop(0,(color||'#4d8fff')+'55');grad.addColorStop(1,(color||'#4d8fff')+'00');ctx.lineTo(w-pad,h-pad);ctx.lineTo(pad,h-pad);ctx.fillStyle=grad;ctx.fill();}
  else{ctx.beginPath();ctx.arc(w/2,h/2,5*dpr,0,Math.PI*2);ctx.fillStyle=color||'#4d8fff';ctx.fill();}
}
export function drawDonut(canvas, values, colors){
  if(!canvas)return;
  const vals=(Array.isArray(values)?values:[]).map(v=>Math.max(0,Number(v)||0));
  const ctx=canvas.getContext('2d');const dpr=Math.max(1,window.devicePixelRatio||1);
  const w=canvas.width=Math.max(240,canvas.clientWidth||400)*dpr;const h=canvas.height=Math.max(140,canvas.clientHeight||210)*dpr;
  const r=Math.min(w,h)/2-20*dpr,cx=w/2,cy=h/2,total=vals.reduce((a,b)=>a+b,0);let start=-Math.PI/2;ctx.clearRect(0,0,w,h);
  if(total<=0){ctx.beginPath();ctx.arc(cx,cy,r,0,Math.PI*2);ctx.lineWidth=18*dpr;ctx.strokeStyle='rgba(136,152,200,.2)';ctx.stroke();drawEmpty(ctx,w,h,dpr,'No transactions');return;}
  vals.forEach((v,i)=>{const a=v/total*Math.PI*2;ctx.beginPath();ctx.arc(cx,cy,r,start,start+a);ctx.lineWidth=18*dpr;ctx.strokeStyle=colors[i]||'#4d8fff';ctx.stroke();start+=a;});
  ctx.fillStyle=getComputedStyle(document.documentElement).getPropertyValue('--text0').trim()||'#fff';ctx.font=`700 ${28*dpr}px Segoe UI`;ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(`${Math.round(vals[0]/total*100)}%`,cx,cy);
}
function drawEmpty(ctx,w,h,dpr,text){ctx.fillStyle='rgba(136,152,200,.75)';ctx.font=`${12*dpr}px Segoe UI`;ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(text,w/2,h/2);}
