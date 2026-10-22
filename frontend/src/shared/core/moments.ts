import { assertNever } from './assertNever'

export function formatFestivalMoment(value: string, language: string): string {
  const moment = new Date(value)
  if (Number.isNaN(moment.getTime())) {
    return ''
  }
  return moment.toLocaleString(language, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function localInputToUtcIso(localInput: string): string | null {
  if (localInput.trim().length === 0) {
    return null
  }
  const moment = new Date(localInput)
  if (Number.isNaN(moment.getTime())) {
    return null
  }
  return moment.toISOString()
}

function twoDigits(value: number): string {
  return value.toString().padStart(2, '0')
}

export function utcIsoToLocalInput(value: string): string {
  const moment = new Date(value)
  if (Number.isNaN(moment.getTime())) {
    return ''
  }
  return (
    `${moment.getFullYear()}-${twoDigits(moment.getMonth() + 1)}-${twoDigits(moment.getDate())}`
    + `T${twoDigits(moment.getHours())}:${twoDigits(moment.getMinutes())}`
  )
}

function isSameLocalDay(first: Date, second: Date): boolean {
  return (
    first.getFullYear() === second.getFullYear()
    && first.getMonth() === second.getMonth()
    && first.getDate() === second.getDate()
  )
}

export function spansMoreThanOneDay(startsAtUtc: string, endsAtUtc: string): boolean {
  return !isSameLocalDay(new Date(startsAtUtc), new Date(endsAtUtc))
}

export function formatTimeOfDay(moment: Date, language: string, withWeekday: boolean): string {
  if (Number.isNaN(moment.getTime())) {
    return ''
  }
  const timeOfDay: Intl.DateTimeFormatOptions = { hour: '2-digit', minute: '2-digit' }
  if (withWeekday) {
    return moment.toLocaleString(language, { weekday: 'short', ...timeOfDay })
  }
  return moment.toLocaleTimeString(language, timeOfDay)
}

export type TimeTickForm = 'timeOfDay' | 'weekdayAndTime' | 'date'

export function formatTimeTick(moment: Date, language: string, form: TimeTickForm): string {
  switch (form) {
    case 'timeOfDay':
      return formatTimeOfDay(moment, language, false)
    case 'weekdayAndTime':
      return formatTimeOfDay(moment, language, true)
    case 'date':
      if (Number.isNaN(moment.getTime())) {
        return ''
      }
      return moment.toLocaleDateString(language, { day: '2-digit', month: '2-digit' })
    default:
      return assertNever(form)
  }
}
