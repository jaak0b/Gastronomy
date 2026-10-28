<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminAreaView,
  AdminStationShapeView,
  AdminTableView,
  SavedTableView,
} from '../../../shared/api/generatedSchemas'
import { navigate } from '../../../shared/router/router'
import { assertNever } from '../../../shared/core/assertNever'
import { combineReleases } from '../../../shared/core/combineReleases'
import type { AdminActionResult } from '../../core/adminActionResult'
import { NEW_STATION_SIZE, NEW_TABLE_SIZE, geometryForANewShapeIn } from '../../core/tablePlanPlacement'
import {
  isDrawnIn,
  selectionOfTables,
  soleSelectedShapeOf,
  type TablePlanSelection,
} from '../../core/tablePlanSelection'
import { useEditSession, type EditSessionMode } from '../../composables/useEditSession'
import { useOpenedFestival } from '../../composables/useOpenedFestival'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useAdminFestivalsStore } from '../../stores/festivals'
import { useAdminStationsStore } from '../../stores/stations'
import { useAdminTablePlanStore } from '../../stores/tablePlan'
import BaseHideConfirmDialog from '../BaseHideConfirmDialog.vue'
import BaseRemoveConfirmDialog from '../BaseRemoveConfirmDialog.vue'
import TablePlanDrawing from './TablePlanDrawing.vue'
import TablePlanNameDialog from './TablePlanNameDialog.vue'
import TablePlanToolbar from './TablePlanToolbar.vue'

interface TableDraft {
  name: string
  area: AdminAreaView
}

const props = defineProps<{ festivalId: string; areaId: string }>()

const { t } = useI18n()
const festivals = useAdminFestivalsStore()
const stations = useAdminStationsStore()
const tablePlan = useAdminTablePlanStore()
const selection = ref<TablePlanSelection | null>(null)
const tableAwaitingHiding = shallowRef<AdminTableView | null>(null)
const stationAwaitingRemoval = shallowRef<AdminStationShapeView | null>(null)
const { refusal: planRefusal, refusalText: planRefusalText, showRefusalOf: showThePlanRefusalOf } =
  useRefusalDisplay()
const tableSession = useEditSession<AdminTableView, TableDraft, SavedTableView | null>({
  saveThrough: saveTheTable,
  afterSaving: selectTheNewTable,
})
let stopListening: (() => void) | null = null

const { loadTheFestivalOrLeave } = useOpenedFestival(() => props.festivalId)
const area = computed(() => tablePlan.areas.find((candidate) => candidate.areaId === props.areaId) ?? null)
const isUnknownArea = computed(() => tablePlan.loadedFestivalId === props.festivalId && area.value === null)
const festivalHasStations = computed(() => stations.stations.some((station) => station.isAtFestival))
const stationsOnNoPlan = computed(() => {
  const stationIdsOnAPlan = new Set(
    tablePlan.areas.flatMap((each) => each.stationShapes.map((shape) => shape.stationId)),
  )
  return stations.stations.filter(
    (station) => station.isAtFestival && !stationIdsOnAPlan.has(station.stationId),
  )
})
const soleSelectedShape = computed(() =>
  area.value === null ? null : soleSelectedShapeOf(area.value, selection.value),
)
const removalRefusal = computed(() => {
  const selected = soleSelectedShape.value
  if (selected === null) {
    return null
  }
  switch (selected.kind) {
    case 'table':
      return selected.table.hasItemsToHandOutOrSettleAtTheRunningFestival
        ? t('errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown')
        : null
    case 'station':
      return null
    default:
      return assertNever(selected)
  }
})

watch(
  isUnknownArea,
  (isUnknown) => {
    if (isUnknown) {
      navigate(`/admin/festivals/${props.festivalId}`)
    }
  },
  { immediate: true },
)

watch(selection, () => {
  planRefusal.value = null
})

watch([area, selection], ([shownArea, selected]) => {
  if (selected !== null && (shownArea === null || !isDrawnIn(shownArea, selected))) {
    selection.value = null
  }
})

async function saveTheTable(
  draft: TableDraft,
  table: AdminTableView | null,
): Promise<AdminActionResult<SavedTableView | null>> {
  return table === null
    ? await tablePlan.createTable(props.festivalId, {
        areaId: draft.area.areaId,
        name: draft.name,
        geometry: geometryForANewShapeIn(draft.area, NEW_TABLE_SIZE),
      })
    : await tablePlan.saveTable(props.festivalId, table.tableId, {
        areaId: draft.area.areaId,
        name: draft.name,
        geometry: table.geometry,
      })
}

async function saveTheTableName(name: string): Promise<void> {
  const shownArea = area.value
  if (shownArea !== null) {
    await tableSession.save({ name, area: shownArea })
  }
}

function selectTheNewTable(saved: SavedTableView | null, mode: EditSessionMode): void {
  switch (mode) {
    case 'create':
      if (saved !== null) {
        selection.value = selectionOfTables([saved.tableId])
      }
      return
    case 'edit':
      return
    default:
      assertNever(mode)
  }
}

function editTheSelectedTable(): void {
  const selected = soleSelectedShape.value
  if (selected?.kind === 'table') {
    tableSession.openForEdit(selected.table)
  }
}

function askToRemoveTheSelection(): void {
  const selected = soleSelectedShape.value
  if (selected === null || removalRefusal.value !== null) {
    return
  }
  switch (selected.kind) {
    case 'table':
      tableAwaitingHiding.value = selected.table
      return
    case 'station':
      stationAwaitingRemoval.value = selected.stationShape
      return
    default:
      assertNever(selected)
  }
}

async function sendAndShowAnyRefusal(action: () => Promise<AdminActionResult<unknown>>): Promise<boolean> {
  planRefusal.value = null
  return !showThePlanRefusalOf(await action())
}

async function confirmHiding(): Promise<void> {
  const table = tableAwaitingHiding.value
  tableAwaitingHiding.value = null
  if (table !== null) {
    await sendAndShowAnyRefusal(() => tablePlan.hideTable(props.festivalId, table.tableId))
  }
}

async function confirmRemoval(): Promise<void> {
  const stationShape = stationAwaitingRemoval.value
  stationAwaitingRemoval.value = null
  if (stationShape !== null) {
    await sendAndShowAnyRefusal(() => tablePlan.takeStationOffThePlan(props.festivalId, stationShape.stationId))
  }
}

async function showAgain(table: AdminTableView): Promise<void> {
  await sendAndShowAnyRefusal(() => tablePlan.showTable(props.festivalId, table.tableId))
}

async function placeTheStation(stationId: string): Promise<void> {
  const shownArea = area.value
  if (shownArea === null) {
    return
  }
  const placed = await sendAndShowAnyRefusal(() =>
    tablePlan.placeStation(props.festivalId, stationId, {
      areaId: shownArea.areaId,
      geometry: geometryForANewShapeIn(shownArea, NEW_STATION_SIZE),
    }),
  )
  if (placed) {
    selection.value = { kind: 'station', stationId }
  }
}

async function reload(): Promise<void> {
  if ((await loadTheFestivalOrLeave()) === null) {
    return
  }
  await tablePlan.loadForFestival(props.festivalId)
  await stations.loadAtTheFestival(props.festivalId)
}

onMounted(async () => {
  stopListening = combineReleases(festivals.listen(), stations.listen(), tablePlan.listen())
  await reload()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <div class="table-plan-editor" data-test="table-plan-editor">
    <div class="editor-block" data-test="editor-block">
      <div class="editor-heading d-flex align-center flex-wrap ga-3 py-2">
        <v-btn
          class="back-to-festival"
          data-test="back-to-festival"
          variant="text"
          prepend-icon="mdi-arrow-left"
          @click="navigate(`/admin/festivals/${festivalId}`)"
        >
          {{ t('common.actions.back') }}
        </v-btn>
        <h1 v-if="area !== null" class="area-name text-h5" data-test="area-name">{{ area.name }}</h1>
      </div>
      <v-alert
        v-if="festivals.loadFailed || tablePlan.loadFailed || stations.loadFailed"
        class="error mb-2"
        data-test="load-failed"
        type="error"
        variant="tonal"
      >
        {{ t('admin.common.errors.loadFailed') }}
      </v-alert>

      <template v-if="area !== null">
        <v-alert
          v-if="planRefusalText !== null"
          class="plan-refusal mb-2"
          data-test="plan-refusal"
          type="warning"
          variant="tonal"
        >
          {{ planRefusalText }}
        </v-alert>
        <div class="plan-frame" data-test="plan-frame">
          <TablePlanToolbar
            :area="area"
            :stations-on-no-plan="stationsOnNoPlan"
            :festival-has-stations="festivalHasStations"
            @new-table="tableSession.openForCreate()"
            @place-station="placeTheStation"
            @show-table="showAgain"
          />
          <TablePlanDrawing
            v-model:selection="selection"
            class="drawing"
            :festival-id="festivalId"
            :area="area"
            :sole-selected-shape="soleSelectedShape"
            :removal-refusal="removalRefusal"
            @edit="editTheSelectedTable"
            @remove="askToRemoveTheSelection"
            @action-started="planRefusal = null"
            @action-finished="showThePlanRefusalOf"
          />
        </div>

        <TablePlanNameDialog
          v-if="tableSession.isOpen.value"
          :title="tableSession.edited.value === null ? t('admin.tablePlan.actions.newTable') : t('admin.tablePlan.actions.editTable')"
          :name-label="t('admin.tablePlan.labels.tableName')"
          :saved-name="tableSession.edited.value?.name ?? ''"
          :error-text="tableSession.refusalText.value"
          @save="saveTheTableName"
          @cancel="tableSession.close()"
        />
      </template>

      <BaseHideConfirmDialog
        v-if="tableAwaitingHiding !== null"
        :title="t('admin.tablePlan.labels.hideTableTitle')"
        :body="t('admin.tablePlan.messages.hideTableBody')"
        @confirm="confirmHiding"
        @cancel="tableAwaitingHiding = null"
      />
      <BaseRemoveConfirmDialog
        v-if="stationAwaitingRemoval !== null"
        :title="t('admin.festivals.labels.removeStationTitle')"
        :body="t('admin.tablePlan.messages.removeStationBody')"
        @confirm="confirmRemoval"
        @cancel="stationAwaitingRemoval = null"
      />
    </div>
  </div>
</template>

<style scoped>
.table-plan-editor {
  flex: 1 1 0;
  min-height: 0;
  overflow-y: auto;
  padding: 0 1rem 1rem;
}

.editor-block {
  display: flex;
  flex-direction: column;
  height: 100%;
  margin: 0 auto;
  max-width: 1100px;
}

.plan-frame {
  border: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
  border-radius: 4px;
  display: flex;
  flex: 1 1 0;
  flex-direction: column;
  min-height: 20rem;
  overflow: hidden;
}

.drawing {
  flex: 1 1 0;
  min-height: 0;
}
</style>
