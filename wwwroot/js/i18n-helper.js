// Global i18n object
const i18n = {
    data: {},
    currentLanguage: 'en',

    // Initialize translations from JSON file
    async init() {
        try {
            const response = await fetch('/js/i18n.json');
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            this.data = await response.json();
            this.currentLanguage = document.documentElement.lang || 'en';
            console.log(`[i18n] Loaded translations for languages: ${Object.keys(this.data).join(', ')}`);
        } catch (error) {
            console.error('[i18n] Failed to load translations:', error);
            // Fallback: create minimal English object
            this.data = { en: {}, pl: {} };
        }
    },

    // Translate key to current language
    t(key) {
        const lang = this.currentLanguage || 'en';
        
        if (this.data[lang] && this.data[lang][key]) {
            return this.data[lang][key];
        }
        
        if (this.data['en'] && this.data['en'][key]) {
            console.warn(`[i18n] Missing translation for "${key}" in language "${lang}", using English fallback`);
            return this.data['en'][key];
        }
        
        console.warn(`[i18n] Missing key "${key}" in both "${lang}" and English`);
        return key;
    },

    // Change language at runtime
    setLanguage(language) {
        if (Object.keys(this.data).includes(language)) {
            this.currentLanguage = language;
            console.log(`[i18n] Language changed to: ${language}`);
        } else {
            console.warn(`[i18n] Language "${language}" not supported`);
        }
    },

    // Get supported languages
    getSupportedLanguages() {
        return Object.keys(this.data);
    }
};

// Formatting helpers
const Intl18n = {
    // Format number according to locale
    formatNumber(num, locale = 'en') {
        try {
            return new Intl.NumberFormat(locale).format(num);
        } catch (error) {
            console.error(`[Intl18n] Number format error: ${error}`);
            return num.toString();
        }
    },

    // Format currency
    formatCurrency(num, locale = 'en', currency = 'USD') {
        try {
            return new Intl.NumberFormat(locale, { 
                style: 'currency', 
                currency: currency 
            }).format(num);
        } catch (error) {
            console.error(`[Intl18n] Currency format error: ${error}`);
            return `${currency} ${num}`;
        }
    },

    // Format date
    formatDate(date, locale = 'en') {
        try {
            const dateObj = typeof date === 'string' ? new Date(date) : date;
            return new Intl.DateTimeFormat(locale).format(dateObj);
        } catch (error) {
            console.error(`[Intl18n] Date format error: ${error}`);
            return date.toString();
        }
    },

    // Format date and time
    formatDateTime(date, locale = 'en') {
        try {
            const dateObj = typeof date === 'string' ? new Date(date) : date;
            return new Intl.DateTimeFormat(locale, {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit'
            }).format(dateObj);
        } catch (error) {
            console.error(`[Intl18n] DateTime format error: ${error}`);
            return date.toString();
        }
    },

    // Format percentage
    formatPercentage(num, locale = 'en', decimals = 2) {
        try {
            return new Intl.NumberFormat(locale, {
                style: 'percent',
                minimumFractionDigits: decimals,
                maximumFractionDigits: decimals
            }).format(num / 100);
        } catch (error) {
            console.error(`[Intl18n] Percentage format error: ${error}`);
            return `${num}%`;
        }
    }
};

// Auto-initialize on DOM ready
document.addEventListener('DOMContentLoaded', () => {
    i18n.init().then(() => {
        console.log('[i18n] Initialization complete');
    });
});
