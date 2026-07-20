const API_BASE = document.querySelector('meta[name="api-base"]')?.content ?? "";

const container = () => document.getElementById('notification-container');
const unreadBadge = () => document.getElementById('notifications-unread-badge');
const unreadSr = () => document.getElementById('notifications-unread-sr');
const antiForgeryToken = () => {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input?.value;
};
const PAGE_LOAD_AT_MS = Date.now();

function mapTypeToCss(type) {
    const normalize = (val) => {
        if (typeof val === 'number') {
            const byNumber = { 0: 'success',1: 'error', 2: 'info',  3: 'warning' };
            return byNumber[val] || 'info';
        }
        if (typeof val === 'string') return val.toLowerCase();
        return 'info';
    };

    const t = normalize(type);
    if (t === 'error') return { banner: 'error-summary', css: 'govuk-error-summary', title: 'There is a problem' };
    if (t === 'warning') return { banner: 'notification-banner', css: 'govuk-notification-banner--warning', title: 'Warning' };
    if (t === 'success') return { banner: 'notification-banner', css: 'govuk-notification-banner--success', title: 'Success' };
    return { banner: 'notification-banner', css: 'govuk-notification-banner', title: 'Information' };
}

function ensureElement(id) {
    let el = document.getElementById(id);
    if (!el) {
        el = document.createElement('div');
        el.id = id;
        container()?.appendChild(el);
    }
    return el;
}

window.renderOrUpdate = function (n) {
    // Handle case where notifications might be in an array (from SignalR)
    // If it's an array, render each notification
    if (Array.isArray(n)) {
        n.forEach(notification => renderSingleNotification(notification));
        return;
    }
    
    // Otherwise render the single notification
    renderSingleNotification(n);
};

function renderSingleNotification(notification) {
    if (!notification || !notification.id) return;
    
    const map = mapTypeToCss(notification.type);
    const id = `notification-${notification.id}`;
    const wrapper = ensureElement(id);
    wrapper.className = `govuk-${map.banner} ${map.css} notification-item`;
    wrapper.setAttribute('role', 'alert');
    wrapper.setAttribute('data-module', `govuk-${map.banner}`);
    wrapper.setAttribute('data-notification-id', notification.id);
    wrapper.setAttribute('data-auto-dismiss', (notification.autoDismiss ? 'true' : 'false'));
    wrapper.setAttribute('data-auto-dismiss-seconds', (notification.autoDismissSeconds ?? 0));

    if (map.banner === 'error-summary') {
        wrapper.innerHTML = `
            <div role="alert">
                <h2 class="govuk-error-summary__title">${map.title}</h2>
                <div class="govuk-error-summary__body">
                    <p class="govuk-notification-banner__heading"><span class="govuk-visually-hidden">Error: </span>${notification.message ?? ''}</p>
                </div>
            </div>
        `;
    } else {
        wrapper.innerHTML = `
            <div class="govuk-notification-banner__header">
                <h2 class="govuk-notification-banner__title">${map.title}</h2>
            </div>
            <div class="govuk-notification-banner__content">
                <p class="govuk-notification-banner__heading">${notification.message ?? ''}</p>
            </div>
        `;
    }

    const closeBtn = wrapper.querySelector('.notification-close-btn');
    if (closeBtn) {
        closeBtn.onclick = async (e) => {
            e.preventDefault();
            await dismiss(notification.id);
        };
    }


    // Auto-dismiss if configured
    if (notification.autoDismiss) {
        const secs = Number(notification.autoDismissSeconds ?? 5);
        setTimeout(() => dismiss(notification.id), secs * 1000);
    }
}

window.removeFromUi = function (id) {
    const el = document.getElementById(`notification-${id}`);
    if (!el) return;
    el.style.transition = 'opacity 0.3s ease-out';
    el.style.opacity = '0';
    setTimeout(() => el.remove(), 300);
};

window.clearUi = function () {
    const cont = container();
    if (!cont) return;
    cont.innerHTML = '';
};

async function refreshUnreadCount() {
    try {
        const unread = await fetch(`/notifications/unread`, { credentials: 'include' }).then(r => r.ok ? r.json() : []);
        const count = Array.isArray(unread) ? unread.length : 0;
        const badge = unreadBadge();
        const notificationsUnreadSr = unreadSr(); 
        if (!badge) return;
        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : String(count);
            const accessibleCount = count > 99 ? 'over 99' : String(count);
            notificationsUnreadSr.textContent = `(You have ${accessibleCount} unread ${count === 1 ? 'notification' : 'notifications'})`;
            // Ensure visual badge even if CSS fails to load (24x24 circle)
            badge.style.display = 'inline-block';
            badge.style.position = 'absolute';
            badge.style.top = '-8px';
            badge.style.right = '-12px';
            badge.style.width = '24px';
            badge.style.height = '24px';
            badge.style.borderRadius = '12px';
            badge.style.backgroundColor = '#d4351c';
            badge.style.color = '#ffffff';
            badge.style.fontSize = '12px';
            badge.style.fontWeight = '700';
            badge.style.lineHeight = '24px';
            badge.style.textAlign = 'center';
            badge.style.zIndex = '2';
        } else {
            badge.textContent = '';
            notificationsUnreadSr.textContent = '(You have no unread notifications)';
            badge.style.display = 'none';
        }
    } catch { }
}

async function ensureHubCookie() {
    const res = await fetch("/internal/hub-ticket", { credentials: "include" });
    if (!res.ok) return;
    const { url } = await res.json();

    await fetch(url, { credentials: "include" });
}

async function startHub() {
    // Skip SignalR when running under Cypress to avoid WebSocket proxy issues
    if (window.Cypress) {
        console.log('Cypress detected - skipping SignalR notifications hub');
        return;
    }
    
    try {
        await ensureHubCookie();

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_BASE}/hubs/notifications`, { withCredentials: true })
            .withAutomaticReconnect()
            .build();

        connection.on("notification.upserted", n => window.renderOrUpdate(n));
        connection.on("notification.dismissed", ({ id }) => window.removeFromUi(id));
        connection.on("notification.cleared", () => window.clearUi());

        connection.onreconnecting(async () => { await ensureHubCookie(); });
        connection.onclose(async () => {
            try {
                await ensureHubCookie();
                await connection.start();
            } catch { /* optional backoff */ }
        });

        // Renew hub cookie proactively (10min cookie >>> renew every ~8 min)
        setInterval(ensureHubCookie, 8 * 60 * 1000);

        await connection.start();
        // Render only notifications that were created very recently (to catch events created during navigation)
        try {
            const thresholdMs = 10000; // 10s window
            const cutoff = PAGE_LOAD_AT_MS - thresholdMs;
            const unread = await fetch(`/notifications/unread`, { credentials: "include" })
                .then(r => r.ok ? r.json() : [])
                .catch(() => []);
            unread
                .filter(n => {
                    const ts = n && n.createdAt ? new Date(n.createdAt).getTime() : 0;
                    return ts >= cutoff;
                })
                .forEach(n => window.renderOrUpdate(n));
        } catch { }
        await refreshUnreadCount();
    } catch {
     
    }
}

// Start on every page load
document.addEventListener("DOMContentLoaded", startHub);

async function markAsRead(id) {
    try {
        const response = await fetch(`/notifications/read/${encodeURIComponent(id)}`, {
            method: 'POST',
            credentials: 'include',
            headers: { 'RequestVerificationToken': antiForgeryToken() ?? '' }
        });
        if (!response.ok) {
            console.error('Failed to mark notification as read:', response.status, response.statusText);
        }
    } catch (error) {
        console.error('Error marking notification as read:', error);
    }
}

async function dismiss(id) {
    try {
        const response = await fetch(`/notifications/remove/${encodeURIComponent(id)}`, {
            method: 'POST',
            credentials: 'include',
            headers: { 'RequestVerificationToken': antiForgeryToken() ?? '' }
        });
        if (!response.ok) {
            console.error('Failed to dismiss notification:', response.status, response.statusText);
        } else {
            window.removeFromUi(id);
        }
    } catch (error) {
        console.error('Error dismissing notification:', error);
    }
    await refreshUnreadCount();
}

window.NotificationsApi = {
    markAllRead: async () => { try { await fetch('/notifications/read-all', { method: 'POST', credentials: 'include', headers: { 'RequestVerificationToken': antiForgeryToken() ?? '' } }); } finally { await refreshUnreadCount(); } },
    clearAll: async () => { try { await fetch('/notifications/clear', { method: 'POST', credentials: 'include', headers: { 'RequestVerificationToken': antiForgeryToken() ?? '' } }); } finally { await refreshUnreadCount(); } }
};

// Update badge when hub events occur
window.addEventListener('DOMContentLoaded', () => {
    // Slight delay to ensure hub handlers are set
    setTimeout(() => {
        try {
            // Hook into existing handlers by wrapping render/remove/clear
            const origRender = window.renderOrUpdate;
            window.renderOrUpdate = function(n) { origRender(n); refreshUnreadCount(); };
            const origRemove = window.removeFromUi;
            window.removeFromUi = function(id) { origRemove(id); refreshUnreadCount(); };
            const origClear = window.clearUi;
            window.clearUi = function() { origClear(); refreshUnreadCount(); };
        } catch { }
    }, 0);
});
