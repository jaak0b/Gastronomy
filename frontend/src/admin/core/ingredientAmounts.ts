import type { IngredientUnit } from '../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../shared/core/deviceLanguage'
import { assertNever } from '../../shared/core/assertNever'

export type AmountEntryUnit = 'piece' | 'gram' | 'kilogram' | 'millilitre' | 'litre'

export interface AmountInput {
  typed: string
  entryUnit: AmountEntryUnit
}

export type ParsedAmountInput =
  | { kind: 'amount'; baseAmount: number }
  | { kind: 'empty' }
  | { kind: 'unreadable' }

const AMOUNT_INPUT = /^\d+(?:[.,]\d+)?$/

const THOUSAND = 1000

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

export type MeasureUnit = Exclude<AmountEntryUnit, 'piece'>

interface MeasureScale {
  smaller: MeasureUnit
  larger: MeasureUnit
}

function measureScaleOf(unit: IngredientUnit): MeasureScale | null {
  switch (unit) {
    case 'piece':
      return null
    case 'gram':
      return { smaller: 'gram', larger: 'kilogram' }
    case 'millilitre':
      return { smaller: 'millilitre', larger: 'litre' }
    default:
      return assertNever(unit)
  }
}

function thousandths(amount: number): number {
  return Number(`${amount}e-3`)
}

function thousandfold(amount: number): number {
  return Number(`${amount}e3`)
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
  switch (language) {
    case 'de':
      return String(amount).replace('.', ',')
    case 'en':
      return String(amount)
    default:
      return assertNever(language)
  }
}

export function amountInputFor(
  baseAmount: number | null,
  unit: IngredientUnit,
  language: AppLanguage,
): AmountInput {
  const baseEntryUnit = entryUnitsFor(unit)[0]
  if (baseAmount === null) {
    return { typed: '', entryUnit: baseEntryUnit }
  }
  const scale = measureScaleOf(unit)
  if (scale !== null && baseAmount >= THOUSAND) {
    return { typed: writtenIn(thousandths(baseAmount), language), entryUnit: scale.larger }
  }
  return { typed: writtenIn(baseAmount, language), entryUnit: baseEntryUnit }
}

export function amountInputSwitchedTo(
  input: AmountInput,
  entryUnit: AmountEntryUnit,
  language: AppLanguage,
): AmountInput {
  const parsed = parseAmountInput(input)
  switch (parsed.kind) {
    case 'amount':
      return {
        typed: writtenIn(
          isThousandfold(entryUnit) ? thousandths(parsed.baseAmount) : parsed.baseAmount,
          language,
        ),
        entryUnit,
      }
    case 'empty':
    case 'unreadable':
      return { typed: input.typed, entryUnit }
    default:
      return assertNever(parsed)
  }
}

function intlUnitOf(entryUnit: MeasureUnit): string {
  switch (entryUnit) {
    case 'gram':
      return 'gram'
    case 'kilogram':
      return 'kilogram'
    case 'millilitre':
      return 'milliliter'
    case 'litre':
      return 'liter'
    default:
      return assertNever(entryUnit)
  }
}

export function unitSymbolOf(
  entryUnit: MeasureUnit,
  language: AppLanguage,
): string {
  const parts = new Intl.NumberFormat(language, {
    style: 'unit',
    unit: intlUnitOf(entryUnit),
    unitDisplay: 'short',
  }).formatToParts(1)
  return parts.find((part) => part.type === 'unit')?.value ?? ''
}

export function formatAmount(
  baseAmount: number,
  unit: IngredientUnit,
  language: AppLanguage,
  describePieces: (formattedCount: string, count: number) => string,
): string {
  const scale = measureScaleOf(unit)
  if (scale === null) {
    return describePieces(
      new Intl.NumberFormat(language, { maximumFractionDigits: 3 }).format(baseAmount),
      baseAmount,
    )
  }
  const showsLargerUnit = baseAmount >= THOUSAND
  return new Intl.NumberFormat(language, {
    style: 'unit',
    unit: intlUnitOf(showsLargerUnit ? scale.larger : scale.smaller),
    maximumFractionDigits: 3,
  }).format(showsLargerUnit ? thousandths(baseAmount) : baseAmount)
}
