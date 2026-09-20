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
