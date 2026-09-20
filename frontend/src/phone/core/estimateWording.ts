import type { AppLanguage } from '../../shared/api/apiTypes'
import type { EstimateRange } from './estimates'
import { formatMinutes, wholeMinutes } from '../../shared/core/productionMinutes'
import type { Translate } from '../../shared/core/translation'

export function estimateText(
  minutes: number | null,
  t: Translate,
  language: AppLanguage,
): string | null {
  if (minutes === null) {
    return null
  }
  return t('estimates.inMinutes', { count: formatMinutes(minutes, language) })
}

export function estimateRangeText(
  range: EstimateRange | null,
  t: Translate,
  language: AppLanguage,
): string | null {
  if (range === null) {
    return null
  }
  if (wholeMinutes(range.min) === wholeMinutes(range.max)) {
    return t('estimates.inMinutes', { count: formatMinutes(range.min, language) })
  }
  return t('estimates.inMinuteRange', {
    min: formatMinutes(range.min, language),
    max: formatMinutes(range.max, language),
  })
}

export function withEstimate(
  line: string,
  minutes: number | null,
  t: Translate,
  language: AppLanguage,
): string {
  const estimate = estimateText(minutes, t, language)
  return estimate === null ? line : t('estimates.withEstimate', { line, estimate })
}
