function appNav() {
    return {
        collapsed: false,
        mobileOpen: false,
        openGroups: {
            management: true,
            rmsconnect: false,
            rmsvpn: false,
            administration: false
        },
        init() {
            const saved = localStorage.getItem('nav-collapsed');
            this.collapsed = saved === 'true';

            // auto-open the group containing the active link
            document.querySelectorAll('.nav-list-item.link-active').forEach(el => {
                const groupEl = el.closest('.nav-group');
                const key = this.keyFromGroupEl(groupEl);
                if (key) this.openGroups[key] = true;
            });

            this.$watch('collapsed', val => localStorage.setItem('nav-collapsed', val));
        },
        toggleCollapsed() {
            this.collapsed = !this.collapsed;
            if (this.collapsed) {
                // close manual expansions when collapsing to icon rail
                Object.keys(this.openGroups).forEach(k => this.openGroups[k] = false);
            }
        },
        toggleGroup(key) {
            if (this.collapsed) {
                this.collapsed = false; // expand nav first if user clicks a group while collapsed
            }
            this.openGroups[key] = !this.openGroups[key];
        },
        keyFromGroupEl(groupEl) {
            const titleEl = groupEl?.querySelector('.nav-title');
            if (!titleEl) return null;
            return titleEl.textContent.trim().replace(/\s+/g, '').toLowerCase();
        }
    };
}