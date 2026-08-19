// ─────────────────────────────────────────────────────────────────────────────
// tableController — reusable HTML-over-the-wire list controller (Alpine + Fetch).
//
// Progressive enhancement: the page ships as a normal GET <form> + <a> links that
// work with a full page load. When this controller is present it intercepts
// searching / filtering / sorting / pagination / page-size changes, fetches ONLY
// the results partial from the same URL, swaps it into #results, and keeps the
// browser URL in sync (pushState) so bookmarking and Back/Forward keep working.
//
// The server returns the results partial (not JSON) when it sees the
// "X-Partial-Table" request header; otherwise it returns the full page.
//
// Usage (per list page):
//   <div x-data="tableController" data-table-controller>
//     <form method="get" @submit.prevent="onSubmit($event)"> …filters… </form>
//     <div id="results" x-ref="results" @click="onClick($event)" @change="onChange($event)"
//          :class="{ 'opacity-50 pe-none': loading }">
//       <partial name="_Results" model="Model" />
//     </div>
//   </div>
// ─────────────────────────────────────────────────────────────────────────────
document.addEventListener('alpine:init', () => {
    Alpine.data('tableController', () => ({
        tableLoading: false,   // distinct name: several page components have their own `loading`
        _abort: null,
        _onPop: null,

        init() {
            this.resultsId = this.$root?.dataset?.resultsId || 'results';

            // Back/Forward: re-fetch the results for whatever URL the browser restored.
            this._onPop = () => this.load(location.pathname + location.search, false);
            window.addEventListener('popstate', this._onPop);

            // Initial mount: #results already has the server-rendered rows (progressive
            // enhancement — a no-JS client stops here and sees them immediately). Once JS
            // is running, hide that content behind the same centered loader used for every
            // other refresh and re-fetch the current URL, so the table only reveals rows
            // once the fetch resolves rather than flashing content before Alpine attached.
            const el = document.getElementById(this.resultsId);
            if (el) {
                el.classList.add('initial-hide');
                el.prepend(Object.assign(document.createElement('div'), { className: 'table-init-spinner' }));
                this.load(location.pathname + location.search, false).finally(() => {
                    el.classList.remove('initial-hide');
                });
            }
        },

        destroy() {
            if (this._onPop) window.removeEventListener('popstate', this._onPop);
            if (this._abort) this._abort.abort();
        },

        // Filter form submit → build a clean URL from the form and reset to page 1.
        // Page size and sort are carried from the live URL (the source of truth), not the
        // form's hidden fields, which can go stale after a pager-driven page-size/sort change
        // (the form isn't re-rendered on partial updates).
        onSubmit(e) {
            const form = e.target.closest('form');
            if (!form) return;
            const params = new URLSearchParams(new FormData(form));
            const current = new URLSearchParams(location.search);
            if (current.has('pageSize')) params.set('pageSize', current.get('pageSize'));
            if (current.has('sort')) params.set('sort', current.get('sort'));
            params.set('pageNumber', '1');
            for (const [k, v] of [...params]) {
                if (v === '' || v == null) params.delete(k);
            }
            const qs = params.toString();
            this.load(location.pathname + (qs ? '?' + qs : ''));
        },

        // Delegated: sort-header and pager links inside #results.
        onClick(e) {
            const link = e.target.closest('a.table-sort, a.page-link');
            if (!link) return;
            e.preventDefault();
            if (link.closest('.disabled')) return; // disabled prev/next
            const href = link.getAttribute('href');
            if (href) this.load(link.href);
        },

        // Delegated: the "N / page" selector inside the pager.
        onChange(e) {
            const sel = e.target.closest('select[data-page-size]');
            if (!sel || !sel.value) return;
            e.preventDefault();
            this.load(sel.value);
        },

        async load(url, push = true) {
            if (this._abort) this._abort.abort();
            this._abort = new AbortController();
            this.tableLoading = true;
            try {
                const res = await fetch(url, {
                    headers: { 'X-Partial-Table': '1' },
                    credentials: 'same-origin',
                    signal: this._abort.signal
                });
                if (!res.ok) { window.location.href = url; return; } // hard-nav on server error
                const html = await res.text();
                // Swap the results fragment. Alpine's MutationObserver auto-initialises any
                // directives in the new nodes (e.g. row action buttons bound to the page's own
                // component), resolving them against the surrounding scope.
                const el = document.getElementById(this.resultsId);
                el.innerHTML = html;
                if (push) history.pushState(null, '', url);
            } catch (err) {
                if (err.name !== 'AbortError') window.location.href = url; // network error → hard-nav
            } finally {
                this.tableLoading = false;
            }
        }
    }));
});

// ─────────────────────────────────────────────────────────────────────────────
// confirmDialog — standardized confirm/delete dialog, replacing native confirm().
// A single global Alpine store (rendered once in _Layout via the _ConfirmDialog
// partial) so every page shares the exact same look/behavior instead of each
// module rolling its own modal or falling back to window.confirm().
//
// Usage (from any @click handler, anywhere Alpine has bound the element):
//   confirmAction('Confirm Delete', 'Do you want to delete the Firmware Package?',
//                 () => $el.closest('form').requestSubmit(), { itemName: p.name });
//   confirmAction('Abort Task', 'Abort the entire task?', () => this.doAbortTask(),
//                 { confirmText: 'Abort', variant: 'danger' });
// ─────────────────────────────────────────────────────────────────────────────
document.addEventListener('alpine:init', () => {
    Alpine.store('confirmDialog', {
        show: false,
        title: '',
        message: '',
        itemName: '',
        confirmText: 'Confirm',
        variant: 'danger',
        _onConfirm: null,
        open(title, message, onConfirm, opts = {}) {
            this.title = title;
            this.message = message;
            this.itemName = opts.itemName || '';
            this.confirmText = opts.confirmText || 'Confirm';
            this.variant = opts.variant || 'danger';
            this._onConfirm = onConfirm;
            this.show = true;
        },
        confirm() {
            this.show = false;
            const run = this._onConfirm;
            this._onConfirm = null;
            if (run) run();
        },
        cancel() {
            this.show = false;
            this._onConfirm = null;
        }
    });
});

window.confirmAction = function (title, message, onConfirm, opts) {
    Alpine.store('confirmDialog').open(title, message, onConfirm, opts);
};

// ─────────────────────────────────────────────────────────────────────────────
// pagerWindow — windowed page-number list for client-side (Alpine) paginations,
// mirroring the shared server-side _Pager (a window of up to 5 pages centred on
// the current page). Keeps modal-dialog pagers visually/behaviourally identical
// to the app-wide pager.
// ─────────────────────────────────────────────────────────────────────────────
window.pagerWindow = function (current, totalPages) {
    const total = Math.max(1, totalPages | 0);
    const cur = Math.min(Math.max(1, current | 0), total);
    let start = Math.max(1, cur - 2);
    const end = Math.min(total, start + 4);
    start = Math.max(1, end - 4);
    const pages = [];
    for (let p = start; p <= end; p++) pages.push(p);
    return pages;
};

// ─────────────────────────────────────────────────────────────────────────────
// Global page loader — NProgress-style top bar for real full-page navigations
// (sidebar/breadcrumb links, non-intercepted form posts, browser Back/Forward).
// Centralized here: no per-page markup or script needed.
//
// Each full navigation runs this file fresh (no SPA state survives), so on
// arrival it always plays a short "completing" flash — the visual counterpart
// to the bar that started animating on the page you navigated away from.
//
// tableController's own submit/click handlers are bound directly on the form
// or link and call preventDefault() before this document-level listener runs
// (it fires later, during bubbling), so partial table refreshes correctly
// don't also trigger this bar — they already have their own #results overlay.
// ─────────────────────────────────────────────────────────────────────────────
(function () {
    const bar = document.createElement('div');
    bar.id = 'page-progress-bar';
    document.body.appendChild(bar);

    requestAnimationFrame(() => {
        // Must set opacity to 1 here — the bar's CSS default is opacity:0, so
        // without this the width animation below plays invisibly and the only
        // thing the user ever sees is the previous page's start() animation
        // getting cut off mid-flight by the navigation itself.
        bar.style.transition = 'none';
        bar.style.opacity = '1';
        void bar.offsetWidth;
        bar.style.transition = 'width .3s ease';
        bar.style.width = '100%';
        setTimeout(() => {
            bar.style.transition = 'opacity .25s ease';
            bar.style.opacity = '0';
            setTimeout(() => { bar.style.transition = 'none'; bar.style.width = '0%'; }, 250);
        }, 300);
    });

    function start() {
        bar.style.transition = 'none';
        bar.style.opacity = '1';
        bar.style.width = '0%';
        void bar.offsetWidth; // force reflow so the width transition below animates from 0
        bar.style.transition = 'width 4s cubic-bezier(0.1, 0.7, 0.3, 1)';
        bar.style.width = '80%';
    }

    document.addEventListener('click', (e) => {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        const link = e.target.closest('a[href]');
        if (!link || link.target === '_blank' || link.hasAttribute('download')) return;
        const href = link.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:') || href.startsWith('mailto:')) return;
        if (link.origin !== location.origin) return;
        start();
    });

    document.addEventListener('submit', (e) => {
        if (e.defaultPrevented) return;
        start();
    });
})();
