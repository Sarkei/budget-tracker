/* Farbschema: hell als Standard, Wahl in localStorage, Start ohne Aufblitzen. */
(function () {
    window.btTheme = {
        get: function () {
            try {
                return localStorage.getItem('bt-theme') || 'light';
            } catch (e) {
                return 'light';
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
