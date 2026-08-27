<script setup lang="ts">
import { computed, ref } from 'vue'
import { useOrderStore } from '../stores/order'
import { currentRoute } from '../router'
import OrderDetail from '../components/orders/OrderDetail.vue'

const order = useOrderStore()
const detail = ref<InstanceType<typeof OrderDetail> | null>(null)

const orderId = computed(() => {
  const route = currentRoute.value
  return route.name === 'orderDetail' ? route.orderId : ''
})

const shownOrder = computed(() => order.orderById(orderId.value))

async function answer(ticketId: string, slipIsOnThePile: boolean): Promise<void> {
  const noticeKey = await order.answerUnknown(orderId.value, ticketId, slipIsOnThePile)
  if (noticeKey !== null) {
    detail.value?.showNotice(ticketId, noticeKey)
  }
}

async function reprint(ticketId: string): Promise<void> {
  await order.reprint(orderId.value, ticketId)
}
</script>

<template>
  <OrderDetail
    v-if="shownOrder !== null"
    ref="detail"
    :order="shownOrder"
    @answer="answer"
    @reprint="reprint"
  />
</template>
