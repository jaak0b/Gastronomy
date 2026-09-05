export type CountedNameWording = (key: string, values: { count: number; item: string }) => string

export function countedName(portions: number, name: string, t: CountedNameWording): string {
  if (portions < 1) {
    return name
  }
  return t('review.line', { count: portions, item: name })
}
