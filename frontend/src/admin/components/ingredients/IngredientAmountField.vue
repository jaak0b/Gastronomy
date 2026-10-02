<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { IngredientUnit } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { appLanguageOf } from '../../../shared/core/deviceLanguage'
import {
  amountInputFor,
  amountInputSwitchedTo,
  entryUnitsFor,
  parseAmountInput,
  unitSymbolOf,
  type AmountEntryUnit,
  type ParsedAmountInput,
} from '../../core/ingredientAmounts'

const props = defineProps<{
  unit: IngredientUnit
  savedAmount: number | null
  label: string
  errorText?: string | null
}>()
const emit = defineEmits<{
  change: [parsed: ParsedAmountInput]
  commit: [parsed: ParsedAmountInput]
}>()

const { t, locale } = useI18n()
const language = computed(() => appLanguageOf(locale.value))
const startingInput = amountInputFor(props.savedAmount, props.unit, language.value)
const typed = ref(startingInput.typed)
const entryUnit = ref<AmountEntryUnit>(startingInput.entryUnit)

const toggleUnits = computed(() =>
  entryUnitsFor(props.unit).flatMap((unit) =>
    unit === 'piece' ? [] : [{ unit, symbol: unitSymbolOf(unit, language.value) }],
  ),
)
const parsed = computed(() =>
  parseAmountInput({ typed: typed.value, entryUnit: entryUnit.value }),
)

watch(
  () => [props.savedAmount, props.unit] as const,
  ([savedAmount, unit]) => {
    const shown = amountInputFor(savedAmount, unit, language.value)
    typed.value = shown.typed
    entryUnit.value = shown.entryUnit
  },
)

watch(
  parsed,
  (current) => {
    emit('change', current)
  },
  { immediate: true },
)

function namesTheSavedAmount(current: ParsedAmountInput): boolean {
  switch (current.kind) {
    case 'amount':
      return current.baseAmount === props.savedAmount
    case 'empty':
      return props.savedAmount === null
    case 'unreadable':
      return false
    default:
      return assertNever(current)
  }
}

function commit(): void {
  if (!namesTheSavedAmount(parsed.value)) {
    emit('commit', parsed.value)
  }
}

function switchEntryUnit(switchedUnit: AmountEntryUnit): void {
  const switched = amountInputSwitchedTo(
    { typed: typed.value, entryUnit: entryUnit.value },
    switchedUnit,
    language.value,
  )
  typed.value = switched.typed
  entryUnit.value = switched.entryUnit
}

function leaveTheField(event: KeyboardEvent): void {
  if (event.target instanceof HTMLElement) {
    event.target.blur()
  }
}
</script>

<template>
  <div class="ingredient-amount-field d-flex align-start ga-2">
    <v-text-field
      v-model="typed"
      class="amount-input"
      inputmode="decimal"
      density="compact"
      hide-details="auto"
      :label="label"
      :error-messages="errorText ? [errorText] : []"
      @blur="commit"
      @keydown.enter.prevent="leaveTheField"
    />
    <v-btn-toggle
      v-if="toggleUnits.length > 0"
      :model-value="entryUnit"
      class="entry-unit-toggle"
      density="compact"
      mandatory
      variant="outlined"
      @update:model-value="switchEntryUnit"
    >
      <v-btn
        v-for="option in toggleUnits"
        :key="option.unit"
        :class="`entry-unit-${option.unit}`"
        :value="option.unit"
      >
        {{ option.symbol }}
      </v-btn>
    </v-btn-toggle>
    <span v-else class="pieces-word text-body-1 pt-2">{{ t('admin.ingredients.labels.pieces') }}</span>
  </div>
</template>

<style scoped>
.amount-input {
  flex: 0 0 9rem;
}
</style>
