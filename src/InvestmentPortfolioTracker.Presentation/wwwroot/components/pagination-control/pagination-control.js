class PaginationControl extends HTMLElement {
  constructor() {
    super();
    this.attachShadow({ mode: 'open' });
    this.pagination = null;
    this.loading = false;
  }

  connectedCallback() {
    this.ready ??= this.initialize();
  }

  async initialize() {
    const [templateResponse, stylesResponse] = await Promise.all([
      fetch('/components/pagination-control/pagination-control.html'),
      fetch('/components/pagination-control/pagination-control.css')
    ]);

    if (!templateResponse.ok || !stylesResponse.ok) {
      throw new Error('Não foi possível carregar os controles de paginação.');
    }

    const [template, styles] = await Promise.all([
      templateResponse.text(),
      stylesResponse.text()
    ]);

    this.shadowRoot.innerHTML = `<style>${styles}</style>${template}`;
    this.shadowRoot.querySelector('[data-action="previous"]')
      .addEventListener('click', () => this.emitPageChange((this.pagination?.pageNumber ?? 1) - 1));
    this.shadowRoot.querySelector('[data-action="next"]')
      .addEventListener('click', () => this.emitPageChange((this.pagination?.pageNumber ?? 1) + 1));
    this.pageSizeSelect.addEventListener('change', () => {
      this.dispatchEvent(new CustomEvent('pagination-change', {
        bubbles: true,
        composed: true,
        detail: { pageNumber: 1, pageSize: Number(this.pageSizeSelect.value) }
      }));
    });

    if (this.pagination) this.render();
  }

  get pageSizeSelect() {
    return this.shadowRoot.querySelector('.page-size');
  }

  setPagination(pagination) {
    this.pagination = pagination;
    if (this.shadowRoot.querySelector('.pagination')) this.render();
  }

  setLoading(isLoading) {
    this.loading = isLoading;
    if (this.shadowRoot.querySelector('.pagination')) this.render();
  }

  emitPageChange(pageNumber) {
    if (this.loading || !this.pagination) return;

    this.dispatchEvent(new CustomEvent('pagination-change', {
      bubbles: true,
      composed: true,
      detail: { pageNumber, pageSize: this.pagination.pageSize }
    }));
  }

  render() {
    const { pageNumber, pageSize, totalItems, totalPages } = this.pagination;
    const summary = this.shadowRoot.querySelector('.pagination-summary');
    const previousButton = this.shadowRoot.querySelector('[data-action="previous"]');
    const nextButton = this.shadowRoot.querySelector('[data-action="next"]');
    const pageNumbers = this.shadowRoot.querySelector('.page-numbers');
    const firstItem = totalItems === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
    const lastItem = Math.min(pageNumber * pageSize, totalItems);

    summary.textContent = totalItems === 0
      ? 'Nenhum ativo cadastrado'
      : `Exibindo ${firstItem}–${lastItem} de ${totalItems} ativos`;
    this.pageSizeSelect.value = String(pageSize);
    this.pageSizeSelect.disabled = this.loading;
    previousButton.disabled = this.loading || !this.pagination.hasPreviousPage;
    nextButton.disabled = this.loading || !this.pagination.hasNextPage;

    const windowSize = 5;
    const firstPage = Math.max(1, Math.min(pageNumber - Math.floor(windowSize / 2), totalPages - windowSize + 1));
    const lastPage = Math.min(totalPages, firstPage + windowSize - 1);
    pageNumbers.replaceChildren();

    for (let page = firstPage; page <= lastPage; page += 1) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = String(page);
      button.setAttribute('aria-label', `Página ${page}`);
      if (page === pageNumber) button.setAttribute('aria-current', 'page');
      button.disabled = this.loading || page === pageNumber;
      button.addEventListener('click', () => this.emitPageChange(page));
      pageNumbers.append(button);
    }
  }
}

customElements.define('pagination-control', PaginationControl);
