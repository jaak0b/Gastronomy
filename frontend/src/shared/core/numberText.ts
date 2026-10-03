import type { AppLanguage } from './deviceLanguage'

export function formatNumber(
  value: number,
  language: AppLanguage,
  maximumFractionDigits: number,
): string {
  return new Intl.NumberFormat(language, { maximumFractionDigits }).format(value)
}
