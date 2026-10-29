<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../shared/core/assertNever'
import { useConnectionStore } from '../../../shared/stores/connection'
import { useOrderStore } from '../../stores/order'
import { PHONE_NOTICE_BAR_DEFAULTS } from '../../styles/phoneSizes'

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
      return connection.recovered ? 'phone.header.messages.backOnline' : null
    case 'connecting':
    case 'reconnecting':
    case 'offline':
      return 'phone.header.messages.reconnecting'
    default:
      return assertNever(state)
  }
})
</script>

<template>
  <v-defaults-provider :defaults="PHONE_NOTICE_BAR_DEFAULTS">
    <v-alert
      v-if="hasArrived"
      class="sent"
      data-test="sent"
      type="success"
      variant="tonal"
      @click="order.dismissConfirmation"
    >
      {{ t('phone.review.messages.sent', { number: order.acceptedOrderNumber }) }}
    </v-alert>
    <v-alert
      v-if="order.draftWasLost"
      class="draft-lost"
      data-test="draft-lost"
      type="warning"
      variant="tonal"
      closable
      @click:close="order.dismissDraftLoss"
    >
      {{ t('phone.header.errors.draftLost') }}
    </v-alert>
    <v-alert
      v-if="connectionKey !== null"
      class="connection"
      data-test="connection"
      type="info"
      variant="tonal"
    >
      {{ t(connectionKey) }}
    </v-alert>
  </v-defaults-provider>
</template>
