(function () {
    document.querySelectorAll('.blog-like-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const wrap = btn.closest('.blog-engage');
            const blogId = wrap?.dataset.blogId;
            if (!blogId) return;

            btn.disabled = true;
            try {
                const res = await fetch('/Blog/ToggleLike', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: `blogId=${encodeURIComponent(blogId)}`
                });
                if (!res.ok) return;
                const json = await res.json();
                if (!json.success) return;

                btn.dataset.liked = json.liked ? 'true' : 'false';
                btn.setAttribute('aria-pressed', json.liked ? 'true' : 'false');
                btn.classList.toggle('liked', json.liked);

                const countEl = btn.querySelector('.blog-like-count');
                if (countEl) countEl.textContent = json.count;

                const label = btn.querySelector('.blog-like-label');
                if (label) label.textContent = json.liked ? 'Liked' : 'Like';

                const heart = btn.querySelector('svg');
                if (heart) heart.setAttribute('fill', json.liked ? 'currentColor' : 'none');
            } finally {
                btn.disabled = false;
            }
        });
    });

    const getSharePayload = (wrap) => {
        const url = wrap?.dataset.shareUrl || '';
        const title = wrap?.dataset.shareTitle || 'Blog';
        const text = wrap?.dataset.shareText || `${title}\n\nRead here: ${url}`;
        return { url, title, text };
    };

    const flashTitle = (btn, message) => {
        const prev = btn.title;
        btn.title = message;
        setTimeout(() => { btn.title = prev; }, 2000);
    };

    document.querySelectorAll('.blog-share-btn[data-share="copy"]').forEach(btn => {
        btn.addEventListener('click', async () => {
            const wrap = btn.closest('.blog-engage');
            const { text } = getSharePayload(wrap);
            if (!text) return;

            try {
                await navigator.clipboard.writeText(text);
                flashTitle(btn, 'Link copied!');
            } catch {
                window.prompt('Copy this link:', text);
            }
        });
    });

    document.querySelectorAll('.blog-share-btn[data-share="native"]').forEach(btn => {
        btn.addEventListener('click', async () => {
            const wrap = btn.closest('.blog-engage');
            const { url, title, text } = getSharePayload(wrap);
            if (!url) return;

            if (navigator.share) {
                try {
                    await navigator.share({ title, text, url });
                    return;
                } catch { /* user cancelled or unsupported */ }
            }

            try {
                await navigator.clipboard.writeText(text);
                flashTitle(btn, 'Copied!');
            } catch {
                window.prompt('Copy and share:', text);
            }
        });
    });
})();
