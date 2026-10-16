import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

void i18n.use(initReactI18next).init({
  resources: {
    'en-US': { translation: {} },
    es: { translation: {} },
  },
  lng: 'en-US',
  fallbackLng: 'en-US',
  supportedLngs: ['en-US', 'es'],
  interpolation: { escapeValue: false },
  react: { useSuspense: false },
})

export default i18n
