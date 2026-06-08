(function () {
    const container = document.getElementById('chatMessages');
    const form = document.getElementById('chatForm');
    const input = document.getElementById('chatInput');
    const contextMenu = document.getElementById('chatContextMenu');
    const deleteModal = document.getElementById('chatDeleteModal');
    const replyBar = document.getElementById('chatReplyBar');
    const replyToName = document.getElementById('chatReplyToName');
    const replyToText = document.getElementById('chatReplyToText');
    const replyCancel = document.getElementById('chatReplyCancel');

    if (!container) return;

    const threadId = container.dataset.threadId;
    const currentUser = container.dataset.currentUser || 'You';
    const currentUserId = container.dataset.currentUserId || '';
    let lastId = parseInt(container.dataset.lastId || '0', 10);
    let activeBubble = null;
    let deleteTargetId = null;
    let deleteTargetIsMine = false;
    let replyTarget = null;

    const deleteAllBtn = document.getElementById('chatDeleteAllBtn');

    const isOwnMessage = (bubble) => {
        const senderId = bubble?.dataset.senderId;
        return !!(currentUserId && senderId && senderId === currentUserId);
    };

    const icons = {
        reply: '<svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M3 10h10a5 5 0 015 5v2M3 10l4-4M3 10l4 4"/></svg>',
        copy: '<svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/></svg>',
        edit: '<svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>',
        delete: '<svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/></svg>'
    };

    const getToken = () => form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const scrollToBottom = () => { container.scrollTop = container.scrollHeight; };

    const formatTime = (value) => {
        if (!value) return '';
        return new Date(value).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
    };

    const escapeHtml = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    const escapeAttr = (s) => escapeHtml(s).replace(/"/g, '&quot;');

    const renderMessageHtml = (msg) => {
        const isMine = msg.senderUserId
            ? msg.senderUserId === currentUserId
            : msg.isMine === true;
        const deleted = msg.deletedForEveryone === true;
        const senderName = escapeHtml(msg.senderName || (isMine ? currentUser : 'Vendor'));
        const content = deleted ? '' : escapeAttr(msg.content || '');
        const text = deleted ? 'This message was deleted' : (msg.content || '');
        const edited = msg.editedAt ? '<span class="chat-edited-tag">edited</span>' : '';

        return `
        <div class="vendor-chat-row ${isMine ? 'mine' : 'theirs'}" data-row-id="${msg.id}">
            <div class="vendor-chat-bubble chat-bubble-selectable"
                 data-id="${msg.id}"
                 data-sender-id="${escapeAttr(msg.senderUserId || '')}"
                 data-mine="${isMine}"
                 data-sender="${senderName}"
                 data-content="${content}"
                 data-deleted="${deleted}">
                <div class="vendor-chat-meta">
                    <strong class="chat-sender-name">${senderName}</strong>
                    <span>· ${formatTime(msg.createdAt)}</span>
                    ${edited}
                </div>
                <div class="vendor-chat-text ${deleted ? 'chat-deleted-text' : ''}">${escapeHtml(text)}</div>
            </div>
        </div>`;
    };

    const appendMessage = (msg) => {
        if (document.querySelector(`.vendor-chat-row[data-row-id="${msg.id}"]`)) return;
        container.insertAdjacentHTML('beforeend', renderMessageHtml(msg));
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
        const bubble = document.querySelector(`.vendor-chat-bubble[data-id="${id}"]`);
        if (!bubble) return;
        bubble.dataset.deleted = 'true';
        bubble.dataset.content = '';
        const textEl = bubble.querySelector('.vendor-chat-text');
        if (textEl) {
            textEl.textContent = 'This message was deleted';
            textEl.classList.add('chat-deleted-text');
        }
        bubble.querySelector('.chat-edited-tag')?.remove();
    };

    const removeRow = (id) => {
        document.querySelector(`.vendor-chat-row[data-row-id="${id}"]`)?.remove();
    };

    const closeMenu = () => {
        contextMenu?.classList.add('hidden');
        activeBubble?.classList.remove('chat-bubble-active');
        activeBubble = null;
    };

    const closeDeleteModal = () => {
        deleteModal?.classList.add('hidden');
        deleteTargetId = null;
    };

    const showDeleteModal = (id, isMine) => {
        deleteTargetId = id;
        deleteTargetIsMine = isMine;
        if (deleteAllBtn) {
            deleteAllBtn.style.display = isMine ? 'block' : 'none';
        }
        deleteModal?.classList.remove('hidden');
    };

    const buildMenuItems = (bubble) => {
        const own = isOwnMessage(bubble);
        const deleted = bubble.dataset.deleted === 'true';
        if (deleted) return [];

        const items = [
            { action: 'reply', label: 'Reply', icon: icons.reply },
            { action: 'copy', label: 'Copy', icon: icons.copy }
        ];
        if (own) {
            items.push({ action: 'edit', label: 'Edit', icon: icons.edit });
        }
        items.push({ action: 'delete', label: 'Delete', icon: icons.delete, danger: true });
        return items;
    };

    const showMenu = (bubble, x, y) => {
        closeMenu();
        activeBubble = bubble;
        bubble.classList.add('chat-bubble-active');

        const items = buildMenuItems(bubble);
        if (!items.length) return;

        contextMenu.innerHTML = items.map(i => `
            <button type="button" class="chat-menu-item ${i.danger ? 'danger' : ''}" data-action="${i.action}">
                ${i.icon}<span>${i.label}</span>
            </button>`).join('');

        contextMenu.classList.remove('hidden');

        const menuW = contextMenu.offsetWidth || 200;
        const menuH = contextMenu.offsetHeight || 180;
        const pad = 8;
        let left = Math.min(x, window.innerWidth - menuW - pad);
        let top = Math.min(y, window.innerHeight - menuH - pad);
        left = Math.max(pad, left);
        top = Math.max(pad, top);
        contextMenu.style.left = left + 'px';
        contextMenu.style.top = top + 'px';
    };

    const setReply = (bubble) => {
        replyTarget = {
            id: bubble.dataset.id,
            sender: bubble.dataset.sender,
            content: bubble.dataset.content
        };
        if (replyBar && replyToName && replyToText) {
            replyToName.textContent = replyTarget.sender;
            replyToText.textContent = replyTarget.content.length > 80
                ? replyTarget.content.slice(0, 80) + '…'
                : replyTarget.content;
            replyBar.classList.remove('hidden');
        }
        input?.focus();
    };

    const clearReply = () => {
        replyTarget = null;
        replyBar?.classList.add('hidden');
    };

    const editMessage = async (id, bubble) => {
        if (!isOwnMessage(bubble)) {
            alert('You can only edit your own messages.');
            return;
        }
        const textEl = bubble.querySelector('.vendor-chat-text');
        const current = bubble.dataset.content || textEl?.textContent || '';
        const updated = window.prompt('Edit message:', current);
        if (updated === null || updated.trim() === '' || updated.trim() === current.trim()) return;
        try {
            const json = await postAction('/VendorNetwork/EditMessage', { messageId: id, content: updated.trim() });
            if (json.success && textEl) {
                textEl.textContent = updated.trim();
                bubble.dataset.content = updated.trim();
                let tag = bubble.querySelector('.chat-edited-tag');
                if (!tag) {
                    tag = document.createElement('span');
                    tag.className = 'chat-edited-tag';
                    tag.textContent = 'edited';
                    bubble.querySelector('.vendor-chat-meta')?.appendChild(tag);
                }
            } else if (json.message || json.Message) {
                alert(json.message || json.Message);
            }
        } catch { alert('Could not edit message.'); }
    };

    const sendMessage = async () => {
        let content = input?.value.trim() || '';
        if (!content) return;

        if (replyTarget) {
            content = `↩ ${replyTarget.sender}: ${replyTarget.content}\n\n${content}`;
        }

        const btn = form?.querySelector('button[type="submit"]');
        if (btn) btn.disabled = true;

        try {
            const body = new URLSearchParams(new FormData(form));
            body.set('content', content);

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
                    senderUserId: data.senderUserId ?? data.SenderUserId ?? currentUserId,
                    isMine: true,
                    deletedForEveryone: false
                });
                input.value = '';
                clearReply();
            } else {
                form.submit();
            }
        } catch {
            form.submit();
        } finally {
            if (btn) btn.disabled = false;
        }
    };

    // Message click → WhatsApp-style menu
    container.addEventListener('click', (e) => {
        const bubble = e.target.closest('.chat-bubble-selectable');
        if (!bubble || bubble.dataset.deleted === 'true') return;
        e.preventDefault();
        showMenu(bubble, e.clientX, e.clientY);
    });

    contextMenu?.addEventListener('click', async (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn || !activeBubble) return;

        const action = btn.dataset.action;
        const id = activeBubble.dataset.id;
        const content = activeBubble.dataset.content;

        closeMenu();

        if (action === 'reply') {
            setReply(activeBubble);
        } else if (action === 'copy') {
            try {
                await navigator.clipboard.writeText(content);
            } catch {
                window.prompt('Copy:', content);
            }
        } else if (action === 'edit') {
            await editMessage(id, activeBubble);
        } else if (action === 'delete') {
            showDeleteModal(id, isOwnMessage(activeBubble));
        }
    });

    deleteModal?.addEventListener('click', async (e) => {
        const btn = e.target.closest('[data-delete]');
        if (!btn || !deleteTargetId) return;

        const action = btn.dataset.delete;
        const id = deleteTargetId;
        closeDeleteModal();

        if (action === 'cancel') return;

        try {
            if (action === 'me') {
                const json = await postAction('/VendorNetwork/HideMessage', { messageId: id });
                if (json.success) removeRow(id);
            } else if (action === 'all') {
                if (!deleteTargetIsMine) {
                    alert('You can only delete your own messages for everyone.');
                    return;
                }
                const json = await postAction('/VendorNetwork/DeleteMessageForEveryone', { messageId: id });
                if (json.success) markDeleted(id);
            }
        } catch {
            alert('Could not delete message.');
        }
    });

    document.addEventListener('click', (e) => {
        if (!e.target.closest('#chatContextMenu') && !e.target.closest('.chat-bubble-selectable')) {
            closeMenu();
        }
    });

    replyCancel?.addEventListener('click', clearReply);

    // Enter = send, Shift+Enter = new line
    input?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    form?.addEventListener('submit', (e) => {
        e.preventDefault();
        sendMessage();
    });

    scrollToBottom();

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
