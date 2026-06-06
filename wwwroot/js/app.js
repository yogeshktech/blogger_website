document.addEventListener('DOMContentLoaded', () => {
    // Public mobile nav
    const navToggle = document.getElementById('navToggle');
    const siteMenu = document.getElementById('siteMenu');
    if (navToggle && siteMenu) {
        navToggle.addEventListener('click', () => siteMenu.classList.toggle('open'));
        document.addEventListener('click', (e) => {
            if (!navToggle.contains(e.target) && !siteMenu.contains(e.target))
                siteMenu.classList.remove('open');
        });
    }

    // Admin sidebar
    const sidebarToggle = document.getElementById('sidebarToggle');
    const adminSidebar = document.getElementById('adminSidebar');
    const sidebarOverlay = document.getElementById('sidebarOverlay');
    const closeSidebar = () => {
        adminSidebar?.classList.remove('open');
        sidebarOverlay?.classList.remove('open');
    };
    sidebarToggle?.addEventListener('click', () => {
        adminSidebar?.classList.toggle('open');
        sidebarOverlay?.classList.toggle('open');
    });
    sidebarOverlay?.addEventListener('click', closeSidebar);

    // Admin user menu dropdown
    const userMenuToggle = document.getElementById('userMenuToggle');
    const userMenuDropdown = document.getElementById('userMenuDropdown');
    const closeUserMenu = () => {
        userMenuDropdown?.classList.remove('open');
        userMenuToggle?.setAttribute('aria-expanded', 'false');
    };
    userMenuToggle?.addEventListener('click', (e) => {
        e.stopPropagation();
        const isOpen = userMenuDropdown?.classList.toggle('open');
        userMenuToggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    });
    document.addEventListener('click', (e) => {
        if (!document.getElementById('userMenu')?.contains(e.target))
            closeUserMenu();
    });

    // Auto-dismiss alerts
    document.querySelectorAll('[data-auto-dismiss]').forEach(el => {
        setTimeout(() => { el.style.opacity = '0'; setTimeout(() => el.remove(), 300); }, 4000);
    });
});
