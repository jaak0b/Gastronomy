import type { AppLanguage } from './apiTypes'
import { assertNever } from './assertNever'

const EURO_INPUT = /^\d+(?:[.,]\d{1,2})?$/

export function parseEuroInput(typed: string): number | null {
  const trimmed = typed.trim()
  if (!EURO_INPUT.test(trimmed)) {
    return null
  }
  const [euros, decimals = ''] = trimmed.replace(',', '.').split('.')
  const cents = decimals.padEnd(2, '0')
  return Number(euros) * 100 + Number(cents)
}

export function formatEuroInput(cents: number | null, locale: AppLanguage): string {
  if (cents === null) {
    return ''
  }
  const euros = Math.floor(cents / 100).toString()
  const remainder = (cents % 100).toString().padStart(2, '0')
  switch (locale) {
    case 'de':
      return `${euros},${remainder}`
    case 'en':
      return `${euros}.${remainder}`
    default:
      return assertNever(locale)
  }
}
