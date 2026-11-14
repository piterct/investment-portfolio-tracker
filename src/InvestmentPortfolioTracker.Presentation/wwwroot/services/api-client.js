async function request(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers || {})
    }
  });

  if (response.status === 204) return null;

  const contentType = response.headers.get('content-type') || '';
  const data = contentType.includes('application/json')
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    throw new Error(data?.error || `Falha na solicitação (${response.status}).`);
  }

  return data;
}

window.ApiClient = Object.freeze({
  getPortfolio: (pageNumber = 1, pageSize = 10, searchTerm = '') => {
    const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) });
    if (searchTerm) query.set('search', searchTerm);
    return request(`${ApiEndpoints.portfolio}?${query}`);
  },
  getSettings: () => request(ApiEndpoints.settings),
  saveSettings: (settings) => request(ApiEndpoints.settings, {
    method: 'PUT',
    body: JSON.stringify(settings)
  }),
  createAsset: (asset) => request(ApiEndpoints.assets, {
    method: 'POST',
    body: JSON.stringify(asset)
  }),
  updateAsset: (id, asset) => request(ApiEndpoints.asset(id), {
    method: 'PUT',
    body: JSON.stringify(asset)
  }),
  deleteAsset: (id) => request(ApiEndpoints.asset(id), {
    method: 'DELETE'
  }),
  refreshQuotes: () => request(ApiEndpoints.refreshQuotes, {
    method: 'POST',
    body: '{}'
  })
});
