class PortfolioSettings extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
  }

  connectedCallback() {
    this.shadowRoot.innerHTML = `
      <style>
        :host { display: block; margin-top: 18px; }
        .settings-row { display: flex; align-items: center; justify-content: space-between; gap: 20px; flex-wrap: wrap; padding: 17px 20px; border: 1px solid var(--line, #30414d); border-radius: 14px; background: var(--panel, #17232b); }
        .settings-info strong { display: block; font-size: 13px; }
        .settings-info span { display: block; color: var(--muted, #9cafbb); font-size: 11px; margin-top: 4px; }
        form { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
        label { color: var(--muted, #9cafbb); font-size: 11px; }
        .field { display: flex; align-items: center; gap: 8px; }
        input[type='number'], input[type='password'] { width: 170px; min-width: 0; background: #101a21; border: 1px solid #3a4e5b; border-radius: 9px; padding: 10px 11px; color: var(--text, #eff6fa); font: inherit; }
        input::placeholder { color: #708590; }
        input:focus-visible, button:focus-visible { outline: 2px solid var(--teal, #22d3c5); outline-offset: 2px; }
        button { cursor: pointer; border: 1px solid var(--teal, #22d3c5); background: var(--teal, #22d3c5); color: #062229; padding: 10px 14px; border-radius: 10px; font: inherit; font-weight: 700; font-size: 12px; }
        button:disabled { cursor: wait; opacity: .75; }
        .token-status { color: var(--muted, #9cafbb); font-size: 10px; max-width: 260px; }
        .checkbox { display: flex; gap: 9px; align-items: center; margin-top: 12px; }
        .checkbox input { accent-color: var(--teal, #22d3c5); }
        @media (max-width: 600px) { form { width: 100%; } .field { flex: 1; } .field input { width: 100%; } }
      </style>
      <section class="settings-row" aria-labelledby="title">
        <div class="settings-info">
          <strong id="title">Configuração da carteira</strong>
          <span>Defina o patrimônio-base e, opcionalmente, uma chave da API brapi para consultar os tickers cadastrados.</span>
        </div>
        <form novalidate>
          <div class="field">
            <label for="patrimony">Patrimônio (R$)</label>
            <input type="number" id="patrimony" min="0" step="0.01" placeholder="Ex.: 20131,02" required />
          </div>
          <div class="field">
            <label for="token">Token brapi</label>
            <input type="password" id="token" autocomplete="new-password" placeholder="Opcional; não é exibido depois" />
          </div>
          <button type="submit">Salvar</button>
          <div class="token-status">Sem token configurado. Alguns tickers exigem chave de API.</div>
          <label class="checkbox"><input type="checkbox" /> Remover token salvo</label>
        </form>
      </section>`;

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
