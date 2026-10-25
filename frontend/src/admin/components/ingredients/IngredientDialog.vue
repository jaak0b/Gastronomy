<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminIngredientView } from '../../../shared/api/generatedSchemas'
import type { IngredientDraft } from '../../stores/ingredients'
import { useAdminUnitsStore } from '../../stores/units'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'

const props = defineProps<{ ingredient: AdminIngredientView | null; errorText: string | null }>()
const emit = defineEmits<{ save: [draft: IngredientDraft]; cancel: [] }>()

const { t } = useI18n()
const units = useAdminUnitsStore()
const name = ref(props.ingredient?.name ?? '')
const unitId = ref<string | null>(props.ingredient?.unitId ?? null)

const unitChoices = computed(() => {
  const activeUnits = units.units
    .filter((unit) => unit.isActive)
    .map((unit) => ({ value: unit.unitId, title: unit.name }))
  const current = props.ingredient
  if (current === null || activeUnits.some((choice) => choice.value === current.unitId)) {
    return activeUnits
  }
  return [...activeUnits, { value: current.unitId, title: current.unitName }]
})

const title = computed(() =>
  props.ingredient === null
    ? t('admin.ingredients.actions.new')
    : t('admin.ingredients.labels.editTitle'),
)

const isIncomplete = computed(() => name.value.trim().length === 0 || unitId.value === null)

function save(): void {
  const chosenUnitId = unitId.value
  if (isIncomplete.value || chosenUnitId === null) {
    return
  }
  emit('save', { name: name.value, unitId: chosenUnitId })
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
      v-model="unitId"
      class="ingredient-unit-field"
      data-test="ingredient-unit-field"
      :label="t('admin.ingredients.labels.unit')"
      :items="unitChoices"
    />
  </BaseFormDialog>
</template>
