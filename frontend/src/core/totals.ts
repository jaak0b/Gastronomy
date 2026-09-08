import type { AppLanguage } from './apiTypes'
import { assertNever } from './assertNever'
import type { BasketLineView } from './basket'
import type { CollapsedLine } from './collapse'

export function collapsedTotalCents(entry: CollapsedLine<BasketLineView>): number | null {
  const unitPriceCents = entry.line.unitPriceCents
  return unitPriceCents === null ? null : unitPriceCents * entry.quantity
}

export function orderTotalCents(lines: BasketLineView[]): number {
  return lines.reduce((total, line) => total + (line.unitPriceCents ?? 0), 0)
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
