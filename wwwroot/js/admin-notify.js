(function () {
    if (!document.body.classList.contains('admin-body')) return;

    const STORAGE_KEY = 'bloghub_notify_state';

    const playTone = (freq, duration, type = 'sine') => {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = type;
            osc.frequency.value = freq;
            gain.gain.value = 0.08;
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            setTimeout(() => { osc.stop(); ctx.close(); }, duration);
        } catch { /* audio not supported */ }
    };

    const playRequestSound = () => {
        playTone(880, 120);
        setTimeout(() => playTone(1046, 150), 130);
    };

    const playAcceptSound = () => {
        playTone(523, 100);
        setTimeout(() => playTone(659, 100), 110);
        setTimeout(() => playTone(784, 180), 220);
    };

    const loadState = () => {
        try { return JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}'); }
        catch { return {}; }
    };

    const saveState = (state) => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    };

    const showToast = (message) => {
        let el = document.getElementById('adminNotifyToast');
        if (!el) {
            el = document.createElement('div');
            el.id = 'adminNotifyToast';
            el.style.cssText = 'position:fixed;bottom:1.25rem;right:1.25rem;z-index:9999;background:#1e293b;color:#fff;padding:0.85rem 1.15rem;border-radius:10px;box-shadow:0 8px 24px rgba(0,0,0,0.2);font-size:0.875rem;max-width:320px;opacity:0;transition:opacity 0.25s;';
            document.body.appendChild(el);
        }
        el.textContent = message;
        el.style.opacity = '1';
        clearTimeout(el._hideTimer);
        el._hideTimer = setTimeout(() => { el.style.opacity = '0'; }, 4500);
    };

    const poll = async () => {
        try {
            const res = await fetch('/VendorNetwork/GetNotifications');
            if (!res.ok) return;
            const json = await res.json();
            if (!json.success) return;

            const state = loadState();
            const isFirstRun = state.pendingIncoming === undefined;

            if (isFirstRun) {
                saveState({
                    pendingIncoming: json.pendingIncoming,
                    knownAcceptedIds: (json.acceptedOutgoing || []).map(i => i.id)
                });
                return;
            }

            const prevPending = state.pendingIncoming ?? 0;
            const knownAccepted = new Set(state.knownAcceptedIds || []);

            if (json.pendingIncoming > prevPending) {
                playRequestSound();
                showToast('New vendor connection request received!');
            }

            (json.acceptedOutgoing || []).forEach(item => {
                if (!knownAccepted.has(item.id)) {
                    playAcceptSound();
                    showToast(`${item.toUserName || 'Vendor'} accepted your connection request!`);
                    knownAccepted.add(item.id);
                }
            });

            saveState({
                pendingIncoming: json.pendingIncoming,
                knownAcceptedIds: Array.from(knownAccepted)
            });
        } catch { /* ignore */ }
    };

    poll();
    setInterval(poll, 12000);
})();
