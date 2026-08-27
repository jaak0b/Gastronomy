import type { AppLanguage } from './apiTypes'
import { assertNever } from './assertNever'
import type { BasketLineView } from './basket'

export function lineTotalCents(line: BasketLineView): number {
  return line.unitPriceCents * line.quantity
}

export function orderTotalCents(lines: BasketLineView[]): number {
  return lines.reduce((total, line) => total + lineTotalCents(line), 0)
}

function grouped(euros: string, separator: string): string {
  return euros.replace(/\B(?=(\d{3})+(?!\d))/g, separator)
}

export function formatPrice(cents: number, locale: AppLanguage): string {
  const euros = Math.floor(Math.abs(cents) / 100).toString()
  const remainder = (Math.abs(cents) % 100).toString().padStart(2, '0')
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
