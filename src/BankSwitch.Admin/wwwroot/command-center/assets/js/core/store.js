export const store = {
  state: {
    page:'dashboard',
    theme: localStorage.getItem('bs_theme') || 'dark',
    session: null,
    connected: false,
    lastSync: null,
    realtimeState:'disconnected',
    realtimeOverview:null,
    realtimeOperations:null,
    realtimeAlerts:[],
    tpsSamples:[],
    refreshMs:Number(localStorage.getItem('bs_refresh_ms') || 10000)
  },
  listeners: new Set(),
  set(patch){ this.state = { ...this.state, ...patch }; this.listeners.forEach(fn=>fn(this.state)); },
  subscribe(fn){ this.listeners.add(fn); return () => this.listeners.delete(fn); }
};
