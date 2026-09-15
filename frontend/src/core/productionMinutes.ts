import type { AppLanguage } from './apiTypes'
import { assertNever } from './assertNever'

export const LONGEST_PRODUCTION_MINUTES = 600

export function formatMinutes(minutes: number, language: AppLanguage): string {
  const rounded = Math.round(minutes * 10) / 10
  switch (language) {
    case 'de':
      return String(rounded).replace('.', ',')
    case 'en':
      return String(rounded)
    default:
      return assertNever(language)
  }
}
