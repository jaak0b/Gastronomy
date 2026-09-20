import { isPhoneScreen, type ScreenName } from './landing'
import { assertNever } from './assertNever'

export const APP_NAME = 'GastronomyApp'

export function screenTitle(
  screen: ScreenName,
  stationName: string | null,
  t: (key: string) => string,
): string {
  if (isPhoneScreen(screen)) {
    return t('app.title.waiter')
  }
  switch (screen) {
    case 'station':
      return stationName === null || stationName === '' ? APP_NAME : stationName
    case 'admin':
      return t('app.title.admin')
    case 'doorGate':
    case 'enrolQr':
    case 'welcome':
    case 'startingUp':
      return APP_NAME
    default:
      return assertNever(screen)
  }
}
