import { assertNever } from '../../shared/core/assertNever'
import type { TimeTickForm } from '../../shared/core/moments'

const MILLISECONDS_PER_MINUTE = 60_000

const MINUTES_PER_DAY = 1440

const DAYS_SHOWN_WITH_WEEKDAY_AND_TIME = 3

const MOST_TIME_TICKS = 8

function tickStepsInMinutesOf(form: TimeTickForm): readonly number[] {
  switch (form) {
    case 'timeOfDay':
      return [1, 2, 5, 10, 15, 30, 60, 120, 180]
    case 'weekdayAndTime':
      return [180, 360, 720]
    case 'date':
      return [1440, 2880, 10080, 20160, 40320]
    default:
      return assertNever(form)
  }
}

function minutesBetween(startsAtMs: number, endsAtMs: number): number {
  return (endsAtMs - startsAtMs) / MILLISECONDS_PER_MINUTE
}

export function timeTickFormOf(startsAtMs: number, endsAtMs: number): TimeTickForm {
  const lengthInMinutes = minutesBetween(startsAtMs, endsAtMs)
  if (lengthInMinutes <= MINUTES_PER_DAY) {
    return 'timeOfDay'
  }
  if (lengthInMinutes <= DAYS_SHOWN_WITH_WEEKDAY_AND_TIME * MINUTES_PER_DAY) {
    return 'weekdayAndTime'
  }
  return 'date'
}

export function timeAxisTicksOf(startsAtMs: number, endsAtMs: number, form: TimeTickForm): number[] {
  const lengthInMinutes = minutesBetween(startsAtMs, endsAtMs)
  const steps = tickStepsInMinutesOf(form)
  const stepInMinutes =
    steps.find((step) => lengthInMinutes / step <= MOST_TIME_TICKS) ?? steps[steps.length - 1]
  const alignmentInMinutes = Math.min(stepInMinutes, MINUTES_PER_DAY)
  const tick = new Date(startsAtMs)
  tick.setHours(0, 0, 0, 0)
  while (tick.getTime() < startsAtMs) {
    tick.setMinutes(tick.getMinutes() + alignmentInMinutes)
  }
  const ticks: number[] = []
  while (tick.getTime() <= endsAtMs) {
    ticks.push(tick.getTime())
    tick.setMinutes(tick.getMinutes() + stepInMinutes)
  }
  return ticks
}
