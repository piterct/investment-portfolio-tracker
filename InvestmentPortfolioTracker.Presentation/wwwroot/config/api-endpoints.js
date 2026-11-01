window.ApiEndpoints = Object.freeze({
  health: '/api/health',
  portfolio: '/api/portfolio',
  settings: '/api/settings',
  assets: '/api/assets',
  asset: (id) => `/api/assets/${encodeURIComponent(id)}`,
  refreshQuotes: '/api/quotes/refresh'
});
