window.ApiEndpoints = Object.freeze({
  health: '/api/health',
  portfolio: '/api/v1/portfolio',
  settings: '/api/settings',
  assets: '/api/assets',
  asset: (id) => `/api/assets/${encodeURIComponent(id)}`,
  refreshQuotes: '/api/quotes/refresh'
});
