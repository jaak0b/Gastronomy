import type { Translate } from '../../shared/core/translation'

export function countedName(portions: number, name: string, t: Translate): string {
  if (portions < 1) {
    return name
  }
  return t('review.line', { count: portions, item: name })
}
