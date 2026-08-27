import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import './style.css'
import App from './App.vue'
import de from './locales/de.json'
import en from './locales/en.json'
import { startRouter } from './router'

const storedLanguage = localStorage.getItem('language')

export const i18n = createI18n({
  legacy: false,
  locale: storedLanguage === 'en' ? 'en' : 'de',
  fallbackLocale: 'de',
  messages: { de, en },
})

startRouter()

createApp(App).use(createPinia()).use(i18n).mount('#app')
