<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { showsTheEmptyListText } from '../../shared/core/emptyList'
import { navigate } from '../../shared/router/router'
import { useSessionStore } from '../../shared/stores/session'
import { useOrderOverviewStore } from '../stores/orderOverview'
import BaseEmptyListText from '../../shared/components/BaseEmptyListText.vue'
import OrderListFilter from '../components/orderList/OrderListFilter.vue'
import OrderListTitle from '../components/orderList/OrderListTitle.vue'
import OrderListCard from '../components/orderList/OrderListCard.vue'

const { t } = useI18n()
const orderOverview = useOrderOverviewStore()
const session = useSessionStore()
let stopListening: (() => void) | null = null

const orders = computed(() => orderOverview.report?.orders ?? [])

const showsEmptyListText = computed(() => showsTheEmptyListText(orderOverview, orders.value.length))

onMounted(async () => {
  if (orderOverview.tableId === null) {
    navigate('/', 'redirect')
    return
  }
  stopListening = orderOverview.listen()
  await orderOverview.load()
})

onUnmounted(() => {
  orderOverview.closeTable()
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="order-overview">
    <OrderListTitle data-test="order-overview-title" :text="t('phone.orderOverview.title')" />
    <OrderListFilter class="mb-3" :filter="orderOverview.filter" @change="orderOverview.changeFilter" />
    <v-alert
      v-if="orderOverview.loadFailureKey !== null"
      class="mb-2"
      data-test="load-failed"
      type="warning"
      variant="tonal"
    >
      {{ t(orderOverview.loadFailureKey) }}
    </v-alert>
    <OrderListCard v-for="order in orders" :key="order.orderId" :order="order" :language="session.language" />
    <BaseEmptyListText v-if="showsEmptyListText" data-test="no-orders" />
  </v-container>
</template>
