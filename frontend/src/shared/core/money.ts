import type { AppLanguage } from './deviceLanguage'
import { assertNever } from './assertNever'

const EURO_INPUT = /^\d+(?:[.,]\d{1,2})?$/

const EURO_INPUT_BEING_TYPED = /^(?:\d+(?:[.,]\d{0,2})?)?$/

export function canBeTypedIntoAEuroField(typed: string): boolean {
  return EURO_INPUT_BEING_TYPED.test(typed)
}

export function parseEuroInput(typed: string): number | null {
  const trimmed = typed.trim()
  if (!EURO_INPUT.test(trimmed)) {
    return null
  }
  const [euros, decimals = ''] = trimmed.replace(',', '.').split('.')
  const cents = decimals.padEnd(2, '0')
  return Number(euros) * 100 + Number(cents)
}

export interface EuroAndCentDigits {
  euros: string
  remainder: string
}

export function euroAndCentDigits(cents: number): EuroAndCentDigits {
  return {
    euros: Math.floor(cents / 100).toString(),
    remainder: (cents % 100).toString().padStart(2, '0'),
  }
}

export function formatEuroInput(cents: number | null, locale: AppLanguage): string {
  if (cents === null) {
    return ''
  }
  const { euros, remainder } = euroAndCentDigits(cents)
  switch (locale) {
    case 'de':
      return `${euros},${remainder}`
    case 'en':
      return `${euros}.${remainder}`
    default:
      return assertNever(locale)
  }
}

function grouped(euros: string, separator: string): string {
  return euros.replace(/\B(?=(\d{3})+(?!\d))/g, separator)
}

export function formatPrice(cents: number, locale: AppLanguage): string {
  const { euros, remainder } = euroAndCentDigits(Math.abs(cents))
  const sign = cents < 0 ? '-' : ''
  switch (locale) {
    case 'de':
      return `${sign}${grouped(euros, '.')},${remainder} €`
    case 'en':
      return `${sign}€${grouped(euros, ',')}.${remainder}`
    default:
      return assertNever(locale)
  }
}
