<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../shared/core/assertNever'
import type { PositionState } from '../../core/openItems'

const props = defineProps<{
  itemName: string
  note: string | null
  orderLabel: string | null
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
    case 'unknown':
      return null
    default:
      return assertNever(props.productionState)
  }
})

const stateIcon = computed(() => {
  switch (props.productionState) {
    case 'produced':
      return 'mdi-check'
    case 'notProduced':
      return 'mdi-clock-outline'
    case 'unknown':
      return null
    default:
      return assertNever(props.productionState)
  }
})
</script>

<template>
  <v-list-item
    class="open-line"
    :class="stateClass"
    :disabled="isDisabled"
    @click="emit('toggle')"
  >
    <template #prepend>
      <v-checkbox-btn
        v-if="!isSettled"
        class="line-tick"
        readonly
        :model-value="isSelected"
      />
    </template>
    <v-list-item-title class="line-name">{{ itemName }}</v-list-item-title>
    <v-list-item-subtitle v-if="orderLabel !== null" class="line-origin">
      {{ orderLabel }}
    </v-list-item-subtitle>
    <v-list-item-subtitle v-if="note !== null" class="line-note">
      {{ t('openItems.itemNote', { note }) }}
    </v-list-item-subtitle>
    <template #append>
      <v-icon v-if="stateIcon !== null" class="line-state" :icon="stateIcon" size="small" />
      <span v-if="isSettled" class="line-paid text-body-1">{{ t('openItems.paid') }}</span>
      <span class="line-price text-body-1">{{ priceText }}</span>
    </template>
  </v-list-item>
</template>

<style scoped>
.line-name,
.line-origin,
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

.open-line.is-produced {
  background: color-mix(in srgb, rgb(var(--v-theme-success)) 38%, rgb(var(--v-theme-surface)));
}

.open-line.is-not-produced {
  background: color-mix(in srgb, rgb(var(--v-theme-error)) 38%, rgb(var(--v-theme-surface)));
}

.open-line.is-produced .line-state {
  color: rgb(var(--v-theme-success));
}

.open-line.is-not-produced .line-state {
  color: rgb(var(--v-theme-error));
}
</style>
