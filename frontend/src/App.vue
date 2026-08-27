<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { currentRoute } from './router'
import { assertNever } from './core/assertNever'
import { useSessionStore } from './stores/session'
import { useCatalogStore } from './stores/catalog'
import { useOrderStore } from './stores/order'
import { usePrinterStatusStore } from './stores/printerStatus'
import { useConnectionStore } from './stores/connection'
import AppHeader from './components/header/AppHeader.vue'
import EnrolQr from './views/EnrolQr.vue'
import EnrolCode from './views/EnrolCode.vue'
import Catalog from './views/Catalog.vue'
import Review from './views/Review.vue'
import Orders from './views/Orders.vue'
import OrderDetailPage from './views/OrderDetailPage.vue'
import StationPage from './views/StationPage.vue'
import AdminShell from './views/admin/AdminShell.vue'

const session = useSessionStore()
const catalog = useCatalogStore()
const order = useOrderStore()
const printerStatus = usePrinterStatusStore()
const connection = useConnectionStore()
const { locale } = useI18n()

type ScreenName =
  | 'enrolQr'
  | 'enrolCode'
  | 'catalog'
  | 'review'
  | 'orders'
  | 'orderDetail'
  | 'station'
  | 'admin'

const screen = computed<ScreenName>(() => {
  const route = currentRoute.value
  switch (route.name) {
    case 'enrolQr':
      return 'enrolQr'
    case 'home':
      return session.isEnrolled ? 'catalog' : 'enrolCode'
    case 'review':
      return 'review'
    case 'orders':
      return 'orders'
    case 'orderDetail':
      return 'orderDetail'
    case 'station':
      return 'station'
    case 'admin':
      return 'admin'
    default:
      return assertNever(route)
  }
})

const showsHeader = computed(
  () => screen.value !== 'station' && screen.value !== 'admin' && session.isEnrolled,
)

watch(
  () => session.language,
  (next) => {
    locale.value = next
  },
  { immediate: true },
)

onMounted(async () => {
  if (screen.value === 'station' || screen.value === 'admin') {
    return
  }
  session.listenForRevocation()
  catalog.listen()
  order.listen()
  printerStatus.listen()
  await session.loadSession()
  if (session.deviceToken !== null) {
    await connection.connect(session.deviceToken)
    await catalog.load()
    await order.loadMine()
    await printerStatus.load()
  }
})
</script>

<template>
  <AppHeader v-if="showsHeader" />
  <main>
    <EnrolQr v-if="screen === 'enrolQr'" />
    <EnrolCode v-else-if="screen === 'enrolCode'" />
    <Catalog v-else-if="screen === 'catalog'" />
    <Review v-else-if="screen === 'review'" />
    <Orders v-else-if="screen === 'orders'" />
    <OrderDetailPage v-else-if="screen === 'orderDetail'" />
    <StationPage v-else-if="screen === 'station'" />
    <AdminShell v-else />
  </main>
</template>
