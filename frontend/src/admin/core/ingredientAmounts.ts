import type { AppLanguage } from '../../shared/core/deviceLanguage'

export type ParsedAmountInput =
  | { kind: 'amount'; amount: number }
  | { kind: 'empty' }
  | { kind: 'unreadable' }

const AMOUNT_INPUT = /^\d+(?:[.,]\d+)?$/

const SIGNIFICANT_DIGITS_A_NUMBER_HOLDS = 15

export function parseAmountInput(typed: string): ParsedAmountInput {
  const trimmed = typed.trim()
  if (trimmed.length === 0) {
    return { kind: 'empty' }
  }
  if (!AMOUNT_INPUT.test(trimmed)) {
    return { kind: 'unreadable' }
  }
  return { kind: 'amount', amount: Number(trimmed.replace(',', '.')) }
}

function writtenIn(amount: number, language: AppLanguage): string {
  return amount.toLocaleString(language, {
    useGrouping: false,
    maximumSignificantDigits: SIGNIFICANT_DIGITS_A_NUMBER_HOLDS,
  })
}

export function amountInputFor(amount: number | null, language: AppLanguage): string {
  return amount === null ? '' : writtenIn(amount, language)
}
