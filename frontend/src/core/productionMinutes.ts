export type ProductionMinutesReading =
  | { kind: 'valid'; minutes: number | null }
  | { kind: 'invalid' }

export const LONGEST_PRODUCTION_MINUTES = 600

const WHOLE_MINUTES = /^\d+$/

export function parseProductionMinutes(typed: string): ProductionMinutesReading {
  const trimmed = typed.trim()
  if (trimmed.length === 0) {
    return { kind: 'valid', minutes: null }
  }
  if (!WHOLE_MINUTES.test(trimmed)) {
    return { kind: 'invalid' }
  }
  const minutes = Number(trimmed)
  if (minutes > LONGEST_PRODUCTION_MINUTES) {
    return { kind: 'invalid' }
  }
  return { kind: 'valid', minutes }
}

export function formatProductionMinutes(minutes: number | null): string {
  return minutes === null ? '' : String(minutes)
}
