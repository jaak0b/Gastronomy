import type { ScreenName } from './landing'
import { assertNever } from './assertNever'

export const APP_NAME = 'GastronomyApp'

export function screenTitle(
  screen: ScreenName,
  stationName: string | null,
  t: (key: string) => string,
): string {
  switch (screen) {
    case 'station':
      return stationName === null || stationName === '' ? APP_NAME : stationName
    case 'catalog':
    case 'review':
    case 'openItems':
      return t('app.title.waiter')
    case 'admin':
      return t('app.title.admin')
    case 'enrolQr':
    case 'welcome':
    case 'startingUp':
      return APP_NAME
    default:
      return assertNever(screen)
  }
}
