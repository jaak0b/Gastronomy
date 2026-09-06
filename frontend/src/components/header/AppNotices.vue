<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../core/assertNever'
import { useConnectionStore } from '../../stores/connection'
import { useOrderStore } from '../../stores/order'

const { t } = useI18n()
const connection = useConnectionStore()
const order = useOrderStore()

const hasArrived = computed(
  () => order.sendState === 'accepted' && order.acceptedOrderNumber !== null,
)

const connectionKey = computed<string | null>(() => {
  const state = connection.state
  switch (state) {
    case 'connected':
      return connection.recovered ? 'header.backOnline' : null
    case 'reconnecting':
    case 'offline':
      return 'header.reconnecting'
    default:
      return assertNever(state)
  }
})
</script>

<template>
  <v-alert
    v-if="hasArrived"
    class="sent"
    type="success"
    variant="tonal"
    rounded="0"
    density="compact"
    @click="order.dismissConfirmation"
  >
    {{ t('review.sent', { number: order.acceptedOrderNumber }) }}
  </v-alert>
  <v-alert
    v-if="order.draftWasLost"
    class="draft-lost"
    type="warning"
    variant="tonal"
    rounded="0"
    density="compact"
    closable
    @click:close="order.dismissDraftLoss"
  >
    {{ t('order.draftLost') }}
  </v-alert>
  <v-alert
    v-if="connectionKey !== null"
    class="connection"
    type="info"
    variant="tonal"
    rounded="0"
    density="compact"
  >
    {{ t(connectionKey) }}
  </v-alert>
</template>
