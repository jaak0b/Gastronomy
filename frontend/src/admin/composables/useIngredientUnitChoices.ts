import { computed, type ComputedRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { IngredientUnit } from '../../shared/api/generatedSchemas'

export interface IngredientUnitChoice {
  value: IngredientUnit
  title: string
}

export function useIngredientUnitChoices(): ComputedRef<IngredientUnitChoice[]> {
  const { t } = useI18n()
  return computed(() => [
    { value: 'piece', title: t('admin.ingredients.labels.unitPiece') },
    { value: 'gram', title: t('admin.ingredients.labels.unitGram') },
    { value: 'millilitre', title: t('admin.ingredients.labels.unitMillilitre') },
  ])
}
