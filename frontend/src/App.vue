<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { currentRoute } from './router'
import { bindLocaleToSession } from './localeBinding'
import { assertNever } from './core/assertNever'
import { useSessionStore } from './stores/session'
import { useCatalogStore } from './stores/catalog'
import { useOrderStore } from './stores/order'
import { usePrinterStatusStore } from './stores/printerStatus'
import { useConnectionStore } from './stores/connection'
import AppHeader from './components/header/AppHeader.vue'
import EnrolQr from './views/EnrolQr.vue'
import Welcome from './views/Welcome.vue'
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

bindLocaleToSession()

type ScreenName =
  | 'enrolQr'
  | 'welcome'
  | 'catalog'
  | 'review'
  | 'orders'
  | 'orderDetail'
  | 'stations'
  | 'admin'

const screen = computed<ScreenName>(() => {
  const route = currentRoute.value
  switch (route.name) {
    case 'enrolQr':
      return 'enrolQr'
    case 'home':
      return session.isEnrolled ? 'catalog' : 'welcome'
    case 'review':
      return session.isEnrolled ? 'review' : 'welcome'
    case 'orders':
      return session.isEnrolled ? 'orders' : 'welcome'
    case 'orderDetail':
      return session.isEnrolled ? 'orderDetail' : 'welcome'
    case 'stations':
      return session.isEnrolled ? 'stations' : 'welcome'
    case 'admin':
      return 'admin'
    default:
      return assertNever(route)
  }
})

const showsHeader = computed(
  () => screen.value !== 'admin' && session.isEnrolled,
)

onMounted(async () => {
  if (screen.value === 'admin') {
    return
  }
  session.listenForRevocation()
  catalog.listen()
  order.listen()
  printerStatus.listen()
  await session.loadSession()
  if (session.deviceToken !== null) {
    await connection.connect({ deviceToken: session.deviceToken })
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
    <Welcome v-else-if="screen === 'welcome'" />
    <Catalog v-else-if="screen === 'catalog'" />
    <Review v-else-if="screen === 'review'" />
    <Orders v-else-if="screen === 'orders'" />
    <OrderDetailPage v-else-if="screen === 'orderDetail'" />
    <StationPage v-else-if="screen === 'stations'" />
    <AdminShell v-else />
  </main>
</template>
