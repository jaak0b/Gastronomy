<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TablePlanAreaView, TablePlanTableView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import { navigate } from '../../shared/router/router'
import { openingTheOrderAt } from '../core/draftCart'
import { useDraftTableName } from '../composables/useDraftTableName'
import { useWaiterLanding } from '../composables/useWaiterLanding'
import { useCatalogStore } from '../stores/catalog'
import { useOpenItemsStore } from '../stores/openItems'
import { useOrderStore } from '../stores/order'
import { useTablePlanStore } from '../stores/tablePlan'
import { useSessionStore } from '../../shared/stores/session'
import AreaList from '../components/tablePlan/AreaList.vue'
import AreaPlan from '../components/tablePlan/AreaPlan.vue'
import TableActionSheet from '../components/tablePlan/TableActionSheet.vue'
import UnsentOrderDialog from '../components/tablePlan/UnsentOrderDialog.vue'

const { t } = useI18n()
const tablePlan = useTablePlanStore()
const catalog = useCatalogStore()
const order = useOrderStore()
const openItems = useOpenItemsStore()
const session = useSessionStore()
const { landing } = useWaiterLanding()
const { draftTableName } = useDraftTableName()

const tappedTable = ref<TablePlanTableView | null>(null)
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

const whatIsRunningIsKnown = computed(
  () => tablePlan.hasLoaded && (tablePlan.areas.length > 0 || catalog.hasLoaded),
)

function goToTheLockedOrder(): void {
  if (landing.value.kind === 'lockedOrder') {
    navigate('/review')
  }
}

onMounted(goToTheLockedOrder)

watch(landing, goToTheLockedOrder)

function openTheOrderFor(table: TablePlanTableView): void {
  order.chooseTable({ tableId: table.tableId, name: table.name })
  navigate('/catalog')
}

function takeTheOrder(): void {
  const table = tappedTable.value
  tappedTable.value = null
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
      tableAskingAboutTheDraft.value = table
      return
    default:
      return assertNever(opening)
  }
}

function settleTheTable(): void {
  const table = tappedTable.value
  tappedTable.value = null
  if (table === null) {
    return
  }
  openItems.openTable(table.tableId)
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
      <div v-if="!tablePlan.hasLoaded && tablePlan.loadFailed" class="pa-4" data-test="table-plan-load-failed">
        <v-alert class="mb-4" type="warning" variant="tonal">
          {{ t('phone.tablePlan.errors.loadFailed') }}
        </v-alert>
        <v-btn
          class="reload-the-plan"
          data-test="reload-the-plan"
          variant="outlined"
          size="x-large"
          block
          @click="tablePlan.load()"
        >
          {{ t('phone.tablePlan.actions.reload') }}
        </v-btn>
      </div>
      <v-progress-linear v-else indeterminate data-test="table-plan-loading" />
    </template>
    <p v-else-if="landing.kind === 'noFestival'" class="no-festival pa-4 text-body-1" data-test="no-festival">
      {{ t('phone.tablePlan.messages.noFestivalIsActive') }}
    </p>
    <p v-else-if="landing.kind === 'noTables'" class="no-tables pa-4 text-body-1" data-test="no-tables">
      {{ t('phone.tablePlan.messages.noTables') }}
    </p>
    <AreaList
      v-else-if="landing.kind === 'areaList'"
      class="pa-4"
      :areas="tablePlan.areas"
      @open-area="tablePlan.openArea"
    />
    <template v-else-if="landing.kind === 'plan' && shownArea !== null">
      <p
        v-if="shownArea.tables.length === 0"
        class="no-tables-in-area pa-4 mb-0 text-body-1"
        data-test="no-tables-in-area"
      >
        {{ t('phone.tablePlan.messages.noTablesInArea') }}
      </p>
      <AreaPlan :area="shownArea" @open-table="tappedTable = $event" />
    </template>
    <TableActionSheet
      v-if="tappedTable !== null"
      :table-name="tappedTable.name"
      @take-order="takeTheOrder"
      @settle="settleTheTable"
      @back="tappedTable = null"
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
