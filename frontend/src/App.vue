<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { currentRoute, keepTheDeviceBehindTheDoor, needsTheDoorOpened } from './router'
import { bindLocaleToSession } from './localeBinding'
import { screenTitle } from './core/appTitle'
import { screenFor, type ScreenName } from './core/landing'
import { useSessionStore } from './stores/session'
import { useStationStore } from './stores/station'
import { useCatalogStore } from './stores/catalog'
import { useConnectionStore } from './stores/connection'
import { useEstimatesStore } from './stores/estimates'
import AppHeader from './components/header/AppHeader.vue'
import AppNotices from './components/header/AppNotices.vue'
import EnrolQr from './views/EnrolQr.vue'
import DoorGate from './views/DoorGate.vue'
import Welcome from './views/Welcome.vue'
import StartingUp from './views/StartingUp.vue'
import Catalog from './views/Catalog.vue'
import Review from './views/Review.vue'
import OpenItems from './views/OpenItems.vue'
import StationPage from './views/StationPage.vue'
import AdminShell from './views/admin/AdminShell.vue'

const session = useSessionStore()
const station = useStationStore()
const catalog = useCatalogStore()
const connection = useConnectionStore()
const estimates = useEstimatesStore()

const { t } = useI18n()

bindLocaleToSession()

const screen = computed<ScreenName>(() => {
  if (needsTheDoorOpened()) {
    return 'doorGate'
  }
  return screenFor(session.deviceSession, currentRoute.value)
})

watch(
  screen,
  (shown) => {
    if (shown === 'doorGate') {
      return
    }
    keepTheDeviceBehindTheDoor()
  },
  { immediate: true },
)

const stationName = computed(() => station.station?.name ?? session.station?.name ?? null)

const title = computed(() => screenTitle(screen.value, stationName.value, t))

watch(
  title,
  (next) => {
    document.title = next
  },
  { immediate: true },
)

const isAWaiterScreen = computed(
  () => screen.value === 'catalog' || screen.value === 'review' || screen.value === 'openItems',
)

let areTheWaiterListenersInPlace = false

async function followTheCatalog(): Promise<void> {
  if (!areTheWaiterListenersInPlace) {
    areTheWaiterListenersInPlace = true
    catalog.listen()
    estimates.listen()
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
  if (screen.value === 'doorGate') {
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
    <DoorGate v-if="screen === 'doorGate'" />
    <EnrolQr v-else-if="screen === 'enrolQr'" />
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
