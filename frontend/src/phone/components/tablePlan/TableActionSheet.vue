<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ tableName: string }>()
const emit = defineEmits<{ 'take-order': []; 'show-orders': []; settle: []; 'cancel-items': []; back: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-bottom-sheet :model-value="true" @update:model-value="emit('back')">
    <v-card class="table-actions" data-test="table-actions">
      <v-card-title class="table-actions-title" data-test="table-actions-title">
        {{ t('common.labels.table', { name: tableName }) }}
      </v-card-title>
      <v-card-actions class="flex-column align-stretch ga-2 pb-4">
        <v-btn
          class="take-order"
          data-test="take-order"
          color="primary"
          variant="flat"
          size="x-large"
          block
          @click="emit('take-order')"
        >
          {{ t('phone.tablePlan.actions.takeOrder') }}
        </v-btn>
        <v-btn
          class="show-orders"
          data-test="show-orders"
          color="primary"
          variant="outlined"
          size="x-large"
          block
          @click="emit('show-orders')"
        >
          {{ t('phone.tablePlan.actions.showOrders') }}
        </v-btn>
        <v-btn
          class="settle-table"
          data-test="settle-table"
          color="primary"
          variant="outlined"
          size="x-large"
          block
          @click="emit('settle')"
        >
          {{ t('phone.openItems.actions.settleItems') }}
        </v-btn>
        <v-btn
          class="cancel-items"
          data-test="cancel-items"
          color="primary"
          variant="outlined"
          size="x-large"
          block
          @click="emit('cancel-items')"
        >
          {{ t('phone.openItems.actions.cancelItems') }}
        </v-btn>
        <v-btn
          class="back-from-table-actions"
          data-test="back-from-table-actions"
          variant="text"
          size="x-large"
          block
          @click="emit('back')"
        >
          {{ t('common.actions.back') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-bottom-sheet>
</template>

<style scoped>
.table-actions-title {
  white-space: normal;
  overflow-wrap: anywhere;
}

.table-actions .v-btn {
  margin-inline-start: 0;
}
</style>
