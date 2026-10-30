<script setup lang="ts">
import { computed, onMounted, ref, useTemplateRef, watch, type ComponentPublicInstance } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../shared/core/assertNever'
import { appLanguageOf } from '../../../shared/core/deviceLanguage'
import {
  amountInputFor,
  parseAmountInput,
  type ParsedAmountInput,
} from '../../core/ingredientAmounts'

const props = withDefaults(
  defineProps<{
    unitName: string
    savedAmount: number | null
    label: string
    errorText?: string | null
    amountWhenCleared?: number | null
    focusOnMount?: boolean
  }>(),
  { amountWhenCleared: null, focusOnMount: false },
)
const emit = defineEmits<{
  amountTyped: [parsed: ParsedAmountInput]
  amountChanged: [parsed: ParsedAmountInput]
  focused: []
}>()

const { locale } = useI18n()
const language = computed(() => appLanguageOf(locale.value))
const typed = ref(amountInputFor(props.savedAmount, language.value))
const parsed = computed(() => parseAmountInput(typed.value))
const amountField = useTemplateRef<ComponentPublicInstance>('amountField')

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
  if (props.amountWhenCleared !== null && parsed.value.kind === 'empty') {
    typed.value = amountInputFor(props.amountWhenCleared, language.value)
  }
  if (!namesTheSavedAmount(parsed.value)) {
    emit('amountChanged', parsed.value)
  }
}

function leaveTheField(event: KeyboardEvent): void {
  if (event.target instanceof HTMLElement) {
    event.target.blur()
  }
}

function focusAndSelectTheText(): void {
  const fieldElement: unknown = amountField.value?.$el
  if (fieldElement instanceof HTMLElement) {
    const input = fieldElement.querySelector('input')
    input?.focus()
    input?.select()
  }
}

function focusIfAsked(): void {
  if (!props.focusOnMount) {
    return
  }
  focusAndSelectTheText()
  emit('focused')
}

onMounted(focusIfAsked)

watch(
  () => props.focusOnMount,
  (shouldFocus) => {
    if (shouldFocus) {
      focusIfAsked()
    }
  },
)
</script>

<template>
  <div class="ingredient-amount-field d-flex align-start">
    <v-text-field
      ref="amountField"
      v-model="typed"
      class="amount-input"
      data-test="amount-input"
      inputmode="decimal"
      hide-details="auto"
      :label="label"
      :error-messages="errorText ? [errorText] : []"
      @blur="commitIfChanged"
      @keydown.enter.prevent="leaveTheField"
    >
      <template #append-inner>
        <span
          class="amount-unit text-medium-emphasis"
          data-test="amount-unit"
        >
          {{ unitName }}
        </span>
      </template>
    </v-text-field>
  </div>
</template>

<style scoped>
.amount-input {
  flex: 0 0 var(--admin-amount-field-width);
}
</style>
