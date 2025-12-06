window.BlazorMauiCssLiveReload = {
    socket: null,
    reconnectAttempts: 0,
    maxReconnectAttempts: 10,
    reconnectDelay: 1000,
    scopedCssStore: {},

    applyCss(cssContent) {
        const id = 'blazor-css-live-reload-style';
        let style = document.getElementById(id);

        if (!style) {
            style = document.createElement('style');
            style.id = id;
            document.head.appendChild(style);
        }

        style.textContent = cssContent;
        void document.body.offsetHeight;

        console.log("[LiveReload] Normal CSS updated:", cssContent.length);
    },

    applyScopedCssBundle(bundleContent) {
        const componentId = this.extractComponentId(bundleContent);

        const cleanedContent = bundleContent
            .replace(/::deep\s+/gi, "")
            .replace(/::slotted\s+/gi, "")
            .replace(/\/\*.*?\*\//gs, "");

        this.scopedCssStore[componentId] = cleanedContent;

        this.updateScopedCssBundle();

        console.log("[LiveReload] Scoped CSS updated for component:", componentId);
    },

    extractComponentId(bundleContent) {
        const match = bundleContent.match(/\[b-[a-z0-9]+\]/);
        return match ? match[0] : `component-${Date.now()}`;
    },

    updateScopedCssBundle() {
        const id = 'blazor-scoped-css-bundle';
        let style = document.getElementById(id);

        if (!style) {
            style = document.createElement('style');
            style.id = id;
            
            document.head.appendChild(style);
        }

        const allScopedCss = Object.values(this.scopedCssStore).join('\n');
        style.textContent = allScopedCss;

        void document.body.offsetHeight;

        console.log("[LiveReload] Scoped CSS bundle updated:", allScopedCss.length, "bytes,", Object.keys(this.scopedCssStore).length, "components");
    },

    start(serverUrl) {
        console.log("[LiveReload] start() called with serverUrl:", serverUrl);
        console.log("[LiveReload] window.BlazorMauiCssLiveReload exists:", !!window.BlazorMauiCssLiveReload);
        
        this.reconnectAttempts = 0;
        this.connect(serverUrl);
    },

    connect(serverUrl) {
        if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            console.error("[LiveReload] Max reconnection attempts reached");
            return;
        }

        try {
            console.log(`[LiveReload] Connecting to ${serverUrl} (attempt ${this.reconnectAttempts + 1})`);
            
            this.socket = new WebSocket(serverUrl);

            this.socket.onopen = () => {
                console.log("[LiveReload] ✓ WebSocket connected successfully");
                this.reconnectAttempts = 0;
            };

            this.socket.onmessage = (e) => {
                const msg = e.data;
                console.log("[LiveReload] ✓ Message received, length:", msg.length);
                console.log("[LiveReload] Message preview:", msg.substring(0, 100));

                if (typeof msg === 'string') {
                    if (msg.startsWith("NORMAL:")) {
                        const content = msg.substring(7);
                        if (content.includes('[b-')) {
                            console.log("[LiveReload] → Detected SCOPED CSS (mislabeled as NORMAL), applying as SCOPED");
                            window.BlazorMauiCssLiveReload.applyScopedCssBundle(content);
                        } else {
                            console.log("[LiveReload] → Applying NORMAL CSS");
                            window.BlazorMauiCssLiveReload.applyCss(content);
                        }
                    } else if (msg.startsWith("SCOPED:")) {
                        console.log("[LiveReload] → Applying SCOPED CSS");
                        window.BlazorMauiCssLiveReload.applyScopedCssBundle(msg.substring(7));
                    } else {
                        console.warn("[LiveReload] ⚠ Unknown message format");
                    }
                }
            };

            this.socket.onerror = (err) => {
                console.error("[LiveReload] ✗ WebSocket error:", err);
            };

            this.socket.onclose = (event) => {
                console.log(`[LiveReload] ✗ WebSocket closed. Code: ${event.code}, Clean: ${event.wasClean}`);
                
                if (this.reconnectAttempts < this.maxReconnectAttempts) {
                    this.reconnectAttempts++;
                    const delay = this.reconnectDelay * Math.pow(2, Math.min(this.reconnectAttempts - 1, 3));
                    console.log(`[LiveReload] → Reconnecting in ${delay}ms...`);
                    setTimeout(() => this.connect(serverUrl), delay);
                }
            };
        } catch (error) {
            console.error("[LiveReload] ✗ WebSocket creation error:", error.message);
            if (this.reconnectAttempts < this.maxReconnectAttempts) {
                this.reconnectAttempts++;
                const delay = this.reconnectDelay * Math.pow(2, Math.min(this.reconnectAttempts - 1, 3));
                setTimeout(() => this.connect(serverUrl), delay);   
            }
        }
    }
};

console.log("[LiveReload] Object initialized:", !!window.BlazorMauiCssLiveReload.start);
