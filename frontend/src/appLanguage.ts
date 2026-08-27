import type { AppLanguage } from './core/apiTypes'

export const LANGUAGE_STORAGE_KEY = 'language'

export function browserLanguage(): AppLanguage {
  const preferred = navigator.language ?? ''
  return preferred.toLowerCase().startsWith('en') ? 'en' : 'de'
}

export function initialLanguage(): AppLanguage {
  const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY)
  if (stored === 'en' || stored === 'de') {
    return stored
  }
  return browserLanguage()
}

export function storeLanguage(language: AppLanguage): void {
  localStorage.setItem(LANGUAGE_STORAGE_KEY, language)
}
