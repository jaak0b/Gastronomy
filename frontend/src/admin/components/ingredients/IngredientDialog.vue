<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminIngredientView, IngredientUnit } from '../../../shared/api/generatedSchemas'
import type { IngredientDraft } from '../../stores/ingredients'
import { INGREDIENT_UNITS } from '../../core/ingredientAmounts'
import { useAmountUnitName } from '../../composables/useAmountUnitName'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'

const props = defineProps<{ ingredient: AdminIngredientView | null; errorText: string | null }>()
const emit = defineEmits<{ save: [draft: IngredientDraft]; cancel: [] }>()

const { t } = useI18n()
const unitName = useAmountUnitName()
const name = ref(props.ingredient?.name ?? '')
const unit = ref<IngredientUnit | null>(props.ingredient?.unit ?? null)

const unitChoices = computed(() =>
  INGREDIENT_UNITS.map((choice) => ({ value: choice, title: unitName(choice) })),
)

const title = computed(() =>
  props.ingredient === null
    ? t('admin.ingredients.actions.new')
    : t('admin.ingredients.labels.editTitle'),
)

const isIncomplete = computed(() => name.value.trim().length === 0 || unit.value === null)

function save(): void {
  const chosenUnit = unit.value
  if (isIncomplete.value || chosenUnit === null) {
    return
  }
  emit('save', { name: name.value, unit: chosenUnit })
}
</script>

<template>
  <BaseFormDialog
    :title="title"
    :error-text="errorText"
    :save-disabled="isIncomplete"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="ingredient-name-field mb-4"
      data-test="ingredient-name-field"
      maxlength="200"
      :label="t('admin.ingredients.labels.name')"
    />
    <v-select
      v-model="unit"
      class="ingredient-unit-field"
      data-test="ingredient-unit-field"
      :label="t('admin.ingredients.labels.unit')"
      :items="unitChoices"
    />
  </BaseFormDialog>
</template>
