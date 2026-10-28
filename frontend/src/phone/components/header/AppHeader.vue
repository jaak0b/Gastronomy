<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../shared/core/assertNever'
import { currentRoute, navigate } from '../../../shared/router/router'
import { useDraftTableName } from '../../composables/useDraftTableName'
import { useWaiterLanding } from '../../composables/useWaiterLanding'
import { useOpenItemsStore } from '../../stores/openItems'
import { useOrderOverviewStore } from '../../stores/orderOverview'
import { useTablePlanStore } from '../../stores/tablePlan'
import SettingsSheet from './SettingsSheet.vue'

type BackDestination = 'areaList' | 'tablePlan' | 'none'

type ReloadableList = 'openItems' | 'orderOverview' | 'none'

const { t } = useI18n()
const tablePlan = useTablePlanStore()
const openItems = useOpenItemsStore()
const orderOverview = useOrderOverviewStore()
const { landing } = useWaiterLanding()
const { draftTableName } = useDraftTableName()
const settingsAreOpen = ref(false)

const shownAreaName = computed(() => {
  const shown = landing.value
  return shown.kind === 'plan'
    ? (tablePlan.areas.find((area) => area.areaId === shown.areaId)?.name ?? null)
    : null
})

const backDestination = computed<BackDestination>(() => {
  const route = currentRoute.value
  switch (route.name) {
    case 'home':
      return shownAreaName.value !== null && tablePlan.areas.length > 1 ? 'areaList' : 'none'
    case 'catalog':
    case 'openItems':
    case 'orderOverview':
      return 'tablePlan'
    case 'review':
    case 'enrolQr':
    case 'stations':
    case 'admin':
      return 'none'
    default:
      return assertNever(route)
  }
})

const backLabel = computed<string | null>(() => {
  const destination = backDestination.value
  switch (destination) {
    case 'areaList':
      return t('phone.tablePlan.actions.backToAreas')
    case 'tablePlan':
      return t('phone.tablePlan.actions.backToThePlan')
    case 'none':
      return null
    default:
      return assertNever(destination)
  }
})

const shownTableName = computed<string | null>(() => {
  const route = currentRoute.value
  switch (route.name) {
    case 'catalog':
      return draftTableName.value
    case 'openItems':
      return openItems.report?.tableName ?? null
    case 'orderOverview':
      return orderOverview.report?.tableName ?? null
    case 'home':
    case 'review':
    case 'enrolQr':
    case 'stations':
    case 'admin':
      return null
    default:
      return assertNever(route)
  }
})

const title = computed(() =>
  shownTableName.value === null
    ? shownAreaName.value
    : t('common.labels.table', { name: shownTableName.value }),
)

const reloadableList = computed<ReloadableList>(() => {
  const route = currentRoute.value
  switch (route.name) {
    case 'openItems':
      return 'openItems'
    case 'orderOverview':
      return 'orderOverview'
    case 'home':
    case 'catalog':
    case 'review':
    case 'enrolQr':
    case 'stations':
    case 'admin':
      return 'none'
    default:
      return assertNever(route)
  }
})

async function reloadTheList(): Promise<void> {
  const list = reloadableList.value
  switch (list) {
    case 'openItems':
      await openItems.load()
      return
    case 'orderOverview':
      await orderOverview.load()
      return
    case 'none':
      return
    default:
      assertNever(list)
  }
}

function goBack(): void {
  const destination = backDestination.value
  switch (destination) {
    case 'areaList':
      tablePlan.closeArea()
      return
    case 'tablePlan':
      navigate('/')
      return
    case 'none':
      return
    default:
      assertNever(destination)
  }
}
</script>

<template>
  <v-app-bar class="app-header" data-test="app-header" height="72">
    <v-btn
      v-if="backLabel !== null"
      class="back-button ms-1"
      data-test="header-back"
      prepend-icon="mdi-arrow-left"
      variant="text"
      size="large"
      @click="goBack"
    >
      {{ backLabel }}
    </v-btn>
    <span v-if="title !== null" class="header-title text-h6 px-3" data-test="header-title">{{ title }}</span>
    <v-spacer />
    <v-btn
      v-if="reloadableList !== 'none'"
      class="reload"
      data-test="reload"
      prepend-icon="mdi-refresh"
      variant="text"
      height="64"
      stacked
      @click="reloadTheList"
    >
      <span class="reload-label text-caption">{{ t('phone.openItems.actions.reload') }}</span>
    </v-btn>
    <v-btn
      class="settings"
      data-test="settings"
      icon="mdi-cog"
      variant="text"
      @click="settingsAreOpen = true"
    >
      <v-icon icon="mdi-cog" />
      <v-tooltip activator="parent" location="bottom">{{ t('phone.header.actions.settings') }}</v-tooltip>
    </v-btn>
  </v-app-bar>
  <SettingsSheet v-if="settingsAreOpen" @close="settingsAreOpen = false" />
</template>

<style scoped>
.back-button,
.reload {
  text-transform: none;
  letter-spacing: normal;
}

.header-title {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
