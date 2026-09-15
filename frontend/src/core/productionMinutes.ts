import type { AppLanguage } from './apiTypes'
import { assertNever } from './assertNever'

export type ProductionMinutesReading =
  | { kind: 'valid'; minutes: number | null }
  | { kind: 'invalid' }

export const LONGEST_PRODUCTION_MINUTES = 600

const MINUTES = /^\d+([.,]\d)?$/

export function parseProductionMinutes(typed: string): ProductionMinutesReading {
  const trimmed = typed.trim()
  if (trimmed.length === 0) {
    return { kind: 'valid', minutes: null }
  }
  if (!MINUTES.test(trimmed)) {
    return { kind: 'invalid' }
  }
  const minutes = Number(trimmed.replace(',', '.'))
  if (minutes > LONGEST_PRODUCTION_MINUTES) {
    return { kind: 'invalid' }
  }
  return { kind: 'valid', minutes }
}

export function formatMinutes(minutes: number, language: AppLanguage): string {
  const rounded = Math.round(minutes * 10) / 10
  switch (language) {
    case 'de':
      return String(rounded).replace('.', ',')
    case 'en':
      return String(rounded)
    default:
      return assertNever(language)
  }
}

export function formatProductionMinutes(
  minutes: number | null,
  language: AppLanguage,
): string {
  return minutes === null ? '' : formatMinutes(minutes, language)
}
