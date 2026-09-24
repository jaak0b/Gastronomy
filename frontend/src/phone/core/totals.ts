import type { AppLanguage } from '../../shared/core/deviceLanguage'
import { assertNever } from '../../shared/core/assertNever'
import type { BasketLineView } from './basket'
import type { CollapsedLine } from '../../shared/core/collapse'
import { euroAndCentDigits } from '../../shared/core/money'

export function quantityTimesUnitPriceCents(quantity: number, unitPriceCents: number): number {
  return quantity * unitPriceCents
}

export function collapsedTotalCents(entry: CollapsedLine<BasketLineView>): number | null {
  const unitPriceCents = entry.line.unitPriceCents
  return unitPriceCents === null
    ? null
    : quantityTimesUnitPriceCents(entry.quantity, unitPriceCents)
}

export function orderTotalCents(lines: BasketLineView[]): number {
  return lines.reduce((total, line) => total + (line.unitPriceCents ?? 0), 0)
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
