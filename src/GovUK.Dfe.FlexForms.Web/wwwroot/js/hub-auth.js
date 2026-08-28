(function () {
    function getApiBaseUrl() {
        return document.querySelector('meta[name="api-base"]')?.content?.trim() ?? "";
    }

    async function signOutHubCookie() {
        const apiBase = getApiBaseUrl();
        if (!apiBase) {
            return;
        }

        try {
            await fetch(`${apiBase}/auth/hub-signout`, {
                method: "POST",
                credentials: "include"
            });
        } catch {
            // Best effort — local session sign-out still proceeds.
        }
    }

    window.signOutHubCookie = signOutHubCookie;
})();
