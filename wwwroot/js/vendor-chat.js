(function () {
    const container = document.getElementById('chatMessages');
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatInput');
    if (!container) return;

    const threadId = container.dataset.threadId;
    const currentUser = container.dataset.currentUser || 'You';
    let lastId = parseInt(container.dataset.lastId || '0', 10);

    const getToken = () => form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const scrollToBottom = () => {
        container.scrollTop = container.scrollHeight;
    };

    const formatTime = (value) => {
        if (!value) return '';
        return new Date(value).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
    };

    const buildActionsHtml = (msg) => {
        if (msg.deletedForEveryone) return '';
        const isMine = msg.isMine === true;
        let html = '<div class="vendor-chat-msg-actions">';
        if (isMine) {
            html += `<button type="button" class="icon-btn chat-edit-btn" title="Edit message" data-id="${msg.id}">
                <svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
            </button>`;
            html += `<button type="button" class="icon-btn icon-btn-danger chat-delete-all-btn" title="Delete for everyone" data-id="${msg.id}">
                <svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/></svg>
            </button>`;
        }
        html += `<button type="button" class="icon-btn chat-hide-btn" title="Delete for me" data-id="${msg.id}">
            <svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
        </button></div>`;
        return html;
    };

    const renderMessageHtml = (msg) => {
        const isMine = msg.isMine === true;
        const deleted = msg.deletedForEveryone === true;
        const senderName = msg.senderName || (isMine ? currentUser : 'Vendor');
        const edited = msg.editedAt ? '<span class="chat-edited-tag">edited</span>' : '';
        const text = deleted ? 'This message was deleted' : (msg.content || '');
        const textClass = deleted ? 'vendor-chat-text chat-deleted-text' : 'vendor-chat-text';

        return `
        <div class="vendor-chat-row ${isMine ? 'mine' : 'theirs'}" data-row-id="${msg.id}">
            <div class="vendor-chat-bubble" data-id="${msg.id}" data-mine="${isMine}">
                <div class="vendor-chat-meta">
                    <strong class="chat-sender-name">${senderName}</strong>
                    <span>· ${formatTime(msg.createdAt)}</span>
                    ${edited}
                </div>
                <div class="${textClass}"></div>
                ${deleted ? '' : buildActionsHtml(msg)}
            </div>
        </div>`;
    };

    const appendMessage = (msg) => {
        if (document.querySelector(`.vendor-chat-row[data-row-id="${msg.id}"]`)) return;

        container.insertAdjacentHTML('beforeend', renderMessageHtml(msg));
        const row = container.querySelector(`.vendor-chat-row[data-row-id="${msg.id}"]`);
        const textEl = row?.querySelector('.vendor-chat-text, .chat-deleted-text');
        if (textEl && !msg.deletedForEveryone) textEl.textContent = msg.content;

        lastId = Math.max(lastId, msg.id);
        scrollToBottom();
    };

    const postAction = async (url, body) => {
        const params = new URLSearchParams(body);
        params.append('__RequestVerificationToken', getToken());
        const res = await fetch(url, { method: 'POST', body: params });
        if (!res.ok) throw new Error('Request failed');
        return res.json();
    };

    const markDeleted = (id) => {
        const row = document.querySelector(`.vendor-chat-row[data-row-id="${id}"]`);
        if (!row) return;
        const textEl = row.querySelector('.vendor-chat-text');
        if (textEl) {
            textEl.textContent = 'This message was deleted';
            textEl.classList.add('chat-deleted-text');
        }
        row.querySelector('.vendor-chat-msg-actions')?.remove();
        row.querySelector('.chat-edited-tag')?.remove();
    };

    const removeRow = (id) => {
        document.querySelector(`.vendor-chat-row[data-row-id="${id}"]`)?.remove();
    };

    container.addEventListener('click', async (e) => {
        const editBtn = e.target.closest('.chat-edit-btn');
        const hideBtn = e.target.closest('.chat-hide-btn');
        const deleteAllBtn = e.target.closest('.chat-delete-all-btn');

        if (editBtn) {
            const id = editBtn.dataset.id;
            const bubble = editBtn.closest('.vendor-chat-bubble');
            const textEl = bubble?.querySelector('.vendor-chat-text');
            const current = textEl?.textContent || '';
            const updated = window.prompt('Edit message:', current);
            if (updated === null || updated.trim() === '' || updated.trim() === current.trim()) return;
            try {
                const json = await postAction('/VendorNetwork/EditMessage', { messageId: id, content: updated.trim() });
                if (json.success && textEl) {
                    textEl.textContent = updated.trim();
                    let tag = bubble.querySelector('.chat-edited-tag');
                    if (!tag) {
                        tag = document.createElement('span');
                        tag.className = 'chat-edited-tag';
                        tag.textContent = 'edited';
                        bubble.querySelector('.vendor-chat-meta')?.appendChild(tag);
                    }
                }
            } catch { alert('Could not edit message.'); }
            return;
        }

        if (hideBtn) {
            const id = hideBtn.dataset.id;
            if (!confirm('Delete this message for you?')) return;
            try {
                const json = await postAction('/VendorNetwork/HideMessage', { messageId: id });
                if (json.success) removeRow(id);
            } catch { alert('Could not hide message.'); }
            return;
        }

        if (deleteAllBtn) {
            const id = deleteAllBtn.dataset.id;
            if (!confirm('Delete this message for everyone?')) return;
            try {
                const json = await postAction('/VendorNetwork/DeleteMessageForEveryone', { messageId: id });
                if (json.success) markDeleted(id);
            } catch { alert('Could not delete message.'); }
        }
    });

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

                if (!res.ok) { form.submit(); return; }

                const json = await res.json();
                const data = json.data || json.Data;
                if ((json.success || json.Success) && data) {
                    appendMessage({
                        id: data.id ?? data.Id,
                        content: data.content ?? data.Content,
                        createdAt: data.createdAt ?? data.CreatedAt,
                        senderName: data.senderName ?? data.SenderName ?? currentUser,
                        isMine: true,
                        deletedForEveryone: false
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
        } catch { /* ignore */ }
    }, 4000);
})();
