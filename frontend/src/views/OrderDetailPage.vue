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

async function answer(stationOrderId: string, slipIsOnThePile: boolean): Promise<void> {
  const noticeKey = await order.answerUnknown(orderId.value, stationOrderId, slipIsOnThePile)
  if (noticeKey !== null) {
    detail.value?.showNotice(stationOrderId, noticeKey)
  }
}

async function printAnotherCopy(stationOrderId: string): Promise<void> {
  await order.printAnotherCopy(orderId.value, stationOrderId)
}
</script>

<template>
  <OrderDetail
    v-if="shownOrder !== null"
    ref="detail"
    :order="shownOrder"
    @answer="answer"
    @print-another-copy="printAnotherCopy"
  />
</template>
