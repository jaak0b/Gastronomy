import { z } from 'zod'

export const appLanguageSchema = z.enum(['de', 'en'])

export type AppLanguage = z.infer<typeof appLanguageSchema>

export const LANGUAGE_STORAGE_KEY = 'language'

export function browserLanguage(): AppLanguage {
  const preferred = navigator.language ?? ''
  return preferred.toLowerCase().startsWith('de') ? 'de' : 'en'
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

export function appLanguageOf(locale: string): AppLanguage {
  if (locale === 'en' || locale === 'de') {
    return locale
  }
  throw new Error(`The locale ${locale} is not one of the application's languages.`)
}
