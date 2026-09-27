import { assertNever } from './assertNever'

export type StartingUpFailure = 'theLaptopWasNotReached' | 'theLaptopCouldNotAnswer'

export function startingUpMessageKey(failure: StartingUpFailure | null): string | null {
  if (failure === null) {
    return null
  }
  switch (failure) {
    case 'theLaptopWasNotReached':
      return 'shared.startup.errors.laptopNotReached'
    case 'theLaptopCouldNotAnswer':
      return 'shared.startup.errors.couldNotStart'
    default:
      return assertNever(failure)
  }
}
