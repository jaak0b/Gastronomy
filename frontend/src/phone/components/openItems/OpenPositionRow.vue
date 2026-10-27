<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../shared/core/assertNever'
import { statusTintCssOf, statusTokenOf, type StatusToken } from '../../../shared/core/statusTints'
import type { PositionState } from '../../core/openItems'

const props = defineProps<{
  itemName: string
  note: string | null
  priceText: string
  isSelected: boolean
  isDisabled: boolean
  isSettled: boolean
  productionState: PositionState
}>()
const emit = defineEmits<{ toggle: [] }>()

const { t } = useI18n()

const stateClass = computed(() => {
  switch (props.productionState) {
    case 'produced':
      return 'is-produced'
    case 'notProduced':
      return 'is-not-produced'
    case 'cancelled':
      return 'cancelled-item'
    default:
      return assertNever(props.productionState)
  }
})

const stateToken = computed<StatusToken | null>(() => {
  switch (props.productionState) {
    case 'produced':
      return statusTokenOf('all')
    case 'notProduced':
      return statusTokenOf('none')
    case 'cancelled':
      return null
    default:
      return assertNever(props.productionState)
  }
})

const tint = computed(() =>
  stateToken.value === null ? {} : { background: statusTintCssOf(stateToken.value) },
)

const stateIcon = computed<string | null>(() => {
  switch (props.productionState) {
    case 'produced':
      return 'mdi-check'
    case 'notProduced':
      return 'mdi-clock-outline'
    case 'cancelled':
      return null
    default:
      return assertNever(props.productionState)
  }
})

const canBeTicked = computed(() => {
  switch (props.productionState) {
    case 'produced':
    case 'notProduced':
      return !props.isSettled
    case 'cancelled':
      return false
    default:
      return assertNever(props.productionState)
  }
})
</script>

<template>
  <v-list-item
    class="open-line"
    data-test="open-line"
    :class="stateClass"
    :style="tint"
    :disabled="isDisabled"
    @click="emit('toggle')"
  >
    <template #prepend>
      <v-checkbox-btn
        v-if="canBeTicked"
        class="line-tick"
        data-test="line-tick"
        readonly
        :model-value="isSelected"
      />
    </template>
    <v-list-item-title class="line-name" data-test="line-name">{{ itemName }}</v-list-item-title>
    <v-list-item-subtitle v-if="note !== null" class="line-note" data-test="line-note">
      {{ t('common.labels.note', { note }) }}
    </v-list-item-subtitle>
    <template #append>
      <v-icon
        v-if="stateIcon !== null"
        class="line-state"
        :color="stateToken ?? undefined"
        data-test="line-state"
        :icon="stateIcon"
        size="small"
      />
      <span v-if="isSettled" class="line-paid text-body-1" data-test="line-paid">{{ t('phone.openItems.labels.paid') }}</span>
      <span class="line-price text-body-1" data-test="line-price">{{ priceText }}</span>
    </template>
  </v-list-item>
</template>

<style scoped>
.line-name,
.line-note {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  overflow-wrap: anywhere;
}

.line-price,
.line-paid,
.line-state {
  flex: 0 0 auto;
  white-space: nowrap;
}

.line-price,
.line-paid {
  padding-inline-start: 0.75rem;
}

.line-state {
  margin-inline-start: 0.5rem;
}

.open-line {
  min-height: 0;
  padding-block: 0.75rem;
}
</style>
