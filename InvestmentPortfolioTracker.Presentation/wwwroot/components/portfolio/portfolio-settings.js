class PortfolioSettings extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
  }

  connectedCallback() {
    this.ready ??= this.initialize();
  }

  async initialize() {
    const templateResponse = await fetch('/components/portfolio/portfolio-settings.html');
    if (!templateResponse.ok) {
      throw new Error('Não foi possível carregar as configurações da carteira.');
    }

    const template = await templateResponse.text();
    this.shadowRoot.innerHTML = `
      <link rel="stylesheet" href="/components/portfolio/portfolio-settings.css">
      ${template}`;

    this.form.addEventListener('submit', (event) => this.handleSubmit(event));
    this.clearToken.addEventListener('change', () => {
      if (this.clearToken.checked) this.token.value = '';
    });
  }

  get form() { return this.shadowRoot.querySelector('form'); }
  get patrimony() { return this.shadowRoot.querySelector('#patrimony'); }
  get token() { return this.shadowRoot.querySelector('#token'); }
  get clearToken() { return this.shadowRoot.querySelector('[type="checkbox"]'); }
  get submitButton() { return this.shadowRoot.querySelector('button'); }
  get tokenStatus() { return this.shadowRoot.querySelector('.token-status'); }

  setSettings(settings) {
    this.patrimony.value = Number(settings.patrimony || 0).toFixed(2);
    this.token.value = '';
    this.clearToken.checked = false;
    this.tokenStatus.textContent = settings.hasApiToken
      ? 'Token salvo localmente. Deixe o campo vazio para mantê-lo.'
      : 'Sem token configurado. Alguns tickers exigem chave de API.';
  }

  setSaving(isSaving) {
    this.submitButton.disabled = isSaving;
    this.submitButton.textContent = isSaving ? 'Salvando…' : 'Salvar';
  }

  handleSubmit(event) {
    event.preventDefault();
    if (!this.form.reportValidity()) return;

    this.dispatchEvent(new CustomEvent('portfolio-settings-save', {
      bubbles: true,
      composed: true,
      detail: {
        patrimony: Number(this.patrimony.value || 0),
        apiToken: this.token.value.trim() || null,
        clearApiToken: this.clearToken.checked
      }
    }));
  }
}

customElements.define('portfolio-settings', PortfolioSettings);
