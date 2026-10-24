<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminAreaView,
  AdminStationShapeView,
  AdminTableView,
} from '../../../shared/api/generatedSchemas'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import { assertNever } from '../../../shared/core/assertNever'
import { shapeNodeIdOf, type ShapeSelection } from '../../core/tablePlanSelection'
import { useRowRefusals } from '../../composables/useRowRefusals'
import { useTypedNames } from '../../composables/useTypedNames'
import { useAdminTablePlanStore } from '../../stores/tablePlan'

const TABLE_NAME_MISSING_KEY = 'errors.admin.festivals.tableNameMissing'

interface TableInTheArea {
  table: AdminTableView
  areaId: string
  nodeId: string
}

interface StationShapeInTheArea {
  stationShape: AdminStationShapeView
  nodeId: string
}

const props = defineProps<{
  festivalId: string
  areas: AdminAreaView[]
  selection: ShapeSelection | null
}>()

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()
const shapeRefusals = useRowRefusals()
const typedNames = useTypedNames()
const tableAwaitingConfirmation = ref<TableInTheArea | null>(null)

const selectedNodeId = computed(() => (props.selection === null ? null : shapeNodeIdOf(props.selection)))

const tablesWithDetails = computed<TableInTheArea[]>(() =>
  props.areas
    .flatMap((area) =>
      area.tables.map((table) => ({
        table,
        areaId: area.areaId,
        nodeId: shapeNodeIdOf({ kind: 'table', tableId: table.tableId }),
      })),
    )
    .filter(
      (entry) =>
        entry.nodeId === selectedNodeId.value
        || shapeRefusals.isRefused(entry.nodeId)
        || typedNames.hasTypedName(entry.table.tableId),
    )
    .sort(
      (left, right) =>
        Number(right.nodeId === selectedNodeId.value) - Number(left.nodeId === selectedNodeId.value),
    ),
)

const stationShapesWithDetails = computed<StationShapeInTheArea[]>(() =>
  props.areas
    .flatMap((area) =>
      area.stationShapes.map((stationShape) => ({
        stationShape,
        nodeId: shapeNodeIdOf({ kind: 'station', stationId: stationShape.stationId }),
      })),
    )
    .filter((entry) => entry.nodeId === selectedNodeId.value || shapeRefusals.isRefused(entry.nodeId)),
)

function typeTheName(entry: TableInTheArea, typed: string): void {
  shapeRefusals.forgetRefusalOf(entry.nodeId)
  typedNames.type(entry.table.tableId, entry.table.name, typed)
}

async function rename(entry: TableInTheArea): Promise<void> {
  const name = typedNames.takeNameToSend(entry.table.tableId, entry.table.name)
  if (name === null) {
    return
  }
  shapeRefusals.forgetRefusalOf(entry.nodeId)
  if (name.trim().length === 0) {
    shapeRefusals.refuse(entry.nodeId, TABLE_NAME_MISSING_KEY)
    return
  }
  const saved = await tablePlan.saveTable(props.festivalId, entry.table.tableId, {
    areaId: entry.areaId,
    name,
    geometry: entry.table.geometry,
  })
  switch (saved.kind) {
    case 'ok':
      typedNames.forgetTypedName(entry.table.tableId)
      return
    case 'failed':
      shapeRefusals.showRefusalOf(entry.nodeId, saved)
      return
    default:
      assertNever(saved)
  }
}

async function hide(entry: TableInTheArea): Promise<void> {
  shapeRefusals.forgetRefusalOf(entry.nodeId)
  shapeRefusals.showRefusalOf(entry.nodeId, await tablePlan.hideTable(props.festivalId, entry.table.tableId))
}

async function show(entry: TableInTheArea): Promise<void> {
  shapeRefusals.forgetRefusalOf(entry.nodeId)
  shapeRefusals.showRefusalOf(entry.nodeId, await tablePlan.showTable(props.festivalId, entry.table.tableId))
}

async function remove(): Promise<void> {
  const entry = tableAwaitingConfirmation.value
  tableAwaitingConfirmation.value = null
  if (entry === null) {
    return
  }
  shapeRefusals.forgetRefusalOf(entry.nodeId)
  shapeRefusals.showRefusalOf(entry.nodeId, await tablePlan.deleteTable(props.festivalId, entry.table.tableId))
}

async function takeOffThePlan(entry: StationShapeInTheArea): Promise<void> {
  shapeRefusals.forgetRefusalOf(entry.nodeId)
  shapeRefusals.showRefusalOf(
    entry.nodeId,
    await tablePlan.takeStationOffThePlan(props.festivalId, entry.stationShape.stationId),
  )
}
</script>

<template>
  <div class="table-plan-shape-details">
    <section
      v-for="entry in tablesWithDetails"
      :key="entry.nodeId"
      class="shape-details mb-6"
      data-test="shape-details"
      :data-test-id="entry.nodeId"
    >
      <div class="d-flex align-center ga-2 mb-2">
        <h2 class="section-heading text-h6" data-test="table-heading">
          {{ t('common.labels.table', { name: entry.table.name }) }}
        </h2>
        <v-chip v-if="entry.table.isHidden" class="hidden" data-test="hidden" size="small" color="grey">
          {{ t('admin.festivals.labels.hidden') }}
        </v-chip>
      </div>
      <v-text-field
        class="table-name-field"
        data-test="table-name-field"
        density="compact"
        hide-details
        :model-value="typedNames.nameShownFor(entry.table.tableId, entry.table.name)"
        :label="t('admin.tablePlan.labels.tableName')"
        @update:model-value="typeTheName(entry, $event)"
        @blur="rename(entry)"
        @keyup.enter="rename(entry)"
      />
      <div class="d-flex align-center ga-2 mt-2">
        <v-btn
          v-if="!entry.table.isHidden"
          class="hide-table"
          data-test="hide-table"
          variant="text"
          @click="hide(entry)"
        >
          {{ t('admin.festivals.actions.hide') }}
        </v-btn>
        <v-btn
          v-else
          class="show-table"
          data-test="show-table"
          variant="text"
          @click="show(entry)"
        >
          {{ t('admin.festivals.actions.show') }}
        </v-btn>
        <v-spacer />
        <v-btn
          class="delete-table"
          data-test="delete-table"
          icon="mdi-delete"
          variant="text"
          color="error"
          @click="tableAwaitingConfirmation = entry"
        />
      </div>
      <v-alert
        v-if="shapeRefusals.refusalTextOf(entry.nodeId) !== null"
        class="refusal mt-2"
        data-test="refusal"
        type="warning"
        variant="tonal"
      >
        {{ shapeRefusals.refusalTextOf(entry.nodeId) }}
      </v-alert>
    </section>

    <section
      v-for="entry in stationShapesWithDetails"
      :key="entry.nodeId"
      class="shape-details mb-6"
      data-test="shape-details"
      :data-test-id="entry.nodeId"
    >
      <h2 class="section-heading text-h6 mb-1">{{ t('admin.tablePlan.labels.station') }}</h2>
      <p class="station-name text-body-1 mb-2" data-test="station-name">{{ entry.stationShape.stationName }}</p>
      <v-btn
        class="take-station-off-the-plan"
        data-test="take-station-off-the-plan"
        variant="text"
        @click="takeOffThePlan(entry)"
      >
        {{ t('admin.tablePlan.actions.takeStationOffThePlan') }}
      </v-btn>
      <v-alert
        v-if="shapeRefusals.refusalTextOf(entry.nodeId) !== null"
        class="refusal mt-2"
        data-test="refusal"
        type="warning"
        variant="tonal"
      >
        {{ shapeRefusals.refusalTextOf(entry.nodeId) }}
      </v-alert>
    </section>

    <BaseConfirmDialog
      v-if="tableAwaitingConfirmation !== null"
      :title="t('admin.tablePlan.labels.deleteTableTitle', { name: tableAwaitingConfirmation.table.name })"
      :confirm-label="t('admin.tablePlan.actions.delete')"
      @confirm="remove"
      @cancel="tableAwaitingConfirmation = null"
    />
  </div>
</template>
