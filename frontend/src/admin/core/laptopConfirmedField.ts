export interface LaptopConfirmedField<T> {
  edited: T
  confirmedByTheLaptop: T
}

export function laptopConfirmedField<T>(
  edited: T,
  confirmedByTheLaptop: T,
): LaptopConfirmedField<T> {
  return { edited, confirmedByTheLaptop }
}

export function stillDiffersFromTheLaptop<T>(
  field: LaptopConfirmedField<T>,
  isTheSameValue: (edited: T, confirmedByTheLaptop: T) => boolean = Object.is,
): boolean {
  return !isTheSameValue(field.edited, field.confirmedByTheLaptop)
}
