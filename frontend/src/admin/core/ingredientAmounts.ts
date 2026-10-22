import type { IngredientUnit } from '../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../shared/core/deviceLanguage'
import { assertNever } from '../../shared/core/assertNever'
import { formatNumber } from '../../shared/core/numberText'

export type AmountEntryUnit = 'piece' | 'gram' | 'kilogram' | 'millilitre' | 'litre'

export interface AmountInput {
  typed: string
  entryUnit: AmountEntryUnit
}

export type ParsedAmountInput =
  | { kind: 'amount'; baseAmount: number }
  | { kind: 'empty' }
  | { kind: 'unreadable' }

export interface ShownAmount {
  count: number
  formattedCount: string
  entryUnit: AmountEntryUnit
}

const AMOUNT_INPUT = /^\d+(?:[.,]\d+)?$/

const THOUSAND = 1000

const SIGNIFICANT_DIGITS_A_NUMBER_HOLDS = 15

const SHOWN_FRACTION_DIGITS = 3

export const INGREDIENT_UNITS: readonly IngredientUnit[] = ['piece', 'gram', 'millilitre']

export function entryUnitsFor(unit: IngredientUnit): readonly AmountEntryUnit[] {
  switch (unit) {
    case 'piece':
      return ['piece']
    case 'gram':
      return ['gram', 'kilogram']
    case 'millilitre':
      return ['millilitre', 'litre']
    default:
      return assertNever(unit)
  }
}

function ingredientUnitOf(entryUnit: AmountEntryUnit): IngredientUnit {
  switch (entryUnit) {
    case 'piece':
      return 'piece'
    case 'gram':
    case 'kilogram':
      return 'gram'
    case 'millilitre':
    case 'litre':
      return 'millilitre'
    default:
      return assertNever(entryUnit)
  }
}

function isThousandfold(entryUnit: AmountEntryUnit): boolean {
  switch (entryUnit) {
    case 'piece':
    case 'gram':
    case 'millilitre':
      return false
    case 'kilogram':
    case 'litre':
      return true
    default:
      return assertNever(entryUnit)
  }
}

function largerEntryUnitOf(unit: IngredientUnit): AmountEntryUnit | null {
  switch (unit) {
    case 'piece':
      return null
    case 'gram':
      return 'kilogram'
    case 'millilitre':
      return 'litre'
    default:
      return assertNever(unit)
  }
}

function withoutBinaryNoise(amount: number): number {
  return Number(amount.toPrecision(SIGNIFICANT_DIGITS_A_NUMBER_HOLDS))
}

function thousandths(amount: number): number {
  return withoutBinaryNoise(amount / THOUSAND)
}

function thousandfold(amount: number): number {
  return withoutBinaryNoise(amount * THOUSAND)
}

export function parseAmountInput(input: AmountInput): ParsedAmountInput {
  const trimmed = input.typed.trim()
  if (trimmed.length === 0) {
    return { kind: 'empty' }
  }
  if (!AMOUNT_INPUT.test(trimmed)) {
    return { kind: 'unreadable' }
  }
  const typedAmount = Number(trimmed.replace(',', '.'))
  return {
    kind: 'amount',
    baseAmount: isThousandfold(input.entryUnit) ? thousandfold(typedAmount) : typedAmount,
  }
}

function writtenIn(amount: number, language: AppLanguage): string {
  return amount.toLocaleString(language, {
    useGrouping: false,
    maximumSignificantDigits: SIGNIFICANT_DIGITS_A_NUMBER_HOLDS,
  })
}

function inEntryUnit(baseAmount: number, entryUnit: AmountEntryUnit): number {
  return isThousandfold(entryUnit) ? thousandths(baseAmount) : baseAmount
}

export function amountInputFor(
  baseAmount: number | null,
  entryUnit: AmountEntryUnit,
  language: AppLanguage,
): AmountInput {
  if (baseAmount === null) {
    return { typed: '', entryUnit }
  }
  return { typed: writtenIn(inEntryUnit(baseAmount, entryUnit), language), entryUnit }
}

export function amountInputSwitchedTo(
  input: AmountInput,
  entryUnit: AmountEntryUnit,
  language: AppLanguage,
): AmountInput {
  if (ingredientUnitOf(input.entryUnit) !== ingredientUnitOf(entryUnit)) {
    return { typed: input.typed, entryUnit }
  }
  const parsed = parseAmountInput(input)
  switch (parsed.kind) {
    case 'amount':
      return amountInputFor(parsed.baseAmount, entryUnit, language)
    case 'empty':
    case 'unreadable':
      return { typed: input.typed, entryUnit }
    default:
      return assertNever(parsed)
  }
}

function roundedAsShown(amount: number): number {
  return Math.round(amount * 10 ** SHOWN_FRACTION_DIGITS) / 10 ** SHOWN_FRACTION_DIGITS
}

export function shownEntryUnitFor(baseAmount: number, unit: IngredientUnit): AmountEntryUnit {
  const largerEntryUnit = largerEntryUnitOf(unit)
  return largerEntryUnit !== null && roundedAsShown(baseAmount) >= THOUSAND ? largerEntryUnit : unit
}

export function displayedAmountIn(
  baseAmount: number,
  entryUnit: AmountEntryUnit,
  language: AppLanguage,
): ShownAmount {
  const count = inEntryUnit(baseAmount, entryUnit)
  return {
    count,
    formattedCount: formatNumber(count, language, SHOWN_FRACTION_DIGITS),
    entryUnit,
  }
}
