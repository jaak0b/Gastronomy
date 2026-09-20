import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import 'vuetify/styles'
import './shared/styles/controls.css'
import '@mdi/font/css/materialdesignicons.css'
import { createVuetify } from 'vuetify'
import App from './App.vue'
import de from './shared/i18n/de.json'
import en from './shared/i18n/en.json'
import { startRouter } from './shared/router/router'
import { appTheme } from './shared/theme'
import { initialLanguage } from './shared/core/deviceLanguage'

export const i18n = createI18n({
  legacy: false,
  locale: initialLanguage(),
  fallbackLocale: 'de',
  messages: { de, en },
})

export const vuetify = createVuetify({
  theme: appTheme,
  defaults: {
    VBtn: { size: 'large', variant: 'flat' },
    VTextField: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VNumberInput: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VSelect: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VCheckbox: { density: 'comfortable', hideDetails: 'auto' },
  },
})

startRouter()

createApp(App).use(createPinia()).use(i18n).use(vuetify).mount('#app')
