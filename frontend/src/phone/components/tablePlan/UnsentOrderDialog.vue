<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useBackStep } from '../../../shared/composables/useBackStep'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatPrice } from '../../../shared/core/money'

const props = defineProps<{
  draftTableName: string
  itemCount: number
  totalCents: number
  language: AppLanguage
}>()
const emit = defineEmits<{ 'back-to-the-draft': []; 'discard-the-draft': []; 'stay-on-the-plan': [] }>()

const { t } = useI18n()

useBackStep(() => emit('stay-on-the-plan'))

const whatIsUnsent = computed(() =>
  t(
    'phone.tablePlan.messages.unsentOrder',
    {
      table: props.draftTableName,
      count: props.itemCount,
      total: formatPrice(props.totalCents, props.language),
    },
    props.itemCount,
  ),
)
</script>

<template>
  <v-dialog :model-value="true" max-width="480" persistent scrim="background" opacity="1">
    <v-card class="unsent-order" data-test="unsent-order">
      <v-card-text class="unsent-order-text app-text" data-test="unsent-order-text">
        {{ whatIsUnsent }}
      </v-card-text>
      <v-card-actions class="flex-column align-stretch ga-2 pb-4">
        <v-btn-wide
          class="back-to-the-draft"
          data-test="back-to-the-draft"
          color="primary"
          @click="emit('back-to-the-draft')"
        >
          {{ t('phone.tablePlan.actions.toTheUnsentTable', { table: draftTableName }) }}
        </v-btn-wide>
        <v-btn-wide
          class="discard-the-draft"
          data-test="discard-the-draft"
          color="primary"
          variant="outlined"
          @click="emit('discard-the-draft')"
        >
          {{ t('phone.tablePlan.actions.discard') }}
        </v-btn-wide>
        <v-btn-wide
          class="stay-on-the-plan"
          data-test="stay-on-the-plan"
          variant="text"
          @click="emit('stay-on-the-plan')"
        >
          {{ t('common.actions.back') }}
        </v-btn-wide>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.unsent-order-text {
  overflow-wrap: anywhere;
}

.unsent-order .v-btn {
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  margin-inline-start: 0;
  text-transform: none;
  letter-spacing: normal;
}

.unsent-order .v-btn :deep(.v-btn__content) {
  white-space: normal;
}
</style>
