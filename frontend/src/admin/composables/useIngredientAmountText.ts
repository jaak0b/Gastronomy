import { useI18n } from 'vue-i18n'
import type { IngredientUnit } from '../../shared/api/generatedSchemas'
import { appLanguageOf } from '../../shared/core/deviceLanguage'
import {
  displayedAmountIn,
  shownEntryUnitFor,
  type AmountEntryUnit,
} from '../core/ingredientAmounts'
import { useAmountUnitName } from './useAmountUnitName'

export function useAmountTextInEntryUnit(): (baseAmount: number, entryUnit: AmountEntryUnit) => string {
  const { t, locale } = useI18n()
  const unitName = useAmountUnitName()
  return (baseAmount, entryUnit) => {
    const shown = displayedAmountIn(baseAmount, entryUnit, appLanguageOf(locale.value))
    return shown.entryUnit === 'piece'
      ? t('admin.ingredients.labels.pieceCount', { amount: shown.formattedCount }, shown.count)
      : `${shown.formattedCount} ${unitName(shown.entryUnit)}`
  }
}

export function useIngredientAmountText(): (baseAmount: number, unit: IngredientUnit) => string {
  const amountTextIn = useAmountTextInEntryUnit()
  return (baseAmount, unit) => amountTextIn(baseAmount, shownEntryUnitFor(baseAmount, unit))
}
