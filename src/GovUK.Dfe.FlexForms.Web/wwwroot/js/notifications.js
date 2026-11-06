const API_BASE = document.querySelector('meta[name="api-base"]')?.content ?? "";

const container = () => document.getElementById('notification-container');
const unreadBadge = () => document.getElementById('notifications-unread-badge');
const unreadSr = () => document.getElementById('notifications-unread-sr');
const antiForgeryToken = () => {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input?.value;
};
const PAGE_LOAD_AT_MS = Date.now();
const HUB_TICKET_COOLDOWN_MS = 30_000;
// hubauth cookie lasts 10 minutes; renew with headroom.
const HUB_COOKIE_RENEW_MS = 8 * 60 * 1000;
const HUB_START_MAX_BACKOFF_MS = 10 * 60 * 1000;
let hubTicketInFlight = null;
let hubTicketCooldownUntil = 0;
let hubCookieRenewedAt = 0;
let hubConnection = null;
let hubStartRetryTimer = null;
let hubStartAttempts = 0;

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

window.renderOrUpdate = function (n, options) {
    // Handle case where notifications might be in an array (from SignalR)
    // If it's an array, render each notification
    if (Array.isArray(n)) {
        n.forEach(notification => renderSingleNotification(notification, options));
        return;
    }
    
    // Otherwise render the single notification
    renderSingleNotification(n, options);
};

function fileValidationStatusLabel(status) {
    const key = String(status ?? '').toLowerCase();
    if (key === 'pending' || key === '1') return 'Validation pending';
    if (key === 'passed' || key === '2') return 'Validated';
    if (key === 'failed' || key === '3') return 'Validation failed';
    return '';
}

// File-validation messages are Markdown from the tenant function. Web renders them with MarkdownSafe
// (Markdig without raw HTML + HtmlSanitizer allow-list); the browser only inserts that output.
async function renderValidationMessage(markdown) {
    try {
        const res = await fetch('/file-validation/message/render', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': antiForgeryToken() ?? ''
            },
            body: JSON.stringify({ message: markdown })
        });
        return res.ok ? await res.json() : null;
    } catch {
        return null;
    }
}

// Shows the message as plain text straight away, then swaps in the rendered HTML. Inline results
// replace the text in place; block results (lists, several paragraphs) are appended to blockParent.
function appendValidationMessage(inlineParent, blockParent, detail, { prefix = '', inlineClass = '', blockClass = '' } = {}) {
    const fallback = document.createElement('span');
    if (inlineClass) fallback.className = inlineClass;
    fallback.textContent = `${prefix}${detail}`;
    inlineParent.appendChild(fallback);

    void renderValidationMessage(detail).then(rendered => {
        if (!rendered?.html || !fallback.isConnected) return;

        const content = document.createElement('div');
        content.innerHTML = rendered.html;

        if (rendered.isInline) {
            const node = document.createElement('span');
            if (inlineClass) node.className = inlineClass;
            node.append(prefix, ...content.childNodes);
            fallback.replaceWith(node);
            return;
        }

        if (blockClass) content.className = blockClass;
        fallback.remove();
        blockParent.appendChild(content);
    });
}

function splitFileValidationDetail(notification) {
    if (String(notification?.category ?? '').toLowerCase() !== 'file-validation') return null;
    const meta = notification.metadata || {};
    const detail = String(meta.message || meta.Message || '').trim();
    const message = String(notification.message ?? '');
    if (!detail || !message.endsWith(detail)) return null;
    return { lead: message.slice(0, message.length - detail.length).trimEnd(), detail };
}

function isApplicationPreviewPage() {
    return Boolean(document.querySelector('[data-application-preview]'));
}

function applyFileValidationStatus(notification, options) {
    const category = notification?.category;
    if (category && String(category).toLowerCase() !== 'file-validation') return;

    const meta = notification?.metadata || {};
    const fileId = meta.fileId || meta.FileId;
    if (!fileId) return;

    const cell = document.querySelector(`[data-file-validation-status="${fileId}"]`);
    if (cell) {
        const label = fileValidationStatusLabel(meta.status || meta.Status);
        const detail = meta.message || meta.Message || '';
        if (label) {
            cell.replaceChildren();
            const labelSpan = document.createElement('span');
            labelSpan.textContent = label;
            cell.appendChild(labelSpan);
            if (detail) {
                appendValidationMessage(cell, cell, detail, {
                    prefix: ' — ',
                    inlineClass: 'govuk-hint',
                    blockClass: 'govuk-hint govuk-!-margin-top-1 govuk-!-margin-bottom-0'
                });
            }
        }
    }

    // Preview submit-gate copy is rendered from the last GET. Reload only for live
    // hub events so unread replay after a reload cannot loop.
    if (options?.live && isApplicationPreviewPage() && !window.__applicationPreviewValidationReload) {
        window.__applicationPreviewValidationReload = true;
        window.location.reload();
    }
}

function renderSingleNotification(notification, options) {
    if (!notification || !notification.id) return;
    applyFileValidationStatus(notification, options);
    
    const map = mapTypeToCss(notification.type);
    const id = `notification-${notification.id}`;
    const wrapper = ensureElement(id);
    wrapper.className = `govuk-${map.banner} ${map.css} notification-item`;
    wrapper.setAttribute('role', 'alert');
    wrapper.setAttribute('data-module', `govuk-${map.banner}`);
    wrapper.setAttribute('data-notification-id', notification.id);
    wrapper.setAttribute('data-auto-dismiss', (notification.autoDismiss ? 'true' : 'false'));
    wrapper.setAttribute('data-auto-dismiss-seconds', (notification.autoDismissSeconds ?? 0));

    const el = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };
    const message = String(notification.message ?? '');
    const validationDetail = splitFileValidationDetail(notification);
    const appendDetail = (heading, blockParent) => {
        if (!validationDetail) return;
        appendValidationMessage(heading, blockParent, validationDetail.detail, { prefix: ' ' });
    };

    if (map.banner === 'error-summary') {
        const alert = el('div');
        alert.setAttribute('role', 'alert');
        const body = el('div', 'govuk-error-summary__body');
        const heading = el('p', 'govuk-notification-banner__heading');
        heading.append(el('span', 'govuk-visually-hidden', 'Error: '), validationDetail?.lead ?? message);
        body.appendChild(heading);
        appendDetail(heading, body);
        alert.append(el('h2', 'govuk-error-summary__title', map.title), body);
        wrapper.replaceChildren(alert);
    } else {
        const header = el('div', 'govuk-notification-banner__header');
        header.appendChild(el('h2', 'govuk-notification-banner__title', map.title));
        const content = el('div', 'govuk-notification-banner__content');
        const heading = el('p', 'govuk-notification-banner__heading', validationDetail?.lead ?? message);
        content.appendChild(heading);
        appendDetail(heading, content);
        wrapper.replaceChildren(header, content);
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
    cont.replaceChildren();
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

window.refreshUnreadCount = refreshUnreadCount;

async function ensureHubCookie() {
    if (Date.now() < hubTicketCooldownUntil) {
        return false;
    }

    if (hubTicketInFlight) {
        return hubTicketInFlight;
    }

    hubTicketInFlight = (async () => {
        try {
            const res = await fetch("/internal/hub-ticket", { credentials: "include" });
            if (!res.ok) {
                hubTicketCooldownUntil = Date.now() + HUB_TICKET_COOLDOWN_MS;
                return false;
            }

            const { url } = await res.json();
            if (!url) {
                return false;
            }

            // Tickets are single use and expire after 60s, so a throttled background tab can redeem too late.
            const redeemed = await fetch(url, { credentials: "include" });
            if (!redeemed.ok) {
                hubTicketCooldownUntil = Date.now() + HUB_TICKET_COOLDOWN_MS;
                return false;
            }

            hubCookieRenewedAt = Date.now();
            return true;
        } catch {
            hubTicketCooldownUntil = Date.now() + HUB_TICKET_COOLDOWN_MS;
            return false;
        } finally {
            hubTicketInFlight = null;
        }
    })();

    return hubTicketInFlight;
}

async function startHub() {
    // Skip SignalR when running under Cypress to avoid WebSocket proxy issues
    if (window.Cypress) {
        console.log('Cypress detected - skipping SignalR notifications hub');
        return;
    }

    if (window.__flexformsHubStarted || typeof signalR === "undefined") {
        return;
    }
    window.__flexformsHubStarted = true;

    try {
        const ticketOk = await ensureHubCookie();
        if (!ticketOk) {
            scheduleHubStartRetry();
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_BASE}/hubs/notifications`, { withCredentials: true })
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .build();

        connection.on("notification.upserted", n => window.renderOrUpdate(n, { live: true }));
        connection.on("notification.dismissed", ({ id }) => window.removeFromUi(id));
        connection.on("notification.cleared", () => window.clearUi());

        // Reconnect re-negotiates with the API, which needs a live hubauth cookie.
        connection.onreconnecting(() => { void ensureHubCookie(); });
        connection.onreconnected(async () => { await ensureHubCookie(); });
        connection.onclose(() => {
            hubConnection = null;
            scheduleHubStartRetry();
        });

        await connection.start();
        hubConnection = connection;
        hubStartAttempts = 0;
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
        hubConnection = null;
        scheduleHubStartRetry();
    }
}

// Initial start can fail (expired session, ticket redeemed too late in a suspended tab). Retry with
// backoff, and only while the tab is visible; the visibility listener picks it up otherwise.
function scheduleHubStartRetry() {
    window.__flexformsHubStarted = false;
    if (hubStartRetryTimer) {
        return;
    }

    const backoffMs = Math.min(HUB_TICKET_COOLDOWN_MS * 2 ** hubStartAttempts, HUB_START_MAX_BACKOFF_MS);
    hubStartAttempts++;
    const delayMs = Math.max(backoffMs, hubTicketCooldownUntil - Date.now());
    hubStartRetryTimer = setTimeout(() => {
        hubStartRetryTimer = null;
        if (!document.hidden) {
            void startHub();
        }
    }, delayMs);
}

// Renewal of an existing connection's cookie. Hidden tabs have their timers throttled, so renew
// when the tab is visible again instead.
setInterval(() => {
    if (hubConnection && !document.hidden) {
        void ensureHubCookie();
    }
}, HUB_COOKIE_RENEW_MS);

document.addEventListener("visibilitychange", () => {
    if (document.hidden) {
        return;
    }
    if (!hubConnection) {
        if (!hubStartRetryTimer) {
            void startHub();
        }
        return;
    }
    if (Date.now() - hubCookieRenewedAt >= HUB_COOKIE_RENEW_MS) {
        void ensureHubCookie();
    }
});

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
    clearAll: async () => {
        const response = await fetch('/notifications/clear', { method: 'POST', credentials: 'include', headers: { 'RequestVerificationToken': antiForgeryToken() ?? '' } });
        if (!response.ok) {
            throw new Error(`Failed to clear notifications (${response.status})`);
        }
        await refreshUnreadCount();
    }
};

// Update badge when hub events occur
window.addEventListener('DOMContentLoaded', () => {
    // Slight delay to ensure hub handlers are set
    setTimeout(() => {
        try {
            // Hook into existing handlers by wrapping render/remove/clear
            const origRender = window.renderOrUpdate;
            window.renderOrUpdate = function(n, options) { origRender(n, options); void refreshUnreadCount(); };
            const origRemove = window.removeFromUi;
            window.removeFromUi = function(id) { origRemove(id); void refreshUnreadCount(); };
            const origClear = window.clearUi;
            window.clearUi = function() { origClear(); void refreshUnreadCount(); };
        } catch { }
    }, 0);
});
