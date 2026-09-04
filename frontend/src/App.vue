<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { currentRoute } from './router'
import { bindLocaleToSession } from './localeBinding'
import { assertNever } from './core/assertNever'
import { useSessionStore } from './stores/session'
import { useCatalogStore } from './stores/catalog'
import { usePrinterStatusStore } from './stores/printerStatus'
import { useConnectionStore } from './stores/connection'
import AppHeader from './components/header/AppHeader.vue'
import AppNotices from './components/header/AppNotices.vue'
import EnrolQr from './views/EnrolQr.vue'
import Welcome from './views/Welcome.vue'
import Catalog from './views/Catalog.vue'
import Review from './views/Review.vue'
import StationPage from './views/StationPage.vue'
import AdminShell from './views/admin/AdminShell.vue'

const session = useSessionStore()
const catalog = useCatalogStore()
const printerStatus = usePrinterStatusStore()
const connection = useConnectionStore()

bindLocaleToSession()

type ScreenName =
  | 'enrolQr'
  | 'welcome'
  | 'catalog'
  | 'review'
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

watch(
  () => session.deviceToken,
  async (token) => {
    if (token === null || screen.value === 'admin') {
      return
    }
    await connection.connect({ deviceToken: token })
  },
)

onMounted(async () => {
  if (screen.value === 'admin') {
    await connection.connect({})
    return
  }
  session.listenForRevocation()
  catalog.listen()
  printerStatus.listen()
  await session.loadSession()
  if (session.deviceToken !== null) {
    await connection.connect({ deviceToken: session.deviceToken })
    await catalog.load()
    await printerStatus.load()
  }
})
</script>

<template>
  <v-app>
    <AppHeader v-if="showsHeader" />
    <v-main>
    <AppNotices v-if="showsHeader" />
    <EnrolQr v-if="screen === 'enrolQr'" />
    <Welcome v-else-if="screen === 'welcome'" />
    <Catalog v-else-if="screen === 'catalog'" />
    <Review v-else-if="screen === 'review'" />
    <StationPage v-else-if="screen === 'stations'" />
    <AdminShell v-else />
    </v-main>
  </v-app>
</template>
