// Declarative replacements for inline event handlers, which the Content-Security-Policy blocks.
//   data-confirm="message"        on a button/link (click) or form (submit): cancel unless confirmed
//   data-navigate-param="name"    on a select: navigate to ?name=<value> on change ("?" when empty)
//   data-submit-on-change         on a form control: submit its form on change
//   data-history-back             on a link: go back in history instead of following href
(function () {
    document.addEventListener('click', function (e) {
        var target = e.target instanceof Element ? e.target : null;
        if (!target) return;

        var confirmEl = target.closest('[data-confirm]');
        if (confirmEl && confirmEl.tagName !== 'FORM' && !window.confirm(confirmEl.getAttribute('data-confirm'))) {
            e.preventDefault();
            e.stopImmediatePropagation();
            return;
        }

        var backLink = target.closest('[data-history-back]');
        if (backLink && window.history.length > 1) {
            e.preventDefault();
            window.history.back();
        }
    }, true);

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (form instanceof HTMLFormElement && form.hasAttribute('data-confirm')
            && !window.confirm(form.getAttribute('data-confirm'))) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);

    document.addEventListener('change', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLElement)) return;

        var param = el.getAttribute('data-navigate-param');
        if (param) {
            var value = el.value;
            window.location.href = value
                ? '?' + encodeURIComponent(param) + '=' + encodeURIComponent(value)
                : '?';
            return;
        }

        if (el.hasAttribute('data-submit-on-change') && el.form) {
            el.form.submit();
        }
    });
})();
