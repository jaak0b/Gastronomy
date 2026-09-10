export type EstimateWording = (key: string, values: Record<string, string | number>) => string

export function estimateText(minutes: number | null, t: EstimateWording): string | null {
  if (minutes === null) {
    return null
  }
  return t('estimates.inMinutes', { count: minutes })
}

export function withEstimate(line: string, minutes: number | null, t: EstimateWording): string {
  const estimate = estimateText(minutes, t)
  return estimate === null ? line : t('estimates.withEstimate', { line, estimate })
}
