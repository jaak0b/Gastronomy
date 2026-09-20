import { assertNever } from './assertNever'

export type StartingUpFailure = 'theLaptopWasNotReached' | 'theLaptopCouldNotAnswer'

export function startingUpMessageKey(failure: StartingUpFailure | null): string | null {
  if (failure === null) {
    return null
  }
  switch (failure) {
    case 'theLaptopWasNotReached':
      return 'startingUp.laptopNotReached'
    case 'theLaptopCouldNotAnswer':
      return 'startingUp.couldNotStart'
    default:
      return assertNever(failure)
  }
}
