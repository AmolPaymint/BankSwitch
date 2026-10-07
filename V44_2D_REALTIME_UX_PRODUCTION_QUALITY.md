# BankSwitch v44.2D — Real-Time, UX & Production Quality

## Objective

v44.2D completes the final frontend quality gate for the v44.2 Enterprise Command Center. It builds on v44.2A security/foundation, v44.2B operational module integration and v44.2C enterprise administration/maker-checker.

## Implemented

### 1. Authenticated real-time channel
- ASP.NET Core SignalR hub at `/hubs/command-center`.
- Hub requires the existing `Viewer` authorization policy.
- Role-specific SignalR groups segment operational/security/risk event delivery.
- `CommandCenterRealtimeBroadcaster` publishes live overview data and operational events on a bounded interval.
- Realtime interval is configurable through `CommandCenter:RealtimeIntervalSeconds`.
- Client reconnect uses bounded exponential backoff.
- REST API remains the fallback when SignalR/WebSockets are unavailable.

### 2. Production frontend SignalR protocol client
The Command Center uses a self-hosted ES6 SignalR protocol client rather than an external CDN dependency. It performs:
1. authenticated SignalR negotiation;
2. WebSocket upgrade using the negotiated token;
3. JSON protocol handshake;
4. record-separator frame parsing;
5. invocation dispatch;
6. ping handling;
7. automatic reconnect.

No external JavaScript CDN is required.

### 3. Real operational charts
The dashboard no longer synthesizes/randomizes TPS samples. Live TPS samples are accumulated only from backend monitoring events. Empty and single-sample chart states are handled explicitly.

### 4. Central notification center
- persistent browser notification inbox;
- realtime operational alert ingestion;
- severity indicators;
- unread count;
- mark-all-read / clear controls;
- critical/high toast escalation;
- bounded local history.

### 5. Accessibility and keyboard support
- skip-to-content link;
- ARIA labels and live regions;
- accessible realtime connection state;
- focus-visible states;
- modal focus trap and Escape handling;
- keyboard `/` shortcut for global search;
- mobile navigation Escape handling;
- reduced-motion support;
- forced-colors support;
- improved responsive behavior.

### 6. API resilience
The ES6 API client now provides:
- request timeouts;
- safe GET retries for HTTP 408/429/502/503/504 conditions;
- bounded exponential backoff;
- Retry-After support;
- client correlation IDs;
- same-origin credential enforcement;
- authentication redirect on HTTP 401.

Mutating requests are not automatically retried.

### 7. CSP/security hardening
The previous inline Command Center bootstrap script was removed because `script-src 'self'` disallows inline JavaScript. Runtime bootstrap values now come from server-rendered `data-*` attributes. CSP permits authenticated same-origin WebSocket connectivity through `connect-src 'self' ws: wss:` while continuing to deny third-party scripts.

### 8. Production smoke / verification assets
- `scripts/verify-v44.2d.sh`
- `scripts/smoke-v44.2d.sh`
- Node ES module syntax validation
- Node realtime frame contract tests
- xUnit hub authorization/background-service contract tests

## Realtime event contract

| Event | Audience | Payload |
|---|---|---|
| `overview` | All authenticated Viewer users | metrics, application health, device health, operations dashboard |
| `operations` | Operations role group | dashboard, health, incidents |
| `alerts` | Operations role group | latest operational alerts |
| `heartbeat` | Calling client | server time + SignalR connection ID |

## Production behavior

If realtime connectivity is degraded, the UI changes status to `REST fallback`. The dashboard continues to load through authenticated REST APIs and periodically refreshes only while the page is visible.

## Quality gate

Run:

```bash
./scripts/verify-v44.2d.sh
```

After deploying the Admin service:

```bash
BASE_URL=https://bankswitch-admin.example.com ./scripts/smoke-v44.2d.sh
```

For authenticated endpoint checks, provide an authenticated cookie through `BANKSWITCH_AUTH_COOKIE` in a secure CI secret.

## Remaining external production evidence

v44.2D completes the frontend implementation milestone, but regulated production deployment still depends on environment-specific evidence: browser compatibility/UAT, accessibility review, penetration testing, load testing, real identity-provider integration, scheme/HSM/device certification, DR drills and external PCI/RBI/ISO compliance sign-off.
