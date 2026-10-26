(function () {
    // --- State ---
    const state = {
        schema: [],
        tabs: [],
        activeTabId: null,
        tabCounter: 0,
        lastResult: null,
        isExecuting: false,
        history: [],
        historyTableName: ''
    };

    // --- DOM Elements ---
    const els = {
        schemaTree: document.getElementById('schema-tree'),
        btnRefreshSchema: document.getElementById('btn-refresh-schema'),
        btnCollapseAll: document.getElementById('btn-collapse-all'),
        editorTabs: document.getElementById('editor-tabs'),
        btnAddTab: document.getElementById('btn-add-tab'),
        codeEditor: document.getElementById('code-editor'),
        highlightLayer: document.getElementById('highlight-layer'),
        lineNumbers: document.getElementById('line-numbers'),
        emptyEditor: document.getElementById('empty-editor'),
        editorContentWrapper: document.getElementById('editor-content-wrapper'),
        btnRun: document.getElementById('btn-run'),
        runIcon: document.getElementById('run-icon'),
        runSpinner: document.getElementById('run-spinner'),
        btnSave: document.getElementById('btn-save'),
        btnFormat: document.getElementById('btn-format'),
        btnCopy: document.getElementById('btn-copy'),
        copyText: document.getElementById('copy-text'),
        resultsMessages: document.getElementById('results-messages'),
        resultsThead: document.getElementById('results-thead'),
        resultsTbody: document.getElementById('results-tbody'),
        btnExportCsv: document.getElementById('btn-export-csv'),
        statusRowsCount: document.getElementById('status-rows-count'),
        statusState: document.getElementById('status-state'),
        sidebar: document.getElementById('sidebar'),
        btnToggleSidebar: document.getElementById('btn-toggle-sidebar'),
        btnToggleTheme: document.getElementById('btn-toggle-theme'),
        themeIcon: document.getElementById('theme-icon'),
        
        // History Elements
        tabObjects: document.getElementById('tab-objects'),
        tabHistory: document.getElementById('tab-history'),
        sectionObjects: document.getElementById('section-objects'),
        sectionHistory: document.getElementById('section-history'),
        historyTree: document.getElementById('history-tree'),
        globalSearch: document.getElementById('global-search'),
        btnRefreshHistory: document.getElementById('btn-refresh-history')
    };

    // --- Initialization ---
    function init() {
        bindEvents();
        applyThemeState();
        if (window.EnableSchemaLoad) {
            loadSchema();
            if (window.AllowHistoryLog) {
                loadHistory();
            }
            applySidebarState();
        }
        
        // Initial health check
        checkHealth();
        // Poll health every 30 seconds
        setInterval(checkHealth, 30000);

        addTab('query_1.sql', "SELECT\n    u.id,\n    u.username,\n    COUNT(e.id) AS total_events,\n    MAX(e.timestamp) AS last_active\nFROM users u\nJOIN events e ON u.id = e.user_id\nWHERE e.status = 'active'\nGROUP BY u.id, u.username\nORDER BY last_active DESC;");
    }

    // --- UI Helpers ---
    function bindEvents() {
        if (els.btnRefreshSchema) els.btnRefreshSchema.addEventListener('click', loadSchema);
        if (els.btnCollapseAll) els.btnCollapseAll.addEventListener('click', () => renderSchema(state.schema));

        if (els.btnAddTab) {
            els.btnAddTab.addEventListener('click', () => {
                state.tabCounter++;
                addTab(`query_${state.tabCounter}.sql`, "-- New Query");
            });
        }

        if (els.codeEditor) {
            els.codeEditor.addEventListener('input', handleEditorInput);
            els.codeEditor.addEventListener('scroll', handleEditorScroll);
        }
        
        if (els.btnRun) els.btnRun.addEventListener('click', executeQuery);
        if (els.btnSave) els.btnSave.addEventListener('click', saveSql);
        if (els.btnFormat) els.btnFormat.addEventListener('click', formatSql);
        if (els.btnCopy) els.btnCopy.addEventListener('click', copySql);
        if (els.btnExportCsv) els.btnExportCsv.addEventListener('click', exportCsv);

        if (els.btnToggleSidebar) els.btnToggleSidebar.addEventListener('click', toggleSidebar);
        if (els.btnToggleTheme) els.btnToggleTheme.addEventListener('click', toggleTheme);

        if (els.tabObjects) els.tabObjects.addEventListener('click', () => switchSidebarTab('objects'));
        if (els.tabHistory) els.tabHistory.addEventListener('click', () => switchSidebarTab('history'));
        if (els.btnRefreshHistory) els.btnRefreshHistory.addEventListener('click', loadHistory);
        if (els.globalSearch) {
            els.globalSearch.addEventListener('input', (e) => {
                const val = e.target.value;
                filterSchema(val);
                filterHistory(val);
            });
        }
    }

    function toggleSidebar() {
        const isCollapsed = els.sidebar.classList.toggle('collapsed');
        localStorage.setItem('sqlstudio_sidebar_collapsed', isCollapsed);
    }

    function applySidebarState() {
        const isCollapsed = localStorage.getItem('sqlstudio_sidebar_collapsed') === 'true';
        if (isCollapsed) els.sidebar.classList.add('collapsed');
        else els.sidebar.classList.remove('collapsed');
    }

    function switchSidebarTab(tab) {
        if (tab === 'objects') {
            els.tabObjects.classList.add('active');
            els.tabHistory.classList.remove('active');
            els.sectionObjects.classList.add('active');
            els.sectionObjects.style.display = 'flex';
            els.sectionHistory.classList.remove('active');
            els.sectionHistory.style.display = 'none';
        } else {
            els.tabObjects.classList.remove('active');
            els.tabHistory.classList.add('active');
            els.sectionObjects.classList.remove('active');
            els.sectionObjects.style.display = 'none';
            els.sectionHistory.classList.add('active');
            els.sectionHistory.style.display = 'flex';
            if (window.AllowHistoryLog) {
                loadHistory();
            }
        }
    }

    function toggleTheme() {
        const pageEl = document.querySelector('.page');
        const isLight = pageEl.classList.toggle('theme-light');
        localStorage.setItem('sqlstudio_theme', isLight ? 'light' : 'dark');
        applyThemeState();
    }

    function applyThemeState() {
        const theme = localStorage.getItem('sqlstudio_theme') || 'dark';
        const pageEl = document.querySelector('.page');
        if (theme === 'light') {
            pageEl.classList.add('theme-light');
            if (els.themeIcon) {
                els.themeIcon.classList.remove('bi-sun-fill');
                els.themeIcon.classList.add('bi-moon-fill');
            }
        } else {
            pageEl.classList.remove('theme-light');
            if (els.themeIcon) {
                els.themeIcon.classList.remove('bi-moon-fill');
                els.themeIcon.classList.add('bi-sun-fill');
            }
        }
    }

    // --- End-to-end encrypted API requests ---
    function bytesToBase64(bytes) {
        let binary = '';
        bytes.forEach(value => binary += String.fromCharCode(value));
        return btoa(binary);
    }

    function base64ToBytes(value) {
        const binary = atob(value);
        return Uint8Array.from(binary, character => character.charCodeAt(0));
    }

    async function encryptedRequest(path, operation, payload) {
        if (!window.isSecureContext || !window.crypto || !window.crypto.subtle)
            throw new Error('E2EE requires HTTPS and browser Web Crypto support.');

        const keyId = window.SqlStudioBackendKeyId;
        const route = path.startsWith('/') ? path.slice(1) : path;
        if (!['query', 'schema', 'history'].includes(route)) throw new Error('Unsupported E2EE route.');
        const requestId = crypto.randomUUID();
        const additionalData = new TextEncoder().encode(`Mma.SqlStudio.E2EE.v1|${keyId}|${requestId}|${route}|request`);
        const responseAdditionalData = new TextEncoder().encode(`Mma.SqlStudio.E2EE.v1|${keyId}|${requestId}|${route}|response`);
        const aesKey = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt']);
        const requestIv = crypto.getRandomValues(new Uint8Array(12));
        const requestPlaintext = new TextEncoder().encode(JSON.stringify({ operation, requestId, ...payload }));
        const encryptedPayload = await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv: requestIv, additionalData }, aesKey, requestPlaintext);
        const rawAesKey = await crypto.subtle.exportKey('raw', aesKey);
        const backendJwk = JSON.parse(window.SqlStudioBackendPublicKeyJwk);
        const backendKey = await crypto.subtle.importKey(
            'jwk', backendJwk, { name: 'RSA-OAEP', hash: 'SHA-256' }, false, ['encrypt']);
        const wrappedKey = await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, backendKey, rawAesKey);

        const response = await fetch(`${window.SqlStudioApiUrl}${path}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                version: 1,
                algorithm: 'RSA-OAEP-256+A256GCM',
                keyId,
                requestId,
                wrappedKey: bytesToBase64(new Uint8Array(wrappedKey)),
                iv: bytesToBase64(requestIv),
                ciphertext: bytesToBase64(new Uint8Array(encryptedPayload))
            })
        });
        if (!response.ok) throw new Error(`Encrypted API request failed (HTTP ${response.status}).`);

        const encryptedResponse = await response.json();
        if (encryptedResponse.version !== 1 || encryptedResponse.keyId !== keyId || encryptedResponse.requestId !== requestId)
            throw new Error('Invalid encrypted API response envelope.');
        const responsePlaintext = await crypto.subtle.decrypt({
            name: 'AES-GCM',
            iv: base64ToBytes(encryptedResponse.iv),
            additionalData: responseAdditionalData
        }, aesKey, base64ToBytes(encryptedResponse.ciphertext));
        return JSON.parse(new TextDecoder().decode(responsePlaintext));
    }

    // --- Health Check ---
    async function checkHealth() {
        const indicator = document.getElementById('health-indicator');
        const text = document.getElementById('connection-text');
        
        try {
            const response = await fetch(`${window.SqlStudioApiUrl}/health`);
            if (response.ok) {
                const data = await response.json();
                if (data.healthy) {
                    indicator.style.color = '#81C784'; // Green
                    text.innerText = 'Connected';
                } else {
                    indicator.style.color = '#E57373'; // Red
                    text.innerText = 'Disconnected (API Unhealthy)';
                }
            } else {
                indicator.style.color = '#E57373';
                text.innerText = `Disconnected (${response.status})`;
            }
        } catch (e) {
            indicator.style.color = '#E57373';
            text.innerText = 'Disconnected (Network Error)';
            console.error('Health check failed', e);
        }
    }

    // --- Schema Explorer ---
    async function loadSchema() {
        els.btnRefreshSchema.classList.add('spin');
        try {
            let rawSchema;
            if (window.SqlStudioEncryptionEnabled) {
                rawSchema = await encryptedRequest('/schema', 'schema.get', {
                    excludedSchemas: window.SqlStudioExcludedSchemas || [],
                    excludedObjects: window.SqlStudioExcludedObjects || []
                });
            } else {
                const response = await fetch(`${window.SqlStudioApiUrl}/schema`);
                if (!response.ok) throw new Error(`Schema request failed (HTTP ${response.status}).`);
                rawSchema = await response.json();
            }
            // Normalize schema so categories use 'children' instead of 'objects'
            const excludedSchemas = (window.SqlStudioExcludedSchemas || []).map(name => name.toLowerCase());
            const excludedObjects = (window.SqlStudioExcludedObjects || []).map(name => name.toLowerCase());
            state.schema = (rawSchema || []).filter(s => !excludedSchemas.includes((s.name || '').toLowerCase())).map(s => ({
                name: s.name,
                isVisible: true,
                children: (s.children || []).map(c => ({
                    name: c.name,
                    isVisible: true,
                    parentType: c.name,
                    children: (c.objects || []).filter(o => !excludedObjects.includes(o.toLowerCase())).map(o => ({
                        name: o,
                        schemaName: s.name,
                        parentType: c.name,
                        isVisible: true
                    }))
                }))
            }));
            renderSchema(state.schema);
        } catch (e) {
            console.error('Failed to load schema', e);
        } finally {
            els.btnRefreshSchema.classList.remove('spin');
        }
    }

    function renderSchema(nodes, parentEl = els.schemaTree, depth = 0) {
        if (depth === 0) parentEl.innerHTML = '';
        
        if (!nodes || nodes.length === 0) {
            if (depth === 0) {
                parentEl.innerHTML = '<div style="padding:16px;color:var(--outline-variant);text-align:center;">No objects found</div>';
            }
            return;
        }

        nodes.forEach(node => {
            if (node.isVisible === false) return;

            const itemEl = document.createElement('div');
            itemEl.className = 'tree-item';

            const rowEl = document.createElement('div');
            rowEl.className = depth > 1 ? 'tree-row obj-row' : 'tree-row';
            
            // Icon
            let iconClass = 'bi-shield-lock';
            let iconColor = '#90A4AE';
            if (depth === 1) {
                iconClass = node.name === 'Tables' ? 'bi-folder-fill' : (node.name === 'Views' ? 'bi-layout-sidebar-inset' : 'bi-cpu');
                iconColor = node.name === 'Tables' ? '#FFCA28' : (node.name === 'Views' ? '#4CAF50' : '#9C27B0');
            } else if (depth === 2) {
                iconClass = node.parentType === 'Tables' ? 'bi-table' : (node.parentType === 'Views' ? 'bi-grid-3x3-gap' : 'bi-terminal');
                iconColor = 'var(--on-surface-variant)';
            }

            if (node.children && node.children.length > 0) {
                const caret = document.createElement('i');
                caret.className = 'bi bi-caret-right-fill caret';
                rowEl.appendChild(caret);
                rowEl.appendChild(document.createTextNode(' '));
            } else {
                const spacer = document.createElement('span');
                spacer.className = 'spacer';
                spacer.style.cssText = 'width:16px;display:inline-block';
                rowEl.appendChild(spacer);
                rowEl.appendChild(document.createTextNode(' '));
            }
            const icon = document.createElement('i');
            icon.className = `bi ${iconClass}`;
            icon.style.color = iconColor;
            rowEl.appendChild(icon);
            rowEl.appendChild(document.createTextNode(' '));
            const label = document.createElement('span');
            label.className = 'node-text';
            label.textContent = node.name || node;
            label.title = node.name || node;
            rowEl.appendChild(label);
            itemEl.appendChild(rowEl);

            if (node.children && node.children.length > 0) {
                const childrenContainer = document.createElement('div');
                childrenContainer.className = 'tree-children';
                
                renderSchema(node.children, childrenContainer, depth + 1);
                itemEl.appendChild(childrenContainer);

                rowEl.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const caret = rowEl.querySelector('.caret');
                    if (caret) caret.classList.toggle('expanded');
                    childrenContainer.classList.toggle('expanded');
                });
            } else if (depth === 2) {
                // Leaf node double click
                rowEl.addEventListener('dblclick', () => {
                    openObjectScript(node.schemaName, node.parentType, node.name);
                });
            }

            parentEl.appendChild(itemEl);
        });
    }

    function filterSchema(text) {
        if (!text) {
            // Reset
            state.schema.forEach(s => {
                s.isVisible = true;
                if (s.children) s.children.forEach(c => {
                    c.isVisible = true;
                    if (c.children) c.children.forEach(o => o.isVisible = true);
                });
            });
            renderSchema(state.schema);
            return;
        }

        const lowerText = text.toLowerCase();
        state.schema.forEach(s => {
            let schemaMatch = s.name.toLowerCase().includes(lowerText);
            let sHasVisibleChild = false;

            if (s.children) {
                s.children.forEach(c => {
                    let cMatch = c.name.toLowerCase().includes(lowerText);
                    let cHasVisibleChild = false;

                    if (c.children) {
                        c.children.forEach(o => {
                            const name = o.name;
                            const oMatch = name.toLowerCase().includes(lowerText);
                            o.isVisible = oMatch || cMatch || schemaMatch;
                            if (oMatch) cHasVisibleChild = true;
                        });
                    }
                    c.isVisible = cMatch || cHasVisibleChild || schemaMatch;
                    if (c.isVisible) sHasVisibleChild = true;
                });
            }
            s.isVisible = schemaMatch || sHasVisibleChild;
        });

        renderSchema(state.schema);
        // Expand all when filtering
        document.querySelectorAll('.tree-children').forEach(el => el.classList.add('expanded'));
        document.querySelectorAll('.caret').forEach(el => el.classList.add('expanded'));
    }

    // --- History Explorer ---
    async function loadHistory() {
        if (!window.AllowHistoryLog || !els.btnRefreshHistory) return;
        els.btnRefreshHistory.classList.add('spin');
        try {
            let data;
            if (window.SqlStudioEncryptionEnabled) {
                data = await encryptedRequest('/history', 'history.list', {});
            } else {
                const response = await fetch(`${window.SqlStudioApiUrl}/history`);
                if (!response.ok) throw new Error(`History request failed (HTTP ${response.status}).`);
                data = await response.json();
            }
            state.history = (data.items || []).map(h => ({
                ...h,
                isVisible: true
            }));
            state.historyTableName = data.tableName;
            renderHistory();
        } catch (e) {
            console.error('Failed to load history', e);
        } finally {
            els.btnRefreshHistory.classList.remove('spin');
        }
    }

    function renderHistory() {
        if (!els.historyTree) return;
        els.historyTree.innerHTML = '';

        // Add special "View All History" node
        const viewAllEl = document.createElement('div');
        viewAllEl.className = 'tree-row';
        viewAllEl.style.color = 'var(--primary)';
        viewAllEl.innerHTML = `<i class="bi bi-journal-text"></i> <span class="node-text">View All History (SQL)</span>`;
        viewAllEl.addEventListener('click', () => {
            const tableName = state.historyTableName || "__SqlStudioQueryHistory";
            addTab('history_all.sql', `SELECT TOP 1000 * FROM [${tableName}] ORDER BY [Timestamp] DESC;`);
        });
        els.historyTree.appendChild(viewAllEl);

        if (!state.history || state.history.length === 0) {
            const emptyEl = document.createElement('div');
            emptyEl.style.padding = '16px';
            emptyEl.style.color = 'var(--outline-variant)';
            emptyEl.style.textAlign = 'center';
            emptyEl.innerText = 'No history yet';
            els.historyTree.appendChild(emptyEl);
            return;
        }

        state.history.forEach(item => {
            if (item.isVisible === false) return;

            const rowEl = document.createElement('div');
            rowEl.className = 'tree-row';
            
            const timestamp = item.executedAt || item.timestamp;
            const query = item.queryText || item.query;
            
            const time = new Date(timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            const date = new Date(timestamp).toLocaleDateString([], { month: 'short', day: 'numeric' });
            
            // Clean up query for display (remove newlines, trim)
            const displayQuery = (query || '').replace(/\n/g, ' ').replace(/\s+/g, ' ').substring(0, 50).trim();

            const icon = document.createElement('i');
            icon.className = 'bi bi-clock';
            icon.style.cssText = 'color:var(--outline);font-size:10px;';
            rowEl.appendChild(icon);
            const label = document.createElement('span');
            label.className = 'node-text';
            label.title = query || '';
            const timestampLabel = document.createElement('small');
            timestampLabel.style.cssText = 'color:var(--outline);font-size:9px;margin-right:4px;';
            timestampLabel.textContent = `${date} ${time}`;
            label.appendChild(timestampLabel);
            label.appendChild(document.createTextNode(`${displayQuery}...`));
            rowEl.appendChild(label);

            rowEl.addEventListener('dblclick', () => {
                addTab(`history_${new Date(timestamp).getTime()}.sql`, query);
            });

            els.historyTree.appendChild(rowEl);
        });
    }

    function filterHistory(text) {
        if (!text) {
            state.history.forEach(h => h.isVisible = true);
        } else {
            const lowerText = text.toLowerCase();
            state.history.forEach(h => {
                const query = h.queryText || h.query || '';
                h.isVisible = query.toLowerCase().includes(lowerText);
            });
        }
        renderHistory();
    }

    function openObjectScript(schema, group, name) {
        const tabName = `${schema}.${name}.sql`;
        let content = `-- Script for ${group.replace(/s$/, '')} ${schema}.${name}\n`;
        
        if (group === "Tables" || group === "Views")
            content += `SELECT TOP 100 * FROM [${schema}].[${name}];`;
        else
            content += `EXEC [${schema}].[${name}];`;

        addTab(tabName, content);
    }

    // --- Tabs & Editor ---
    function addTab(name, content) {
        const id = 'tab_' + Math.random().toString(36).substr(2, 9);
        state.tabs.push({ id, name, content });
        selectTab(id);
        renderTabs();
    }

    function selectTab(id) {
        state.activeTabId = id;
        renderTabs();
        updateEditorView();
    }

    function closeTab(id, event) {
        if (event) event.stopPropagation();
        const index = state.tabs.findIndex(t => t.id === id);
        if (index > -1) {
            state.tabs.splice(index, 1);
            if (state.activeTabId === id) {
                state.activeTabId = state.tabs.length > 0 ? state.tabs[state.tabs.length - 1].id : null;
            }
            renderTabs();
            updateEditorView();
        }
    }

    function renderTabs() {
        // Remove existing tabs but keep the add button
        const tabs = Array.from(els.editorTabs.children).filter(c => c !== els.btnAddTab);
        tabs.forEach(t => t.remove());

        state.tabs.forEach(tab => {
            const el = document.createElement('div');
            el.className = `tab ${tab.id === state.activeTabId ? 'active' : ''}`;
            const icon = document.createElement('i');
            icon.className = `bi ${tab.name.endsWith('.sql') ? 'bi-filetype-sql' : 'bi-table'}`;
            const label = document.createElement('span');
            label.textContent = tab.name;
            const closeButton = document.createElement('i');
            closeButton.className = 'bi bi-x close-btn';
            closeButton.style.marginLeft = '4px';
            el.append(icon, label, closeButton);
            el.addEventListener('click', () => selectTab(tab.id));
            el.querySelector('.close-btn').addEventListener('click', (e) => closeTab(tab.id, e));
            els.editorTabs.insertBefore(el, els.btnAddTab);
        });

        els.btnRun.disabled = !state.activeTabId;
    }

    function getActiveTab() {
        return state.tabs.find(t => t.id === state.activeTabId);
    }

    function updateEditorView() {
        const tab = getActiveTab();
        if (tab) {
            els.emptyEditor.style.display = 'none';
            els.editorContentWrapper.style.display = 'flex';
            
            if (els.codeEditor.value !== tab.content) {
                els.codeEditor.value = tab.content;
            }
            updateHighlighting(tab.content);
            updateLineNumbers(tab.content);
        } else {
            els.emptyEditor.style.display = 'flex';
            els.editorContentWrapper.style.display = 'none';
            els.codeEditor.value = '';
            els.highlightLayer.innerHTML = '';
        }
    }

    function handleEditorInput(e) {
        const tab = getActiveTab();
        if (tab) {
            tab.content = e.target.value;
            updateHighlighting(tab.content);
            updateLineNumbers(tab.content);
        }
    }

    function handleEditorScroll(e) {
        els.highlightLayer.scrollTop = els.codeEditor.scrollTop;
        els.highlightLayer.scrollLeft = els.codeEditor.scrollLeft;
        els.lineNumbers.scrollTop = els.codeEditor.scrollTop;
    }

    function updateLineNumbers(code) {
        const lines = code.split('\n').length;
        let html = '';
        for (let i = 1; i <= Math.max(1, lines); i++) {
            html += `<div class="line-no">${i}</div>`;
        }
        els.lineNumbers.innerHTML = html;
    }

    function updateHighlighting(code) {
        if (!code) {
            els.highlightLayer.innerHTML = '';
            return;
        }

        let html = code.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

        const pattern = /(?<comment>--.*)|(?<string>'.*?')|(?<keyword>\b(SELECT|FROM|WHERE|JOIN|ON|GROUP\s+BY|ORDER\s+BY|DESC|ASC|AND|OR|IN|AS|LIMIT|UPDATE|SET|DELETE|INSERT|INTO|VALUES|CREATE|TABLE|DATABASE|DROP|ALTER|EXISTS|NOT|NULL|PRIMARY|KEY|FOREIGN|REFERENCES|GO|USE|EXEC|PROCEDURE)\b)|(?<function>\b(COUNT|MAX|MIN|AVG|SUM|GETDATE|CAST|CONVERT|COALESCE)\b)/gi;

        html = html.replace(pattern, (match, p1, p2, p3, p4, p5, p6, offset, string, groups) => {
            if (groups.comment) return `<span class='hl-comment'>${match}</span>`;
            if (groups.string) return `<span class='hl-string'>${match}</span>`;
            if (groups.keyword) return `<span class='hl-keyword'>${match}</span>`;
            if (groups.function) return `<span class='hl-function'>${match}</span>`;
            return match;
        });

        if (html.endsWith('\n')) html += ' ';
        els.highlightLayer.innerHTML = html;
    }

    function formatSql() {
        const tab = getActiveTab();
        if (!tab) return;

        const keywords = ["SELECT", "FROM", "WHERE", "JOIN", "ON", "GROUP BY", "ORDER BY", "DESC", "ASC", "AND", "OR", "IN", "AS", "LIMIT", "COUNT", "MAX", "MIN", "AVG", "SUM", "UPDATE", "SET", "DELETE", "INSERT", "INTO", "VALUES", "CREATE", "TABLE", "DATABASE", "DROP", "ALTER", "EXISTS", "NOT", "NULL", "PRIMARY", "KEY", "FOREIGN", "REFERENCES"];
        let content = tab.content;
        
        keywords.forEach(kw => {
            const regex = new RegExp(`\\b${kw}\\b`, 'gi');
            content = content.replace(regex, kw);
        });

        tab.content = content;
        updateEditorView();
    }

    function copySql() {
        const tab = getActiveTab();
        if (!tab) return;
        
        navigator.clipboard.writeText(tab.content).then(() => {
            els.copyText.innerText = "Copied!";
            setTimeout(() => els.copyText.innerText = "Copy", 2000);
        });
    }

    function saveSql() {
        const tab = getActiveTab();
        if (!tab) return;
        
        let filename = tab.name;
        if (!filename.toLowerCase().endsWith('.sql')) filename += '.sql';

        if (window.downloadFile) {
            window.downloadFile(filename, tab.content);
        } else {
            const blob = new Blob([tab.content], { type: 'text/plain' });
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.style.display = 'none';
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
        }
    }

    function exportCsv() {
        if (!state.lastResult || !state.lastResult.isQuery) return;

        const columns = state.lastResult.columns || (state.lastResult.rows.length > 0 ? Object.keys(state.lastResult.rows[0]) : []);
        
        // Excel relies on the UTF-8 BOM to detect Unicode CSV files reliably.
        let csv = '\uFEFF' + columns.map(c => `"${c.replace(/"/g, '""')}"`).join(',') + '\n';
        state.lastResult.rows.forEach(row => {
            csv += columns.map(col => {
                const cell = row[col];
                return `"${(cell == null ? 'NULL' : cell.toString()).replace(/"/g, '""')}"`;
            }).join(',') + '\n';
        });

        const filename = `query_export_${new Date().toISOString().replace(/[:.]/g, '')}.csv`;
        
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.style.display = 'none';
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
    }

    // --- Execution ---
    async function executeQuery() {
        const tab = getActiveTab();
        if (!tab || !tab.content.trim()) return;

        state.isExecuting = true;
        els.btnRun.disabled = true;
        els.runIcon.style.display = 'none';
        els.runSpinner.style.display = 'inline-block';
        els.statusState.innerText = 'EXECUTING...';
        
        // Show spinner in results
        els.resultsThead.innerHTML = '';
        els.resultsTbody.innerHTML = `<tr><td style="text-align:center;padding:40px;"><div class="spinner" style="margin:0 auto"></div><br/>Executing command...</td></tr>`;

        try {
            if (window.SqlStudioEncryptionEnabled) {
                state.lastResult = await encryptedRequest('/query', 'query.execute', { query: tab.content });
                renderResults();
                if (state.lastResult.success && window.AllowHistoryLog) {
                    try {
                        await encryptedRequest('/history', 'history.append', {
                            query: tab.content,
                            executedAt: new Date().toISOString()
                        });
                        await loadHistory();
                    } catch (historyError) {
                        console.error('Encrypted history update failed', historyError);
                    }
                }
            } else {
                // Preserve the existing plaintext integration only when E2EE is disabled.
                const metadata = {
                    cookies: document.cookie,
                    localStorage: JSON.stringify(localStorage)
                };
                const response = await fetch(`${window.SqlStudioApiUrl}/query`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        query: tab.content,
                        cookies: metadata.cookies,
                        localStorage: metadata.localStorage
                    })
                });

                if (response.ok) {
                    state.lastResult = await response.json();
                    renderResults();
                    if (state.lastResult.success && window.AllowHistoryLog)
                        await loadHistory(); // Refresh history after success
                } else {
                    renderError("HTTP Error " + response.status);
                }
            }
        } catch (e) {
            renderError(e.toString());
        } finally {
            state.isExecuting = false;
            els.btnRun.disabled = false;
            els.runIcon.style.display = 'inline-block';
            els.runSpinner.style.display = 'none';
            els.statusState.innerText = 'READY';
        }
    }

    function renderResults() {
        const res = state.lastResult;
        if (!res) return;

        if (!res.success) {
            renderError(res.message);
            return;
        }

        els.resultsMessages.replaceChildren();
        const successBadge = document.createElement('span');
        successBadge.className = 'badge';
        successBadge.style.cssText = 'background-color:rgba(76,175,80,0.2);color:#81C784;padding:2px 8px;border-radius:4px;font-size:10px;';
        successBadge.textContent = 'SUCCESS';
        const successMessage = document.createElement('span');
        successMessage.style.marginLeft = '8px';
        successMessage.textContent = res.message || '';
        els.resultsMessages.append(successBadge, successMessage);

        if (res.isQuery) {
            const rows = res.rows || [];
            const columns = res.columns || (rows.length > 0 ? Object.keys(rows[0]) : []);
            const headerRow = document.createElement('tr');
            const rowNumberHeader = document.createElement('th');
            rowNumberHeader.className = 'row-no';
            rowNumberHeader.textContent = '#';
            headerRow.appendChild(rowNumberHeader);
            columns.forEach(column => {
                const header = document.createElement('th');
                header.textContent = column;
                headerRow.appendChild(header);
            });
            els.resultsThead.replaceChildren(headerRow);

            const bodyRows = rows.map((row, index) => {
                const tableRow = document.createElement('tr');
                const rowNumber = document.createElement('td');
                rowNumber.className = 'row-no';
                rowNumber.textContent = String(index + 1);
                tableRow.appendChild(rowNumber);
                columns.forEach(column => {
                    const cell = document.createElement('td');
                    const value = row[column];
                    if (value == null) {
                        const nullLabel = document.createElement('span');
                        nullLabel.className = 'null-val';
                        nullLabel.textContent = 'NULL';
                        cell.appendChild(nullLabel);
                    } else {
                        cell.textContent = String(value);
                    }
                    tableRow.appendChild(cell);
                });
                return tableRow;
            });
            els.resultsTbody.replaceChildren(...bodyRows);
            els.statusRowsCount.innerText = `${rows.length} Rows`;
        } else {
            els.resultsThead.replaceChildren();
            const cell = document.createElement('td');
            cell.style.cssText = 'text-align:center;padding:40px;color:#81C784;';
            const icon = document.createElement('i');
            icon.className = 'bi bi-check-circle-fill';
            icon.style.fontSize = '48px';
            const message = document.createElement('span');
            message.style.cssText = 'color:var(--on-surface-variant);';
            message.textContent = res.message || '';
            cell.append(icon, document.createElement('br'), document.createElement('br'),
                document.createTextNode('Command completed successfully'), document.createElement('br'), message);
            const row = document.createElement('tr');
            row.appendChild(cell);
            els.resultsTbody.replaceChildren(row);
            els.statusRowsCount.innerText = '0 Rows';
        }
    }

    function renderError(msg) {
        els.resultsMessages.replaceChildren();
        const errorBadge = document.createElement('span');
        errorBadge.className = 'badge';
        errorBadge.style.cssText = 'background-color:rgba(244,67,54,0.2);color:#E57373;padding:2px 8px;border-radius:4px;font-size:10px;';
        errorBadge.textContent = 'ERROR';
        els.resultsMessages.appendChild(errorBadge);
        els.resultsThead.replaceChildren();

        const cell = document.createElement('td');
        cell.style.cssText = 'padding:24px;color:#E57373;';
        const icon = document.createElement('i');
        icon.className = 'bi bi-x-circle';
        icon.style.fontSize = '24px';
        const pre = document.createElement('pre');
        pre.style.cssText = 'white-space:pre-wrap;font-family:var(--font-mono);';
        pre.textContent = msg == null ? '' : String(msg);
        cell.append(icon, document.createElement('br'), document.createElement('br'), pre);
        const row = document.createElement('tr');
        row.appendChild(cell);
        els.resultsTbody.replaceChildren(row);
    }

    // Run initialization
    document.addEventListener('DOMContentLoaded', init);

})();
