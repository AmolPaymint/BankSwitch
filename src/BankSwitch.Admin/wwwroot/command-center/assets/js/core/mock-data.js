const now = () => new Date().toISOString();
export const mock = {
  health: [
    { name:'Switch Engine', status:'Online', uptime:'99.982%', latency:'18 ms' },
    { name:'ISO8583 Gateway', status:'Online', uptime:'99.991%', latency:'7 ms' },
    { name:'HSM Boundary', status:'Degraded', uptime:'99.750%', latency:'92 ms' },
    { name:'Visa Host', status:'Boundary', uptime:'N/A', latency:'Sim' },
    { name:'CBS Adapter', status:'Boundary', uptime:'N/A', latency:'Sim' },
    { name:'POS Queue', status:'Online', uptime:'99.930%', latency:'25 ms' }
  ],
  transactions: Array.from({length:42},(_,i)=>({
    rrn:`5032${String(900000+i).padStart(6,'0')}`, stan:String(100000+i), mti:i%7===0?'0400':i%3===0?'0100':'0200', pan:`5289********${String(2300+i).slice(-4)}`,
    amount:(Math.random()*9000+100).toFixed(2), currency:'INR', channel:['ATM','POS','ECOM','MPOS'][i%4], response:['00','00','00','51','91','94'][i%6], route:['VISA','MASTERCARD','RUPAY','NPCI'][i%4], time:new Date(Date.now()-i*82000).toLocaleString(), status:['Approved','Approved','Approved','Declined','Timeout','Duplicate'][i%6]
  })),
  routingRules: [
    ['R-IN-DOM-RUPAY','Country=IN + Currency=INR + Network=RuPay','RuPay/NPCI','Active','1'],
    ['R-MCC-FUEL','MCC=5541/5542 + Product=Debit','Mastercard MIP','Active','2'],
    ['R-CARD-RANGE-VISA','BIN 400000-499999','Visa Base I','Active','3'],
    ['R-DEVICE-ATM','Device=ATM + Institution=IOB','NFS/NPCI','Active','1'],
    ['R-INTERCHANGE-LOW','Interchange=LOW + Currency=INR','Domestic Sink','Draft','7']
  ],
  alerts: [
    ['High HSM latency detected','warning','2 min ago'], ['POS settlement batch generated','success','11 min ago'], ['Visa adapter running in boundary mode','warning','18 min ago'], ['ODR case SLA nearing breach','danger','22 min ago'], ['DR drill evidence uploaded','success','41 min ago']
  ],
  merchants: Array.from({length:20},(_,i)=>({ id:`M${10000+i}`, name:['Shree Stores','City Fuel','Metro Retail','QuickMart','Hotel Orchid'][i%5]+' '+(i+1), terminals:1+(i%7), status:i%6?'Active':'Review', mdr:['0.35%','0.65%','1.10%'][i%3], settlement:['T+0','T+1','T+2'][i%3]})),
  atms: Array.from({length:18},(_,i)=>({ id:`ATM${String(7000+i)}`, location:['Pune','Mumbai','Nashik','Nagpur','Thane','Chennai'][i%6], vendor:['NCR','Diebold','Wincor'][i%3], cash:(Math.random()*1200000+200000).toFixed(0), status:['Online','Online','Low Cash','Down','EJ Pending'][i%5], cassettes:`${2+i%3}/4` })),
  evidence: Array.from({length:18},(_,i)=>({ control:['PCI DSS 3.4','RBI DPS','ISO 27001 A.8','PCI PIN','NPCI Audit','VAPT'][i%6], title:['PAN masking evidence','Maker-checker approval','Key ceremony logs','DR drill record','Access review','Settlement hash proof'][i%6], owner:['Security','Operations','Compliance','Finance'][i%4], status:['Uploaded','Pending Review','Approved','Expired'][i%4], updated: now() }))
};
