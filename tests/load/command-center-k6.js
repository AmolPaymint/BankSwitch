import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

export const options = {
  scenarios: {
    command_center: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10 },
        { duration: '2m', target: 25 },
        { duration: '30s', target: 0 }
      ],
      gracefulRampDown: '10s'
    }
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<750','p(99)<1500']
  }
};

const latency = new Trend('bankswitch_command_center_latency', true);
const failures = new Rate('bankswitch_command_center_failures');
const base = __ENV.BANKSWITCH_BASE_URL || 'https://localhost:5001';
const cookie = __ENV.BANKSWITCH_SESSION_COOKIE || '';

export default function(){
  const params={headers:{Accept:'application/json',...(cookie?{Cookie:cookie}:{})},timeout:'10s'};
  for(const path of ['/api/command-center/health','/api/command-center/overview','/api/command-center/transactions?page=1&pageSize=25']){
    const r=http.get(`${base}${path}`,params);
    latency.add(r.timings.duration);
    const ok=check(r,{[`${path} status 200`]:x=>x.status===200});
    failures.add(!ok);
  }
  sleep(1);
}
