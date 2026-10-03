import type { BasketLineView } from './basket'
import type { CollapsedLine } from '../../shared/core/collapse'

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
