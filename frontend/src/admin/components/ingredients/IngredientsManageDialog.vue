<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminIngredientsStore, type IngredientDraft } from '../../stores/ingredients'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'
import IngredientEditLine from './IngredientEditLine.vue'

const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const ingredients = useAdminIngredientsStore()
const { refusal, refusalText, showRefusalOf } = useRefusalDisplay()
const deactivatedIngredientId = ref<string | null>(null)

async function saveIngredient(ingredientId: string, draft: IngredientDraft): Promise<void> {
  refusal.value = null
  showRefusalOf(await ingredients.save(ingredientId, draft))
}

async function activateIngredient(ingredientId: string): Promise<void> {
  refusal.value = null
  showRefusalOf(await ingredients.setActive(ingredientId, true))
}

async function deactivateIngredient(): Promise<void> {
  const ingredientId = deactivatedIngredientId.value
  deactivatedIngredientId.value = null
  if (ingredientId !== null) {
    refusal.value = null
    showRefusalOf(await ingredients.setActive(ingredientId, false))
  }
}
</script>

<template>
  <BaseFormDialog
    :title="t('admin.ingredients.title')"
    :error-text="refusalText"
    :cancel-label="t('admin.ingredients.actions.close')"
    close-only
    @cancel="emit('close')"
  >
    <p v-if="ingredients.ingredients.length === 0" class="no-ingredients text-body-1">
      {{ t('admin.ingredients.messages.noneYet') }}
    </p>
    <IngredientEditLine
      v-for="ingredient in ingredients.ingredients"
      :key="ingredient.ingredientId"
      :ingredient="ingredient"
      @save="saveIngredient(ingredient.ingredientId, $event)"
      @activate="activateIngredient(ingredient.ingredientId)"
      @deactivate="deactivatedIngredientId = ingredient.ingredientId"
    />
  </BaseFormDialog>
  <BaseConfirmDialog
    v-if="deactivatedIngredientId !== null"
    :title="t('admin.ingredients.labels.deactivateTitle')"
    :body="t('admin.ingredients.messages.deactivateCost')"
    :confirm-label="t('admin.ingredients.actions.deactivateConfirm')"
    @confirm="deactivateIngredient"
    @cancel="deactivatedIngredientId = null"
  />
</template>
