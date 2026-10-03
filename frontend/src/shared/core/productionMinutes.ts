import type { AppLanguage } from './deviceLanguage'
import { formatNumber } from './numberText'

export const LONGEST_PRODUCTION_MINUTES = 600

export function wholeMinutes(minutes: number): number {
  return Math.ceil(minutes)
}

export function formatMinutes(minutes: number, language: AppLanguage): string {
  return formatNumber(wholeMinutes(minutes), language, 0)
}
