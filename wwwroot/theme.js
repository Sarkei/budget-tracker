/* Farbschema: dunkel als Standard, Wahl in localStorage, Start ohne Aufblitzen. */
(function () {
    window.btTheme = {
        get: function () {
            try {
                /* Tims Vorgabe: dunkel ist der Standard. */
                return localStorage.getItem('bt-theme') || 'dark';
            } catch (e) {
                return 'dark';
            }
        },
        set: function (theme) {
            var value = theme === 'dark' ? 'dark' : 'light';
            document.documentElement.setAttribute('data-theme', value);
            document.documentElement.classList.toggle('dark', value === 'dark');
            try {
                localStorage.setItem('bt-theme', value);
            } catch (e) { /* privater Modus */ }
        }
    };
})();
