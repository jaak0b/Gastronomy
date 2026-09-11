import type { EstimateRange } from './estimates'

export type EstimateWording = (key: string, values: Record<string, string | number>) => string

export function estimateText(minutes: number | null, t: EstimateWording): string | null {
  if (minutes === null) {
    return null
  }
  return t('estimates.inMinutes', { count: minutes })
}

export function estimateRangeText(range: EstimateRange | null, t: EstimateWording): string | null {
  if (range === null) {
    return null
  }
  if (range.min === range.max) {
    return t('estimates.inMinutes', { count: range.min })
  }
  return t('estimates.inMinuteRange', { min: range.min, max: range.max })
}

export function withEstimate(line: string, minutes: number | null, t: EstimateWording): string {
  const estimate = estimateText(minutes, t)
  return estimate === null ? line : t('estimates.withEstimate', { line, estimate })
}

export function withRangeEstimate(
  line: string,
  range: EstimateRange | null,
  t: EstimateWording,
): string {
  const estimate = estimateRangeText(range, t)
  return estimate === null ? line : t('estimates.withEstimate', { line, estimate })
}
