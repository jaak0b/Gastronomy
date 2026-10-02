<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../shared/core/assertNever'
import { appLanguageOf } from '../../../shared/core/deviceLanguage'
import {
  amountInputFor,
  amountInputSwitchedTo,
  parseAmountInput,
  type AmountEntryUnit,
  type ParsedAmountInput,
} from '../../core/ingredientAmounts'
import { useAmountUnitName } from '../../composables/useAmountUnitName'

const props = defineProps<{
  entryUnits: readonly AmountEntryUnit[]
  savedAmount: number | null
  label: string
  errorText?: string | null
}>()
const emit = defineEmits<{
  amountTyped: [parsed: ParsedAmountInput, entryUnit: AmountEntryUnit]
  amountChanged: [parsed: ParsedAmountInput]
}>()

const { locale } = useI18n()
const unitName = useAmountUnitName()
const language = computed(() => appLanguageOf(locale.value))
const startingInput = amountInputFor(props.savedAmount, props.entryUnits[0], language.value)
const typed = ref(startingInput.typed)
const entryUnit = ref<AmountEntryUnit>(startingInput.entryUnit)

const unitChoices = computed(() =>
  props.entryUnits.map((unit) => ({ value: unit, title: unitName(unit) })),
)
const parsed = computed(() =>
  parseAmountInput({ typed: typed.value, entryUnit: entryUnit.value }),
)

watch(
  () => props.savedAmount,
  (savedAmount) => {
    typed.value = amountInputFor(savedAmount, entryUnit.value, language.value).typed
  },
)

watch(
  () => props.entryUnits,
  (entryUnits) => {
    if (!entryUnits.includes(entryUnit.value)) {
      switchEntryUnit(entryUnits[0])
    }
  },
)

watch(
  [parsed, entryUnit],
  ([current, currentEntryUnit]) => {
    emit('amountTyped', current, currentEntryUnit)
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

function commitIfChanged(): void {
  if (!namesTheSavedAmount(parsed.value)) {
    emit('amountChanged', parsed.value)
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
      @blur="commitIfChanged"
      @keydown.enter.prevent="leaveTheField"
    />
    <v-select
      v-if="unitChoices.length > 1"
      :model-value="entryUnit"
      class="amount-unit-select"
      density="compact"
      hide-details
      :items="unitChoices"
      @update:model-value="switchEntryUnit"
    />
    <span v-else class="amount-unit text-body-1 pt-2">{{ unitName(entryUnit) }}</span>
  </div>
</template>

<style scoped>
.amount-input {
  flex: 0 0 9rem;
}

.amount-unit-select {
  flex: 0 0 6.5rem;
}
</style>
