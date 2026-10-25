<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../shared/core/assertNever'
import { appLanguageOf } from '../../../shared/core/deviceLanguage'
import {
  amountInputFor,
  parseAmountInput,
  type ParsedAmountInput,
} from '../../core/ingredientAmounts'

const props = defineProps<{
  unitName: string
  savedAmount: number | null
  label: string
  errorText?: string | null
}>()
const emit = defineEmits<{
  amountTyped: [parsed: ParsedAmountInput]
  amountChanged: [parsed: ParsedAmountInput]
}>()

const { locale } = useI18n()
const language = computed(() => appLanguageOf(locale.value))
const typed = ref(amountInputFor(props.savedAmount, language.value))
const parsed = computed(() => parseAmountInput(typed.value))

watch(
  () => props.savedAmount,
  (savedAmount) => {
    typed.value = amountInputFor(savedAmount, language.value)
  },
)

watch(
  parsed,
  (current) => {
    emit('amountTyped', current)
  },
  { immediate: true },
)

function namesTheSavedAmount(current: ParsedAmountInput): boolean {
  switch (current.kind) {
    case 'amount':
      return current.amount === props.savedAmount
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
      data-test="amount-input"
      inputmode="decimal"
      density="compact"
      hide-details="auto"
      :label="label"
      :error-messages="errorText ? [errorText] : []"
      @blur="commitIfChanged"
      @keydown.enter.prevent="leaveTheField"
    />
    <span
      class="amount-unit text-body-1 pt-2"
      data-test="amount-unit"
    >
      {{ unitName }}
    </span>
  </div>
</template>

<style scoped>
.amount-input {
  flex: 0 0 9rem;
}
</style>
