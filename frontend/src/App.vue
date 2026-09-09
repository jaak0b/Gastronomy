<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { currentRoute } from './router'
import { bindLocaleToSession } from './localeBinding'
import { screenFor, type ScreenName } from './core/landing'
import { useSessionStore } from './stores/session'
import { useCatalogStore } from './stores/catalog'
import { useConnectionStore } from './stores/connection'
import AppHeader from './components/header/AppHeader.vue'
import AppNotices from './components/header/AppNotices.vue'
import EnrolQr from './views/EnrolQr.vue'
import Welcome from './views/Welcome.vue'
import StartingUp from './views/StartingUp.vue'
import Catalog from './views/Catalog.vue'
import Review from './views/Review.vue'
import OpenItems from './views/OpenItems.vue'
import StationPage from './views/StationPage.vue'
import AdminShell from './views/admin/AdminShell.vue'

const session = useSessionStore()
const catalog = useCatalogStore()
const connection = useConnectionStore()

bindLocaleToSession()

const screen = computed<ScreenName>(() => screenFor(session.deviceSession, currentRoute.value))

const isAWaiterScreen = computed(
  () => screen.value === 'catalog' || screen.value === 'review' || screen.value === 'openItems',
)

let isListeningToTheCatalog = false

async function followTheCatalog(): Promise<void> {
  if (!isListeningToTheCatalog) {
    isListeningToTheCatalog = true
    catalog.listen()
  }
  await catalog.load()
}

watch(
  () => session.deviceToken,
  async (token) => {
    if (token === null || screen.value === 'admin') {
      return
    }
    await connection.connect({ deviceToken: token })
  },
)

watch(isAWaiterScreen, async (isTheWaiterApp) => {
  if (isTheWaiterApp) {
    await followTheCatalog()
  }
})

onMounted(async () => {
  if (screen.value === 'admin') {
    await connection.connect({})
    return
  }
  session.watchForBeingSignedOut()
  await session.loadSession()
  if (session.deviceToken === null) {
    return
  }
  await connection.connect({ deviceToken: session.deviceToken })
  if (isAWaiterScreen.value) {
    await followTheCatalog()
  }
})
</script>

<template>
  <v-app>
    <AppHeader v-if="isAWaiterScreen" />
    <v-main>
    <AppNotices v-if="isAWaiterScreen" />
    <EnrolQr v-if="screen === 'enrolQr'" />
    <Welcome v-else-if="screen === 'welcome'" />
    <StartingUp v-else-if="screen === 'startingUp'" />
    <Catalog v-else-if="screen === 'catalog'" />
    <Review v-else-if="screen === 'review'" />
    <OpenItems v-else-if="screen === 'openItems'" />
    <StationPage v-else-if="screen === 'station'" />
    <AdminShell v-else />
    </v-main>
  </v-app>
</template>
