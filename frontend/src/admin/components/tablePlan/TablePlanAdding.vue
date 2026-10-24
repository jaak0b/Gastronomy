<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminAreaView, AdminStationView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import {
  NEW_STATION_SIZE,
  NEW_TABLE_SIZE,
  geometryBelowTheLowestShape,
} from '../../core/tablePlanPlacement'
import type { ShapeSelection } from '../../core/tablePlanSelection'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useRowRefusals } from '../../composables/useRowRefusals'
import { useAdminTablePlanStore } from '../../stores/tablePlan'

const props = defineProps<{
  festivalId: string
  area: AdminAreaView
  stationsToPlace: AdminStationView[]
}>()
const selectedShape = defineModel<ShapeSelection | null>('selectedShape', { required: true })

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()
const { refusal: tableRefusal, refusalText: tableRefusalText } = useRefusalDisplay()
const stationRefusals = useRowRefusals()
const newTableName = ref('')

const shapesInTheArea = computed(() => [
  ...props.area.tables.map((table) => table.geometry),
  ...props.area.stationShapes.map((shape) => shape.geometry),
])

async function addTable(): Promise<void> {
  if (newTableName.value.trim().length === 0) {
    return
  }
  tableRefusal.value = null
  const created = await tablePlan.createTable(props.festivalId, {
    areaId: props.area.areaId,
    name: newTableName.value,
    geometry: geometryBelowTheLowestShape(shapesInTheArea.value, NEW_TABLE_SIZE),
  })
  switch (created.kind) {
    case 'ok':
      newTableName.value = ''
      selectedShape.value = { kind: 'table', tableId: created.value.tableId }
      return
    case 'failed':
      tableRefusal.value = created.message
      return
    default:
      assertNever(created)
  }
}

async function placeStation(stationId: string): Promise<void> {
  stationRefusals.forgetRefusalOf(stationId)
  const placed = await tablePlan.placeStation(props.festivalId, stationId, {
    areaId: props.area.areaId,
    geometry: geometryBelowTheLowestShape(shapesInTheArea.value, NEW_STATION_SIZE),
  })
  switch (placed.kind) {
    case 'ok':
      selectedShape.value = { kind: 'station', stationId }
      return
    case 'failed':
      stationRefusals.showRefusalOf(stationId, placed)
      return
    default:
      assertNever(placed)
  }
}
</script>

<template>
  <section class="table-plan-adding" data-test="table-plan-adding">
    <v-text-field
      v-model="newTableName"
      class="new-table-name"
      data-test="new-table-name"
      density="compact"
      hide-details
      :label="t('admin.tablePlan.labels.newTableName')"
      @update:model-value="tableRefusal = null"
      @keyup.enter="addTable"
    />
    <v-btn
      class="add-table mt-2"
      data-test="add-table"
      color="primary"
      variant="tonal"
      block
      :disabled="newTableName.trim().length === 0"
      @click="addTable"
    >
      {{ t('admin.tablePlan.actions.addTable') }}
    </v-btn>
    <v-alert v-if="tableRefusalText !== null" class="refusal mt-2" data-test="refusal" type="warning" variant="tonal">
      {{ tableRefusalText }}
    </v-alert>

    <template v-if="stationsToPlace.length > 0">
      <h2 class="section-heading text-h6 mt-6 mb-2">{{ t('admin.tablePlan.labels.stationsToPlace') }}</h2>
      <div
        v-for="station in stationsToPlace"
        :key="station.stationId"
        class="station-to-place mb-2"
        data-test="station-to-place"
        :data-test-id="station.stationId"
      >
        <div class="d-flex align-center flex-wrap ga-2">
          <span class="station-name text-body-1" data-test="station-name">{{ station.name }}</span>
          <v-spacer />
          <v-btn
            class="place-station"
            data-test="place-station"
            variant="text"
            size="small"
            @click="placeStation(station.stationId)"
          >
            {{ t('admin.tablePlan.actions.placeStation') }}
          </v-btn>
        </div>
        <v-alert
          v-if="stationRefusals.refusalTextOf(station.stationId) !== null"
          class="refusal mt-1"
          data-test="refusal"
          type="warning"
          variant="tonal"
        >
          {{ stationRefusals.refusalTextOf(station.stationId) }}
        </v-alert>
      </div>
    </template>
  </section>
</template>
