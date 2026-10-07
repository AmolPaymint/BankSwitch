import { CONFIG } from './config.js';

export class ApiError extends Error {
  constructor(message,status,payload=null,correlationId=''){super(message);this.name='ApiError';this.status=status;this.payload=payload;this.correlationId=correlationId;}
}

export class ApiClient {
  constructor(baseUrl=CONFIG.apiBaseUrl,{timeoutMs=15000,retries=2}={}){this.baseUrl=baseUrl.replace(/\/$/,'');this.timeoutMs=timeoutMs;this.retries=retries;}

  async request(path,options={}){
    const url=path.startsWith('http')?path:`${this.baseUrl}${path.startsWith('/')?path:`/${path}`}`;
    const method=(options.method||'GET').toUpperCase();
    const maxAttempts=method==='GET'?this.retries+1:1;
    let lastError;
    for(let attempt=1;attempt<=maxAttempts;attempt++){
      const controller=new AbortController();
      const timeout=setTimeout(()=>controller.abort(new DOMException('Request timeout','TimeoutError')),options.timeoutMs||this.timeoutMs);
      const correlationId=crypto.randomUUID?.()||`${Date.now()}-${Math.random()}`;
      const headers={'Accept':'application/json','X-Client-Correlation-Id':correlationId,...(options.body?{'Content-Type':'application/json'}:{}),...(options.headers||{})};
      try{
        const res=await fetch(url,{credentials:'same-origin',...options,method,headers,signal:options.signal||controller.signal});
        clearTimeout(timeout);
        if(res.status===401){location.href='/Account/Login?returnUrl='+encodeURIComponent(location.pathname+location.hash);throw new ApiError('Authentication required',401,null,correlationId);}
        let payload=null;const type=res.headers.get('content-type')||'';
        if(type.includes('application/json')){try{payload=await res.json();}catch{payload=null;}}
        else if(res.status!==204)payload=await res.text();
        if(!res.ok){
          const message=payload?.message||payload?.title||`${res.status} ${res.statusText}`;
          const err=new ApiError(message,res.status,payload,res.headers.get('x-correlation-id')||correlationId);
          if(attempt<maxAttempts&&[429,502,503,504].includes(res.status)){await delay(retryDelay(res,attempt));lastError=err;continue;}
          throw err;
        }
        return payload;
      }catch(err){
        clearTimeout(timeout);lastError=err;
        if(err?.name==='AbortError'||err?.name==='TimeoutError'){lastError=new ApiError('The BankSwitch API request timed out.',408,null,correlationId);}
        if(attempt<maxAttempts&&!(lastError instanceof ApiError&&![408,429,502,503,504].includes(lastError.status))){await delay(Math.min(250*2**(attempt-1),2000));continue;}
        throw lastError;
      }
    }
    throw lastError||new ApiError('Request failed',0);
  }

  command(path,options={}){return this.request(`/command-center${path}`,options);}
  settings(path,options={}){return this.request(`/settings${path}`,options);}
  session(){return this.command('/session');}
  overview(){return this.command('/overview');}
  transactions(params={}){const q=new URLSearchParams(Object.entries(params).filter(([,v])=>v!==undefined&&v!==null&&v!==''));return this.command(`/transactions${q.size?`?${q}`:''}`);}
  alerts(take=25){return this.command(`/alerts?take=${take}`);}
  operations(){return this.command('/operations');}
  health(){return this.command('/health');}
  settingDefinitions(domain=''){return this.settings(`/definitions${domain?`?domain=${encodeURIComponent(domain)}`:''}`);}
  settingValues(environment,institutionScope='GLOBAL',domain=''){const q=new URLSearchParams({environment,institutionScope});if(domain)q.set('domain',domain);return this.settings(`/values?${q}`);}
  changeRequests(state=''){return this.settings(`/change-requests${state?`?state=${encodeURIComponent(state)}`:''}`);}
  history(environment,institutionScope='GLOBAL',domain='',take=250){const q=new URLSearchParams({environment,institutionScope,take:String(take)});if(domain)q.set('domain',domain);return this.settings(`/history?${q}`);}
}
function delay(ms){return new Promise(resolve=>setTimeout(resolve,ms));}
function retryDelay(res,attempt){const h=res.headers.get('retry-after');if(h&&/^\d+$/.test(h))return Math.min(Number(h)*1000,5000);return Math.min(250*2**(attempt-1),2000);}
export const api=new ApiClient();
