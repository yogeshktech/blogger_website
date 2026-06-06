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

    document.querySelectorAll('.blog-share-btn[data-share="copy"]').forEach(btn => {
        btn.addEventListener('click', async () => {
            const wrap = btn.closest('.blog-engage');
            const url = wrap?.dataset.shareUrl;
            if (!url) return;

            try {
                await navigator.clipboard.writeText(url);
                const prev = btn.title;
                btn.title = 'Link copied!';
                setTimeout(() => { btn.title = prev || 'Copy link'; }, 2000);
            } catch {
                window.prompt('Copy this link:', url);
            }
        });
    });
})();
