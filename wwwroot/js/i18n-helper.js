// Minimal i18n stub with English + Polish placeholders
const i18n = {
    data: { 
        en: {},
        pl: {
            creatingYourPlan: 'Tworzenie planu...',
            aiCreatesYourPlan: 'AI tworzy Twój plan',
            prepare: 'Przygotowywanie',
            creating: 'Tworzenie',
            downloadYourPlan: 'Pobierz swój plan i zacznij odkrywać z pewnością siebie',
            createTravelPlan: 'Utwórz plan podróży',
            fillInDetails: 'Wypełnij poniższe informacje, aby wygenerować spersonalizowany plan podróży AI.',
            tripDetails: 'Szczegóły podróży',
            optionalAiSuggest: '(opcjonalne - AI zasugeruje)',
            flight: 'Samolot',
            car: 'Samochód',
            train: 'Pociąg',
            bus: 'Autobus',
            helpsAiSuggest: 'Pomaga AI zasugerować najlepsze opcje transportu dla Ciebie',
            additionalPreferences: 'Dodatkowe preferencje',
            examplesPreferences: "Przykłady: Uwzględnij życie nocne i kluby | Tylko restauracje wegetariańskie | Skup się na muzeach sztuki i galeriach | Miejsca dostępne dla osób na wózkach",
            beSpecific: "Bądź konkretny! Zamiast 'imprezowanie w nocy', napisz 'Uwzględnij życie nocne i późne bary' dla lepszych wyników.",
            generatePlan: 'Generuj plan',
            preparing: 'Przygotowywanie',
            requiredFields: 'Pola obowiązkowe',
            processingTime: 'Czas przetwarzania: kilka sekund',
            noPlanFound: 'Nie znaleziono planu podróży. Utwórz nowy plan.',
            backToPlanner: 'Powrót do planera',
            plan: 'Plan',
            expandAll: 'Rozwiń wszystko',
            collapseAll: 'Zwiń wszystko',
            saveChanges: 'Zapisz zmiany',
            saveDetails: 'Zapisz szczegóły'
        }
    },
    currentLanguage: 'en',

    async init() {
        // Read document lang or cookie if present
        try {
            const docLang = document.documentElement.lang || 'en';
            // try parse cookie 'c' for culture from CookieRequestCultureProvider default name
            const cookieName = '.AspNetCore.Culture';
            const cookieValue = (document.cookie || '').split('; ').find(c => c.startsWith(cookieName + '='))?.split('=')[1];
            if (cookieValue) {
                // cookieValue format: c=en|uic=en
                const parts = decodeURIComponent(cookieValue).split('|');
                const cPart = parts.find(p => p.startsWith('c='));
                if (cPart) {
                    const culture = cPart.split('=')[1];
                    if (culture && culture.startsWith('pl')) this.currentLanguage = 'pl';
                    else this.currentLanguage = 'en';
                } else {
                    this.currentLanguage = docLang.startsWith('pl') ? 'pl' : 'en';
                }
            } else {
                this.currentLanguage = docLang.startsWith('pl') ? 'pl' : 'en';
            }
        } catch (err) {
            this.currentLanguage = 'en';
        }
        return Promise.resolve();
    },

    t(key) {
        const lang = this.currentLanguage || 'en';
        if (this.data[lang] && this.data[lang][key]) return this.data[lang][key];
        if (this.data['en'] && this.data['en'][key]) return this.data['en'][key];
        // fallback map
        const fallbacks = {
            creatingYourPlan: 'Creating your plan...',
            aiCreatesYourPlan: 'AI Creates Your Plan',
            prepare: 'Preparing',
            creating: 'Creating',
            downloadYourPlan: 'Download your plan and start exploring with confidence'
        };
        return fallbacks[key] || key;
    },

    setLanguage(language) {
        if (language === 'pl' || language === 'en') this.currentLanguage = language; else console.warn(`[i18n] Language not supported`);
    },

    getSupportedLanguages() { return Object.keys(this.data); }
};

const Intl18n = {
    formatNumber(num, locale = 'en') { try { return new Intl.NumberFormat(locale).format(num); } catch { return num.toString(); } },
    formatCurrency(num, locale = 'en', currency = 'USD') { try { return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(num); } catch { return `${currency} ${num}`; } },
    formatDate(date, locale = 'en') { try { const d = typeof date === 'string' ? new Date(date) : date; return new Intl.DateTimeFormat(locale).format(d); } catch { return String(date); } },
    formatDateTime(date, locale = 'en') { try { const d = typeof date === 'string' ? new Date(date) : date; return new Intl.DateTimeFormat(locale, { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(d); } catch { return String(date); } }
};

// Auto-initialize
if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => i18n.init()); else i18n.init();
