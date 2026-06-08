(function () {
    const container = document.getElementById('chatMessages');
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatInput');
    if (!container) return;

    const threadId = container.dataset.threadId;
    let lastId = parseInt(container.dataset.lastId || '0', 10);

    const scrollToBottom = () => {
        container.scrollTop = container.scrollHeight;
    };

    const appendMessage = (msg) => {
        if (document.querySelector(`.vendor-chat-bubble[data-id="${msg.id}"]`)) return;

        const isMine = msg.isMine === true;
        const row = document.createElement('div');
        row.className = 'vendor-chat-row ' + (isMine ? 'mine' : 'theirs');

        const bubble = document.createElement('div');
        bubble.className = 'vendor-chat-bubble';
        bubble.dataset.id = msg.id;

        const when = msg.createdAt
            ? new Date(msg.createdAt).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
            : '';

        bubble.innerHTML = `
            <div class="vendor-chat-meta">${isMine ? 'You' : (msg.senderName || 'Vendor')} · ${when}</div>
            <div class="vendor-chat-text"></div>`;
        bubble.querySelector('.vendor-chat-text').textContent = msg.content;

        row.appendChild(bubble);
        container.appendChild(row);
        lastId = Math.max(lastId, msg.id);
        scrollToBottom();
    };

    scrollToBottom();

    if (form && input) {
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const content = input.value.trim();
            if (!content) return;

            const btn = form.querySelector('button[type="submit"]');
            if (btn) btn.disabled = true;

            try {
                const body = new URLSearchParams(new FormData(form));
                const res = await fetch(form.action, {
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    body
                });

                if (!res.ok) {
                    form.submit();
                    return;
                }

                const json = await res.json();
                if (json.success && json.data) {
                    appendMessage({
                        id: json.data.id,
                        content: json.data.content,
                        createdAt: json.data.createdAt,
                        senderName: 'You',
                        isMine: true
                    });
                    input.value = '';
                } else {
                    form.submit();
                }
            } catch {
                form.submit();
            } finally {
                if (btn) btn.disabled = false;
            }
        });
    }

    setInterval(async () => {
        try {
            const res = await fetch(`/VendorNetwork/GetMessages?threadId=${threadId}&afterId=${lastId}`);
            if (!res.ok) return;
            const json = await res.json();
            if (json.success && json.data?.length) {
                json.data.forEach(appendMessage);
            }
        } catch { /* ignore polling errors */ }
    }, 4000);
})();
