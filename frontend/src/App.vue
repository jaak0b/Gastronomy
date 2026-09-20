<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { currentRoute } from './shared/router/router'
import {
  backButtonTrapNeedsBuilding,
  markBackButtonTrapActive,
} from './shared/router/backButtonTrap'
import { useLocaleBinding } from './shared/composables/useLocaleBinding'
import { screenTitle } from './shared/core/appTitle'
import { isPhoneScreen, screenFor, type ScreenName } from './shared/core/landing'
import { useSessionStore } from './shared/stores/session'
import { useStationStore } from './stores/station'
import { useCatalogStore } from './phone/stores/catalog'
import { useConnectionStore } from './shared/stores/connection'
import { useEstimatesStore } from './phone/stores/estimates'
import AppHeader from './phone/components/header/AppHeader.vue'
import AppNotices from './phone/components/header/AppNotices.vue'
import EnrolQr from './shared/views/EnrolQr.vue'
import DoorGate from './shared/views/DoorGate.vue'
import Welcome from './shared/views/Welcome.vue'
import StartingUp from './shared/views/StartingUp.vue'
import Catalog from './phone/views/Catalog.vue'
import Review from './phone/views/Review.vue'
import OpenItems from './phone/views/OpenItems.vue'
import StationPage from './views/StationPage.vue'
import AdminShell from './views/admin/AdminShell.vue'

const session = useSessionStore()
const station = useStationStore()
const catalog = useCatalogStore()
const connection = useConnectionStore()
const estimates = useEstimatesStore()

const { t } = useI18n()

useLocaleBinding(() => session.language)

const screen = computed<ScreenName>(() => {
  if (backButtonTrapNeedsBuilding()) {
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
    markBackButtonTrapActive(currentRoute.value)
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

const isAWaiterScreen = computed(() => isPhoneScreen(screen.value))

const showsTheAppBar = computed(() => isAWaiterScreen.value && screen.value !== 'review')

let catalogListenersStarted = false

function ensureCatalogListenersStarted(): void {
  if (catalogListenersStarted) {
    return
  }
  catalogListenersStarted = true
  catalog.listen()
  estimates.listen()
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
    ensureCatalogListenersStarted()
    await catalog.load()
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
    ensureCatalogListenersStarted()
    await catalog.load()
  }
})
</script>

<template>
  <v-app>
    <AppHeader v-if="showsTheAppBar" />
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
