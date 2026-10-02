<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminIngredientView, IngredientUnit } from '../../../shared/api/generatedSchemas'
import type { IngredientDraft } from '../../stores/ingredients'
import { INGREDIENT_UNITS } from '../../core/ingredientAmounts'
import { useAmountUnitName } from '../../composables/useAmountUnitName'

const props = defineProps<{ ingredient: AdminIngredientView }>()
const emit = defineEmits<{ save: [draft: IngredientDraft]; deactivate: []; activate: [] }>()

const { t } = useI18n()
const unitName = useAmountUnitName()
const name = ref(props.ingredient.name)
const unit = ref<IngredientUnit>(props.ingredient.unit)

const unitChoices = computed(() =>
  INGREDIENT_UNITS.map((choice) => ({ value: choice, title: unitName(choice) })),
)

watch(
  () => props.ingredient.name,
  (savedName, previousSavedName) => {
    if (name.value === previousSavedName) {
      name.value = savedName
    }
  },
)

watch(
  () => props.ingredient.unit,
  (savedUnit) => {
    unit.value = savedUnit
  },
)

function saveTheName(): void {
  if (name.value !== props.ingredient.name) {
    emit('save', { name: name.value, unit: unit.value })
  }
}

function saveTheUnit(chosen: IngredientUnit): void {
  emit('save', { name: name.value, unit: chosen })
}

function leaveTheField(event: KeyboardEvent): void {
  if (event.target instanceof HTMLElement) {
    event.target.blur()
  }
}
</script>

<template>
  <div class="ingredient-edit-line d-flex align-center flex-wrap ga-2 py-1">
    <v-text-field
      v-model="name"
      class="ingredient-name-field flex-grow-1"
      maxlength="200"
      density="compact"
      hide-details
      :label="t('admin.ingredients.labels.name')"
      @blur="saveTheName"
      @keydown.enter.prevent="leaveTheField"
    />
    <v-select
      v-model="unit"
      class="ingredient-unit-field"
      density="compact"
      hide-details
      :label="t('admin.ingredients.labels.unit')"
      :items="unitChoices"
      @update:model-value="saveTheUnit"
    />
    <v-chip v-if="!ingredient.isActive" class="deactivated" size="small" color="grey">
      {{ t('admin.common.labels.deactivated') }}
    </v-chip>
    <v-btn
      v-if="ingredient.isActive"
      class="deactivate-ingredient"
      icon="mdi-eye-off"
      variant="text"
      color="error"
      @click="emit('deactivate')"
    />
    <v-btn v-else class="activate-ingredient" variant="text" @click="emit('activate')">
      {{ t('admin.ingredients.actions.activate') }}
    </v-btn>
  </div>
</template>

<style scoped>
.ingredient-unit-field {
  flex: 0 0 10rem;
}
</style>
