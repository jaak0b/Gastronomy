export const PICKED_FESTIVAL_STORAGE_KEY = 'adminPickedFestival'

export function readPickedFestival(): string | null {
  try {
    return localStorage.getItem(PICKED_FESTIVAL_STORAGE_KEY)
  } catch {
    return null
  }
}

export function writePickedFestival(festivalId: string): boolean {
  try {
    localStorage.setItem(PICKED_FESTIVAL_STORAGE_KEY, festivalId)
    return true
  } catch {
    return false
  }
}

export function forgetPickedFestival(): boolean {
  try {
    localStorage.removeItem(PICKED_FESTIVAL_STORAGE_KEY)
    return true
  } catch {
    return false
  }
}
