(function () {
    const container = document.getElementById('chatMessages');
    if (!container) return;

    const threadId = container.dataset.threadId;
    let lastId = parseInt(container.dataset.lastId || '0', 10);

    const scrollToBottom = () => {
        container.scrollTop = container.scrollHeight;
    };

    const appendMessage = (msg) => {
        if (document.querySelector(`[data-id="${msg.id}"]`)) return;
        const bubble = document.createElement('div');
        bubble.className = 'vendor-chat-bubble ' + (msg.isMine ? 'mine' : 'theirs');
        bubble.dataset.id = msg.id;
        bubble.innerHTML = `
            <div class="vendor-chat-meta">${msg.isMine ? 'You' : (msg.senderName || 'Vendor')} · ${new Date(msg.createdAt).toLocaleString()}</div>
            <div class="vendor-chat-text"></div>`;
        bubble.querySelector('.vendor-chat-text').textContent = msg.content;
        container.appendChild(bubble);
        lastId = Math.max(lastId, msg.id);
        scrollToBottom();
    };

    scrollToBottom();

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
