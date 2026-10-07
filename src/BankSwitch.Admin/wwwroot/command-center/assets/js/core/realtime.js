import { CONFIG } from './config.js';

const RS = '\u001e';

export function parseSignalRFrames(buffer){
  const parts = buffer.split(RS);
  const remainder = parts.pop() || '';
  const messages = [];
  for(const raw of parts){
    if(!raw.trim()) continue;
    try{ messages.push(JSON.parse(raw)); }catch{}
  }
  return { messages, remainder };
}

function hubUrlFromToken(path, token){
  const base = new URL(path, window.location.origin);
  base.searchParams.set('id', token);
  base.protocol = base.protocol === 'https:' ? 'wss:' : 'ws:';
  return base.toString();
}

export class RealtimeClient extends EventTarget {
  constructor(path = CONFIG.realtimeHub){
    super();
    this.path = path;
    this.socket = null;
    this.buffer = '';
    this.stopped = false;
    this.attempt = 0;
    this.timer = null;
    this.connectionId = null;
  }

  async start(){
    this.stopped = false;
    await this.#connect();
  }

  stop(){
    this.stopped = true;
    clearTimeout(this.timer);
    if(this.socket && this.socket.readyState < 2) this.socket.close(1000,'client stop');
    this.socket = null;
  }

  async #connect(){
    if(this.stopped) return;
    this.#state('connecting');
    try{
      const negotiateUrl = `${this.path.replace(/\/$/,'')}/negotiate?negotiateVersion=1`;
      const res = await fetch(negotiateUrl,{method:'POST',credentials:'same-origin',headers:{'Accept':'application/json'}});
      if(res.status===401){ location.href='/Account/Login?returnUrl='+encodeURIComponent(location.pathname+location.hash); return; }
      if(!res.ok) throw new Error(`SignalR negotiate failed (${res.status})`);
      const n = await res.json();
      const token = n.connectionToken || n.connectionId;
      if(!token) throw new Error('SignalR negotiation did not return a connection token');
      this.connectionId = n.connectionId || token;
      await this.#openSocket(hubUrlFromToken(this.path, token));
    }catch(err){
      this.dispatchEvent(new CustomEvent('error',{detail:err}));
      this.#state('degraded');
      this.#scheduleReconnect();
    }
  }

  #openSocket(url){
    return new Promise((resolve,reject)=>{
      const ws = new WebSocket(url);
      let handshaken = false;
      const timeout = setTimeout(()=>{ if(!handshaken){ try{ws.close();}catch{} reject(new Error('SignalR handshake timeout')); } },8000);
      this.socket = ws;
      ws.onopen = ()=>ws.send(JSON.stringify({protocol:'json',version:1})+RS);
      ws.onmessage = evt=>{
        this.buffer += String(evt.data || '');
        const parsed = parseSignalRFrames(this.buffer);
        this.buffer = parsed.remainder;
        for(const msg of parsed.messages){
          if(!handshaken && msg.error===undefined && msg.type===undefined){
            handshaken=true; clearTimeout(timeout); this.attempt=0; this.#state('connected'); resolve(); continue;
          }
          this.#handle(msg);
        }
      };
      ws.onerror = ()=>{ if(!handshaken){ clearTimeout(timeout); reject(new Error('SignalR WebSocket error')); } };
      ws.onclose = ()=>{ clearTimeout(timeout); this.socket=null; if(!this.stopped){ this.#state('degraded'); this.#scheduleReconnect(); } };
    });
  }

  #handle(msg){
    if(msg.type===1 && msg.target){
      const value = Array.isArray(msg.arguments) ? msg.arguments[0] : undefined;
      this.dispatchEvent(new CustomEvent(msg.target,{detail:value}));
      window.dispatchEvent(new CustomEvent(`bankswitch:realtime:${msg.target}`,{detail:value}));
    }else if(msg.type===6){
      // SignalR ping frame.
    }else if(msg.type===7){
      this.dispatchEvent(new CustomEvent('error',{detail:new Error(msg.error || 'SignalR connection closed by server')}));
    }
  }

  #scheduleReconnect(){
    if(this.stopped || this.timer) return;
    const delays=[1000,2000,5000,10000,15000,30000];
    const delay=delays[Math.min(this.attempt++,delays.length-1)];
    this.timer=setTimeout(()=>{this.timer=null;this.#connect();},delay);
  }

  #state(state){ this.dispatchEvent(new CustomEvent('state',{detail:{state,connectionId:this.connectionId}})); }
}
