import { useI18n } from 'vue-i18n'
import { assertNever } from '../../shared/core/assertNever'
import type { AmountEntryUnit } from '../core/ingredientAmounts'

export function useAmountUnitName(): (entryUnit: AmountEntryUnit) => string {
  const { t } = useI18n()
  return (entryUnit) => {
    switch (entryUnit) {
      case 'piece':
        return t('admin.ingredients.units.piece')
      case 'gram':
        return t('admin.ingredients.units.gram')
      case 'kilogram':
        return t('admin.ingredients.units.kilogram')
      case 'millilitre':
        return t('admin.ingredients.units.millilitre')
      case 'litre':
        return t('admin.ingredients.units.litre')
      default:
        return assertNever(entryUnit)
    }
  }
}
