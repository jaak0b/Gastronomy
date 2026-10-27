<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { statusTintCssOf } from '../../../shared/core/statusTints'

const props = defineProps<{
  itemName: string
  note: string | null
  priceText: string
  isSelected: boolean
  isSelectable: boolean
  isCancelled: boolean
  isShownAsPaid: boolean
}>()
const emit = defineEmits<{ toggle: [] }>()

const { t } = useI18n()

const paidBackground = computed(() =>
  props.isShownAsPaid ? { background: statusTintCssOf('success') } : {},
)
</script>

<template>
  <v-list-item
    class="open-line"
    data-test="open-line"
    :class="{ 'cancelled-item': isCancelled, 'paid-line': isShownAsPaid }"
    :style="paidBackground"
    :disabled="isCancelled"
    :link="isSelectable"
    @click="emit('toggle')"
  >
    <template #prepend>
      <v-checkbox-btn
        v-if="isSelectable"
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
      <span v-if="isShownAsPaid" class="line-paid text-body-1" data-test="line-paid">{{ t('phone.openItems.labels.paid') }}</span>
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
.line-paid {
  flex: 0 0 auto;
  white-space: nowrap;
  padding-inline-start: 0.75rem;
}

.open-line {
  min-height: 0;
  padding-block: 0.75rem;
}
</style>
