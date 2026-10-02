import { useI18n } from 'vue-i18n'
import type { IngredientUnit } from '../../shared/api/generatedSchemas'
import { appLanguageOf } from '../../shared/core/deviceLanguage'
import { formatAmount } from '../core/ingredientAmounts'

export function useIngredientAmountText(): (baseAmount: number, unit: IngredientUnit) => string {
  const { t, locale } = useI18n()
  return (baseAmount, unit) =>
    formatAmount(baseAmount, unit, appLanguageOf(locale.value), (formattedCount, count) =>
      t('admin.ingredients.labels.pieceCount', { amount: formattedCount }, count),
    )
}
