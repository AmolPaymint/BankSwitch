import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = process.cwd();
const endpointDir = path.join(root,'src','BankSwitch.Admin','Endpoints');
const apiFile = path.join(root,'src','BankSwitch.Admin','wwwroot','command-center','assets','js','core','api.js');

function read(p){ return fs.readFileSync(p,'utf8'); }
function fail(msg){ console.error(`FAIL: ${msg}`); process.exitCode=1; }
function pass(msg){ console.log(`PASS: ${msg}`); }

const required = [
  ['GET','/api/command-center/session'],['GET','/api/command-center/health'],['GET','/api/command-center/overview'],
  ['GET','/api/command-center/transactions'],['GET','/api/command-center/alerts'],['GET','/api/command-center/operations'],
  ['GET','/api/command-center/routing'],['GET','/api/settings/domains'],['GET','/api/settings/definitions'],['GET','/api/settings/completeness'],
  ['GET','/api/settings/values'],['POST','/api/settings/validate'],['POST','/api/settings/change-requests'],
  ['GET','/api/settings/change-requests'],['GET','/api/settings/history'],['POST','/api/settings/snapshots'],
  ['GET','/api/settings/snapshots'],['GET','/api/settings/diagnostics'],['GET','/api/settings/feature-flags'],
  ['GET','/api/settings/certificates'],['GET','/api/settings/secrets']
];

const files = fs.readdirSync(endpointDir).filter(x=>x.endsWith('.cs'));
const discovered=[];
for(const f of files){
  const lines=read(path.join(endpointDir,f)).split(/\r?\n/);
  const groups=new Map();
  for(let i=0;i<lines.length;i++){
    const line=lines[i];
    const gm=line.match(/(?:var|const)\s+(\w+)\s*=\s*app\.MapGroup\("([^"]+)"\)/);
    if(gm){ groups.set(gm[1],gm[2]); continue; }
    const rm=line.match(/(\w+)\.Map(Get|Post|Put|Delete|Patch)\("([^"]+)"/);
    if(rm && groups.has(rm[1])) discovered.push([rm[2].toUpperCase(),`${groups.get(rm[1])}${rm[3]}`,`${f}:${i+1}`]);
  }
}

const keys=new Map();
for(const [verb,route,where] of discovered){
  const key=`${verb} ${route}`;
  if(!keys.has(key)) keys.set(key,[]);
  keys.get(key).push(where);
}
for(const [key,wheres] of keys){ if(wheres.length>1) fail(`duplicate endpoint contract ${key}: ${wheres.join(', ')}`); }
if(!process.exitCode) pass(`no duplicate Minimal API method/path contracts across ${files.length} endpoint files`);

for(const [verb,route] of required){
  const exact=`${verb} ${route}`;
  if(keys.has(exact)) pass(`endpoint exists: ${exact}`); else fail(`required endpoint missing: ${exact}`);
}

const api=read(apiFile);
const frontendChecks=[
  ['session()',"this.command('/session')"],['overview()',"this.command('/overview')"],['transactions()',"this.command(`/transactions"],
  ['alerts()',"this.command(`/alerts"],['operations()',"this.command('/operations')"],['health()',"this.command('/health')"],
  ['settings definitions',"this.settings(`/definitions"],['settings values',"this.settings(`/values"],
  ['change requests',"this.settings(`/change-requests"],['history',"this.settings(`/history"]
];
for(const [name,needle] of frontendChecks){ api.includes(needle)?pass(`frontend mapping ${name}`):fail(`frontend API mapping missing: ${name}`); }
if(!process.exitCode) console.log(`Contract verification complete: ${discovered.length} API routes discovered.`);
