import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import { createVuetify } from 'vuetify'
import App from './App.vue'
import de from './locales/de.json'
import en from './locales/en.json'
import { startRouter } from './router'
import { initialLanguage } from './appLanguage'

export const i18n = createI18n({
  legacy: false,
  locale: initialLanguage(),
  fallbackLocale: 'de',
  messages: { de, en },
})

export const vuetify = createVuetify({
  defaults: {
    VBtn: { size: 'large', variant: 'flat' },
    VTextField: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VSelect: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VCheckbox: { density: 'comfortable', hideDetails: 'auto' },
  },
})

startRouter()

createApp(App).use(createPinia()).use(i18n).use(vuetify).mount('#app')
