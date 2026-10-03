import { createI18n } from 'vue-i18n'
import de from '../../src/shared/i18n/de.json'
import en from '../../src/shared/i18n/en.json'

export function testPlugins(locale: 'de' | 'en' = 'de') {
  return [createI18n({ legacy: false, locale, messages: { de, en } })]
}
