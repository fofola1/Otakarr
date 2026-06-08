document.addEventListener('DOMContentLoaded', () => {
    // State values
    let indexerApiKey = '';
    
    // Initialise Icons
    lucide.createIcons();

    // DOM Elements
    const serviceStatusEl = document.getElementById('service-status');
    const downloaderUrlEl = document.getElementById('downloader-url');
    const apiKeyStatusEl = document.getElementById('api-key-status');
    const scrapersListEl = document.getElementById('scrapers-list');
    const runningPortEl = document.getElementById('running-port');
    const setupUrlInput = document.getElementById('setup-url');
    const setupApiKeyInput = document.getElementById('setup-apikey');
    const copyApiKeyBtn = document.getElementById('copy-apikey-btn');
    
    const searchForm = document.getElementById('search-form');
    const queryInput = document.getElementById('query-input');
    const seasonInput = document.getElementById('season-input');
    const episodeInput = document.getElementById('episode-input');
    const searchResultsSection = document.getElementById('search-results-section');
    const resultsCountEl = document.getElementById('results-count');
    const resultsTbody = document.getElementById('results-tbody');
    
    const payloadInput = document.getElementById('payload-input');
    const decodeBtn = document.getElementById('decode-btn');
    const clearDecoderBtn = document.getElementById('clear-decoder-btn');
    const decoderErrorEl = document.getElementById('decoder-error');
    const decoderOutputWrapper = document.getElementById('decoder-output-wrapper');
    const decoderOutputEl = document.getElementById('decoder-output');

    // Helper: Format size
    function formatBytes(bytes) {
        if (!bytes || bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    // Helper: Copy to Clipboard
    async function copyToClipboard(text, button) {
        try {
            await navigator.clipboard.writeText(text);
            const originalIcon = button.innerHTML;
            button.innerHTML = '<i data-lucide="check" style="color: var(--success); width: 1.25rem; height: 1.25rem;"></i>';
            lucide.createIcons();
            setTimeout(() => {
                button.innerHTML = originalIcon;
                lucide.createIcons();
            }, 1500);
        } catch (err) {
            console.error('Failed to copy: ', err);
        }
    }

    // Load System Configuration
    async function loadConfig() {
        try {
            const res = await fetch('/api/config-status');
            if (!res.ok) throw new Error('Failed to load status');
            const data = await res.json();

            // Render stats
            serviceStatusEl.textContent = 'Active / Online';
            serviceStatusEl.parentElement.classList.add('active');
            
            downloaderUrlEl.textContent = data.downloaderUrl;
            downloaderUrlEl.title = data.downloaderUrl;
            
            runningPortEl.textContent = data.port;
            
            scrapersListEl.textContent = data.activeScrapers.join(', ') || 'None';

            if (data.apiKeyConfigured) {
                apiKeyStatusEl.textContent = 'API Key Active';
                apiKeyStatusEl.className = 'badge badge-success';
                
                // Prompt user to fetch/define key or grab from query
                const urlParams = new URLSearchParams(window.location.search);
                const queryKey = urlParams.get('apikey') || '';
                
                if (queryKey) {
                    indexerApiKey = queryKey;
                    setupApiKeyInput.value = queryKey;
                    copyApiKeyBtn.disabled = false;
                } else {
                    setupApiKeyInput.value = '•••••••• (See .env / URL)';
                    copyApiKeyBtn.disabled = true;
                }
            } else {
                apiKeyStatusEl.textContent = 'Disabled (No Key)';
                apiKeyStatusEl.className = 'badge badge-warning';
                setupApiKeyInput.value = 'None (Security Disabled)';
                copyApiKeyBtn.disabled = true;
            }

            // Set setup URL
            const protocol = window.location.protocol;
            const host = window.location.host;
            setupUrlInput.value = `${protocol}//${host}/api`;

        } catch (err) {
            serviceStatusEl.textContent = 'Offline / Error';
            serviceStatusEl.parentElement.querySelector('.pulse-dot').style.backgroundColor = 'var(--error)';
            serviceStatusEl.parentElement.querySelector('.pulse-dot').style.boxShadow = '0 0 10px var(--error)';
            console.error('Config fetch failed:', err);
        }
    }

    // Interactive Scraper Search
    searchForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        
        const q = queryInput.value.trim();
        const season = seasonInput.value;
        const ep = episodeInput.value;
        
        const submitBtn = searchForm.querySelector('button[type="submit"]');
        const btnText = submitBtn.querySelector('.btn-text');
        const spinner = submitBtn.querySelector('.spinner');

        // Toggle Loading State
        submitBtn.disabled = true;
        btnText.textContent = 'Searching...';
        spinner.classList.remove('hidden');

        try {
            // Build query params
            const params = new URLSearchParams();
            if (q) params.append('q', q);
            if (season) params.append('season', season);
            if (ep) params.append('ep', ep);

            const res = await fetch(`/api/search-json?${params.toString()}`);
            if (!res.ok) throw new Error('Search request failed');
            
            const results = await res.json();
            renderSearchResults(results);
        } catch (err) {
            console.error('Search failed:', err);
            resultsTbody.innerHTML = `<tr><td colspan="5" class="error-msg" style="text-align: center;">Search failed to execute. Ensure backend is running.</td></tr>`;
            searchResultsSection.classList.remove('hidden');
        } finally {
            submitBtn.disabled = false;
            btnText.textContent = 'Search Scrapers';
            spinner.classList.add('hidden');
        }
    });

    // Render Search Results Table
    function renderSearchResults(results) {
        resultsCountEl.textContent = results.length;
        resultsTbody.innerHTML = '';

        if (results.length === 0) {
            resultsTbody.innerHTML = `<tr><td colspan="5" style="text-align: center; color: var(--text-muted); padding: 2rem;">No matching streams found for this query.</td></tr>`;
        } else {
            results.forEach((item, index) => {
                // Generate the enclosure download URL for copying
                const protocol = window.location.protocol;
                const host = window.location.host;
                const indexerUrl = `${protocol}//${host}/api`;
                
                // Construct payload JSON object
                const payloadObj = {
                    site: item.scraperName,
                    id: item.guid,
                    title: item.title,
                    season: item.season,
                    ep: item.episode,
                    stream_url: item.url,
                    resolution: item.resolution,
                    source: item.source
                };
                
                // Base64 encode the payload (URL safe)
                const jsonStr = JSON.stringify(payloadObj);
                const base64Str = btoa(unescape(encodeURIComponent(jsonStr)))
                    .replace(/\+/g, '-')
                    .replace(/\//g, '_')
                    .replace(/=+$/, '');
                
                const downloaderBase = downloaderUrlEl.textContent !== 'Loading...' ? downloaderUrlEl.textContent : 'http://localhost:8080/download';
                const separator = downloaderBase.includes('?') ? '&' : '?';
                const fullEnclosureUrl = `${downloaderBase}${separator}payload=${base64Str}`;

                const tr = document.createElement('tr');
                tr.innerHTML = `
                    <td style="font-weight: 500;">${item.title}</td>
                    <td><span class="tag">${item.resolution}</span></td>
                    <td>${formatBytes(item.size)}</td>
                    <td><span style="color: var(--cyan);">${item.source}</span></td>
                    <td>
                        <div style="display: flex; gap: 0.5rem;">
                            <button class="btn btn-secondary action-copy-url" data-url="${fullEnclosureUrl}" title="Copy Enclosure URL">
                                <i data-lucide="copy" style="width: 14px; height: 14px;"></i> Copy URL
                            </button>
                            <button class="btn btn-secondary action-decode" data-payload="${base64Str}" title="Decode and inspect metadata">
                                <i data-lucide="cpu" style="width: 14px; height: 14px;"></i> Inspect
                            </button>
                        </div>
                    </td>
                `;
                resultsTbody.appendChild(tr);
            });

            // Bind Actions on newly rendered buttons
            document.querySelectorAll('.action-copy-url').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const url = btn.getAttribute('data-url');
                    copyToClipboard(url, btn);
                });
            });

            document.querySelectorAll('.action-decode').forEach(btn => {
                btn.addEventListener('click', () => {
                    const payload = btn.getAttribute('data-payload');
                    payloadInput.value = payload;
                    decodePayload(payload);
                    document.getElementById('payload-input').scrollIntoView({ behavior: 'smooth' });
                });
            });
        }

        searchResultsSection.classList.remove('hidden');
        lucide.createIcons();
    }

    // Decode Base64 Payload
    function decodePayload(inputStr) {
        decoderErrorEl.classList.add('hidden');
        decoderOutputWrapper.classList.add('hidden');
        
        if (!inputStr) {
            decoderErrorEl.textContent = 'Please enter a URL or a base64 payload string.';
            decoderErrorEl.classList.remove('hidden');
            return;
        }

        let base64 = inputStr.trim();

        // 1. If it's a full URL, extract the payload parameter
        if (base64.startsWith('http://') || base64.startsWith('https://')) {
            try {
                const urlObj = new URL(base64);
                const extracted = urlObj.searchParams.get('payload');
                if (!extracted) {
                    throw new Error('URL does not contain a "payload" query parameter.');
                }
                base64 = extracted;
            } catch (err) {
                decoderErrorEl.textContent = err.message || 'Invalid URL entered.';
                decoderErrorEl.classList.remove('hidden');
                return;
            }
        }

        // 2. Unescape URL-safe base64 characters
        base64 = base64.replace(/-/g, '+').replace(/_/g, '/');
        
        // 3. Restore padding if missing
        const paddingNeeded = (4 - (base64.Length % 4)) % 4;
        base64 += '='.repeat(paddingNeeded);

        try {
            // 4. Decode base64 to string
            const decodedJsonStr = decodeURIComponent(escape(atob(base64)));
            
            // 5. Format and show JSON
            const jsonObj = JSON.parse(decodedJsonStr);
            decoderOutputEl.textContent = JSON.stringify(jsonObj, null, 2);
            decoderOutputWrapper.classList.remove('hidden');
        } catch (err) {
            decoderErrorEl.textContent = 'Decoding failed: The string is not a valid Base64 encoded JSON string.';
            decoderErrorEl.classList.remove('hidden');
        }
    }

    // Bind event listeners for Decoder
    decodeBtn.addEventListener('click', () => {
        decodePayload(payloadInput.value);
    });

    clearDecoderBtn.addEventListener('click', () => {
        payloadInput.value = '';
        decoderErrorEl.classList.add('hidden');
        decoderOutputWrapper.classList.add('hidden');
    });

    // Copy setup inputs
    document.querySelectorAll('.copy-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-target');
            let text = '';
            if (targetId === 'decoder-output') {
                text = document.getElementById(targetId).textContent;
            } else {
                text = document.getElementById(targetId).value;
            }
            copyToClipboard(text, btn);
        });
    });

    // Initial Loading
    loadConfig();
});
