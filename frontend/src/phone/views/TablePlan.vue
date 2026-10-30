<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TablePlanAreaView, TablePlanTableView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import { showsTheEmptyListText } from '../../shared/core/emptyList'
import { closeStep, navigate, openStep, openStepValue } from '../../shared/router/router'
import { openingTheOrderAt } from '../core/draftCart'
import { useDraftTableName } from '../composables/useDraftTableName'
import { useWaiterLanding } from '../composables/useWaiterLanding'
import { useCatalogStore } from '../stores/catalog'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore } from '../stores/order'
import { useOrderOverviewStore } from '../stores/orderOverview'
import { useTablePlanStore } from '../stores/tablePlan'
import { useSessionStore } from '../../shared/stores/session'
import BaseEmptyListText from '../../shared/components/BaseEmptyListText.vue'
import LoadFailureNotice from '../components/LoadFailureNotice.vue'
import AreaList from '../components/tablePlan/AreaList.vue'
import AreaPlan from '../components/tablePlan/AreaPlan.vue'
import TableActionSheet from '../components/tablePlan/TableActionSheet.vue'
import UnsentOrderDialog from '../components/tablePlan/UnsentOrderDialog.vue'

const { t } = useI18n()
const tablePlan = useTablePlanStore()
const catalog = useCatalogStore()
const order = useOrderStore()
const openItems = useOpenItemsStore()
const orderOverview = useOrderOverviewStore()
const session = useSessionStore()
const { landing } = useWaiterLanding()
const { draftTableName } = useDraftTableName()

const TABLE_STEP = 'table'

const tableAskingAboutTheDraft = ref<TablePlanTableView | null>(null)

const shownArea = computed<TablePlanAreaView | null>(() => {
  const shown = landing.value
  switch (shown.kind) {
    case 'plan':
      return tablePlan.areas.find((area) => area.areaId === shown.areaId) ?? null
    case 'lockedOrder':
    case 'noFestival':
    case 'noTables':
    case 'areaList':
      return null
    default:
      return assertNever(shown)
  }
})

const tappedTable = computed<TablePlanTableView | null>(() => {
  const tableId = openStepValue(TABLE_STEP)
  return shownArea.value?.tables.find((table) => table.tableId === tableId) ?? null
})

const showsNoTables = computed(
  () =>
    landing.value.kind === 'noTables' && showsTheEmptyListText(tablePlan, tablePlan.areas.length),
)

const showsNoTablesInArea = computed(
  () =>
    shownArea.value !== null && showsTheEmptyListText(tablePlan, shownArea.value.tables.length),
)

const whatIsRunningIsKnown = computed(
  () => tablePlan.hasLoaded && (tablePlan.areas.length > 0 || catalog.hasLoaded),
)

function goToTheLockedOrder(): void {
  if (landing.value.kind === 'lockedOrder') {
    navigate('/review', 'redirect')
  }
}

onMounted(goToTheLockedOrder)

watch(landing, goToTheLockedOrder)

function openTheTable(table: TablePlanTableView): void {
  openStep({ name: TABLE_STEP, value: table.tableId, kind: 'dialog' })
}

function closeTheTable(): void {
  closeStep(TABLE_STEP)
}

function openTheOrderFor(table: TablePlanTableView): void {
  order.chooseTable({ tableId: table.tableId, name: table.name })
  navigate('/catalog')
}

function takeTheOrder(): void {
  const table = tappedTable.value
  if (table === null) {
    return
  }
  const opening = openingTheOrderAt(order.draft, table.tableId)
  switch (opening) {
    case 'continueTheDraft':
      navigate('/catalog')
      return
    case 'takeTheTable':
      openTheOrderFor(table)
      return
    case 'askBeforeDiscardingTheDraft':
      closeTheTable()
      tableAskingAboutTheDraft.value = table
      return
    default:
      return assertNever(opening)
  }
}

function showTheOrdersOfTheTable(): void {
  const table = tappedTable.value
  if (table === null) {
    return
  }
  orderOverview.openTable(table.tableId)
  navigate('/order-overview')
}

function settleTheTable(): void {
  const table = tappedTable.value
  if (table === null) {
    return
  }
  openItems.openTable(table.tableId)
  navigate('/open-items')
}

function cancelItemsAtTheTable(): void {
  const table = tappedTable.value
  if (table === null) {
    return
  }
  openItems.openTableToCancelItems(table.tableId)
  navigate('/open-items')
}

function backToTheDraft(): void {
  tableAskingAboutTheDraft.value = null
  navigate('/catalog')
}

function discardTheDraft(): void {
  const table = tableAskingAboutTheDraft.value
  tableAskingAboutTheDraft.value = null
  if (table === null) {
    return
  }
  order.discardTheDraftAndChooseTable({ tableId: table.tableId, name: table.name })
  navigate('/catalog')
}
</script>

<template>
  <div class="table-plan" data-test="table-plan">
    <template v-if="!whatIsRunningIsKnown">
      <LoadFailureNotice
        v-if="!tablePlan.hasLoaded && tablePlan.loadFailed"
        class="pa-4"
        data-test="table-plan-load-failed"
        :text="t('phone.tablePlan.errors.loadFailed')"
        @reload="tablePlan.load()"
      />
      <v-progress-linear v-else indeterminate data-test="table-plan-loading" />
    </template>
    <p v-else-if="landing.kind === 'noFestival'" class="no-festival pa-4 app-text" data-test="no-festival">
      {{ t('phone.tablePlan.messages.noFestivalIsActive') }}
    </p>
    <BaseEmptyListText
      v-else-if="showsNoTables"
      class="no-tables pa-4 app-text"
      data-test="no-tables"
    />
    <AreaList
      v-else-if="landing.kind === 'areaList'"
      :areas="tablePlan.areas"
      @open-area="tablePlan.openArea"
    />
    <template v-else-if="landing.kind === 'plan' && shownArea !== null">
      <BaseEmptyListText
        v-if="showsNoTablesInArea"
        class="no-tables-in-area pa-4 mb-0 app-text"
        data-test="no-tables-in-area"
      />
      <AreaPlan :area="shownArea" @open-table="openTheTable" />
    </template>
    <TableActionSheet
      v-if="tappedTable !== null"
      :table-name="tappedTable.name"
      @take-order="takeTheOrder"
      @show-orders="showTheOrdersOfTheTable"
      @settle="settleTheTable"
      @cancel-items="cancelItemsAtTheTable"
      @back="closeTheTable"
    />
    <UnsentOrderDialog
      v-if="tableAskingAboutTheDraft !== null && draftTableName !== null"
      :draft-table-name="draftTableName"
      :item-count="order.itemCount"
      :total-cents="order.totalCents"
      :language="session.language"
      @back-to-the-draft="backToTheDraft"
      @discard-the-draft="discardTheDraft"
      @stay-on-the-plan="tableAskingAboutTheDraft = null"
    />
  </div>
</template>

<style scoped>
.table-plan {
  display: flex;
  flex: 1 1 0;
  flex-direction: column;
  min-height: 0;
  overflow-y: auto;
}
</style>
