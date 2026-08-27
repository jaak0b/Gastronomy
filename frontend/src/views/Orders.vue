<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../stores/session'
import { navigate } from '../router'
import OrderList from '../components/orders/OrderList.vue'

const { t } = useI18n()
const order = useOrderStore()
const session = useSessionStore()
</script>

<template>
  <v-container class="orders">
    <h1 class="text-h5 mb-4">{{ t('orders.title') }}</h1>
    <OrderList
      :orders="order.orders"
      :language="session.language"
      @open="(orderId) => navigate(`/orders/${orderId}`)"
    />
  </v-container>
</template>
