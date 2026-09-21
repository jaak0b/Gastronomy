import type { AppLanguage } from './deviceLanguage'
import { assertNever } from './assertNever'

export const LONGEST_PRODUCTION_MINUTES = 600

export function wholeMinutes(minutes: number): number {
  return Math.ceil(minutes)
}

export function formatMinutes(minutes: number, language: AppLanguage): string {
  const whole = wholeMinutes(minutes)
  switch (language) {
    case 'de':
      return new Intl.NumberFormat('de', { maximumFractionDigits: 0 }).format(whole)
    case 'en':
      return new Intl.NumberFormat('en', { maximumFractionDigits: 0 }).format(whole)
    default:
      return assertNever(language)
  }
}
