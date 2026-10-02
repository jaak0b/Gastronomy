import { useI18n } from 'vue-i18n'
import type { IngredientUnit } from '../../shared/api/generatedSchemas'
import { appLanguageOf } from '../../shared/core/deviceLanguage'
import { displayedAmountFor } from '../core/ingredientAmounts'
import { useAmountUnitName } from './useAmountUnitName'

export function useIngredientAmountText(): (baseAmount: number, unit: IngredientUnit) => string {
  const { t, locale } = useI18n()
  const unitName = useAmountUnitName()
  return (baseAmount, unit) => {
    const shown = displayedAmountFor(baseAmount, unit, appLanguageOf(locale.value))
    return shown.entryUnit === 'piece'
      ? t('admin.ingredients.labels.pieceCount', { amount: shown.formattedCount }, shown.count)
      : `${shown.formattedCount} ${unitName(shown.entryUnit)}`
  }
}
