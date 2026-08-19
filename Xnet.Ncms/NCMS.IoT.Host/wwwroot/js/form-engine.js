// wwwroot/js/form-engine.js

function formEngine(config, schema, initialSubmitted = false) {
    return {
        config,
        schema,
        errors: {},
        touched: {},
        submitted: initialSubmitted === true,
        activeSection: '',
        activeTopSection: '',
        navOpen: false,

        visible(path, operator, expected) {
            const actual = this.resolve(this.config, path);
            switch (operator) {
                case 'eq': return actual === expected;
                case 'neq': return actual !== expected;
                case 'truthy': return !!actual;
                case 'gt': return actual > expected;
                case 'lt': return actual < expected;
                case 'in': return Array.isArray(expected) && expected.includes(actual);
                default: return true;
            }
        },

        resolve(obj, path) {
            return path.split('.').reduce((acc, key) => {
                const m = key.match(/^([^\[]+)(\[(\d+)\])?$/);
                if (!acc || !m) return undefined;
                let v = acc[m[1]];
                if (m[3] !== undefined) v = v?.[parseInt(m[3])];
                return v;
            }, obj);
        },

        setValue(path, value) {
            const parts = path.split('.');
            let current = this.config;
            for (let i = 0; i < parts.length; i++) {
                const m = parts[i].match(/^([^\[]+)(\[(\d+)\])?$/);
                if (!m) return;
                const key = m[1];
                const idx = m[3] !== undefined ? parseInt(m[3]) : null;
                const isLast = i === parts.length - 1;

                if (idx !== null) {
                    current[key] ??= [];
                    if (!Array.isArray(current[key])) return;
                    if (isLast) {
                        current[key][idx] = value;
                        return;
                    }
                    current[key][idx] ??= {};
                    current = current[key][idx];
                    continue;
                }

                if (isLast) {
                    current[key] = value;
                    return;
                }

                current[key] ??= {};
                current = current[key];
            }
        },

        touch(path) {
            this.touched[path] = true;
        },

        shouldShowError(path) {
            return this.submitted || this.touched[path] === true;
        },

        // Single generic visibility rule used by every node in the tree (sections, objects,
        // arrays, array items, and leaf fields alike): a node is shown if it IS the selected
        // node, an ancestor of it (so the chain leading to the selection stays mounted), or a
        // descendant of it (so the full subtree under a selection renders). No node-type- or
        // domain-specific logic here — this must work for arbitrary nesting of any shape.
        isVisible(path) {
            const sel = this.activeSection;
            if (!path || !sel) return true;
            if (path === sel) return true;
            if (sel.startsWith(`${path}.`) || sel.startsWith(`${path}[`)) return true;   // path is an ancestor of sel
            if (path.startsWith(`${sel}.`) || path.startsWith(`${sel}[`)) return true;    // path is a descendant of sel
            return false;
        },

        // Back-compat wrappers so existing partials keep working unchanged.
        isContainerVisible(path) {
            return this.isVisible(path);
        },

        isArrayItemVisible(itemPath, _arrayPath) {
            return this.isVisible(itemPath);
        },

        parentPath(path) {
            const dotIndex = path.lastIndexOf('.');
            const bracketIndex = path.lastIndexOf('[');
            const splitIndex = Math.max(dotIndex, bracketIndex);
            if (splitIndex <= 0) return '';
            return path.substring(0, splitIndex);
        },

        isFieldVisible(path) {
            return this.isVisible(path);
        },

        initSectionNav() {
            const firstSection = this.schema?.sections?.[0]?.key || '';
            this.activeSection = firstSection;
            this.activeTopSection = firstSection;
            // No scrollspy: selection is a discrete click, driven entirely by selectNav().
            // Auto-reassigning activeSection from scroll position would fight the "show only
            // the selected subtree" model and make hidden content flicker back in.
        },

        scrollToSection(key) {
            this.selectNav(key, key);
        },

        selectNav(targetPath, sectionKey) {
            this.activeSection = targetPath;
            this.activeTopSection = sectionKey || targetPath;
            this.navOpen = false;
            this.$nextTick(() => {
                const target = this.$root.querySelector(`[data-nav-anchor="${targetPath}"]`);
                target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            });
        },

        beforeSubmit(e) {
            // Lightweight client-side required-field check; server remains source of truth.
            this.submitted = true;
            this.errors = {};
            let ok = true;
            this.$el.querySelectorAll('[required]').forEach(input => {
                if (!input.value) {
                    this.errors[input.name] = 'This field is required.';
                    ok = false;
                }
            });
            if (!ok) e.preventDefault();
        }
    };
}

function arrayField(initialItems, itemSchema) {
    return {
        items: initialItems || [],
        itemSchema,

        add() {
            const blank = {};
            (this.itemSchema.fields || []).forEach(f => blank[f.key] = f.type === 'Number' ? 0 : '');
            this.items.push(blank);
        },

        remove(idx) {
            this.items.splice(idx, 1);
        }
    };
}

function deepArrayField(initialItems, itemSchema) {
    return {
        items: JSON.parse(JSON.stringify(initialItems || [])),
        itemSchema: itemSchema || { fields: [] },

        init() {
            const itemsJson = this.$root.querySelector('script[data-role="initial-items"]')?.textContent || '[]';
            const schemaJson = this.$root.querySelector('script[data-role="item-schema"]')?.textContent || '{"fields":[]}';
            this.items = JSON.parse(itemsJson);
            this.itemSchema = JSON.parse(schemaJson);
        },

        add() {
            const blank = {};
            for (const f of this.itemSchema.fields || []) {
                if (f.type === 'Array') blank[f.key] = [];
                else if (f.type === 'Object') blank[f.key] = {};
                else if (f.type === 'Number') blank[f.key] = 0;
                else if (f.type === 'Checkbox') blank[f.key] = false;
                else blank[f.key] = '';
            }
            this.items.push(blank);
        },

        remove(idx) {
            this.items.splice(idx, 1);
        }
    };
}

// wwwroot/js/rule-engine.js — MUST mirror C# semantics exactly, op-for-op
function evaluateRule(rule, scope, root, self) {
    if (rule.path) return rule.path === '$self' ? self : resolvePath(rule.path, scope, root);
    if ('literal' in rule) return rule.literal;

    const args = (rule.args || []).map(a => evaluateRule(a, scope, root, self));

    switch (rule.op) {
        case 'eq': return args[0] === args[1];
        case 'neq': return args[0] !== args[1];
        case 'gt': return args[0] > args[1];
        case 'lt': return args[0] < args[1];
        case 'gte': return args[0] >= args[1];
        case 'lte': return args[0] <= args[1];
        case 'between': return args[0] >= args[1] && args[0] <= args[2];
        case 'and': return args.every(Boolean);
        case 'or': return args.some(Boolean);
        case 'not': return !args[0];
        case 'in': return args.slice(1).includes(args[0]);
        case 'matches': return new RegExp(args[1]).test(args[0]);
        case 'required': return self !== null && self !== undefined && self !== '';
        default: throw new Error(`Unknown rule op '${rule.op}'`);
    }
}