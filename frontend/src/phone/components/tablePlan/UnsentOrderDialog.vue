<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ draftTableName: string; chosenTableName: string }>()
const emit = defineEmits<{ 'back-to-the-draft': []; 'discard-the-draft': [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog :model-value="true" max-width="480" persistent>
    <v-card class="unsent-order" data-test="unsent-order">
      <v-card-title class="unsent-order-title" data-test="unsent-order-title">
        {{ t('phone.tablePlan.messages.unsentOrder', { table: draftTableName }) }}
      </v-card-title>
      <v-card-actions class="flex-column align-stretch ga-2 pb-4">
        <v-btn
          class="back-to-the-draft"
          data-test="back-to-the-draft"
          color="primary"
          variant="flat"
          size="x-large"
          block
          @click="emit('back-to-the-draft')"
        >
          {{ t('phone.tablePlan.actions.backToTheUnsentOrder', { table: draftTableName }) }}
        </v-btn>
        <v-btn
          class="discard-the-draft"
          data-test="discard-the-draft"
          color="primary"
          variant="outlined"
          size="x-large"
          block
          @click="emit('discard-the-draft')"
        >
          {{ t('phone.tablePlan.actions.discardAndTakeTheOrder', { table: chosenTableName }) }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.unsent-order-title {
  white-space: normal;
  overflow-wrap: anywhere;
  line-height: 1.35;
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
