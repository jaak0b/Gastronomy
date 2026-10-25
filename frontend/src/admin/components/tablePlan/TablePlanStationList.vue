<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminAreaView,
  AdminStationShapeView,
  AdminStationView,
} from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import type { AdminActionResult } from '../../core/adminActionResult'
import { NEW_STATION_SIZE, geometryForANewShapeIn } from '../../core/tablePlanPlacement'
import type { TablePlanSelection } from '../../core/tablePlanSelection'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useAdminTablePlanStore } from '../../stores/tablePlan'
import BaseSearchAddLine from '../BaseSearchAddLine.vue'
import RemovableStationRows from '../stations/RemovableStationRows.vue'

const props = defineProps<{
  festivalId: string
  area: AdminAreaView
  stationsOnNoPlan: AdminStationView[]
  festivalHasStations: boolean
}>()
const selection = defineModel<TablePlanSelection | null>('selection', { required: true })

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()
const { refusal: placingRefusal, refusalText: placingRefusalText, showRefusalOf: showThePlacingRefusalOf } =
  useRefusalDisplay()
const chosenStationId = ref<string | null>(null)

const noStationsToPlaceText = computed(() =>
  props.festivalHasStations
    ? t('admin.tablePlan.messages.noStationsToPlace')
    : t('admin.tablePlan.messages.festivalHasNoStations'),
)

function isSelected(stationShape: AdminStationShapeView): boolean {
  return selection.value?.kind === 'station' && selection.value.stationId === stationShape.stationId
}

async function takeOffThePlan(stationShape: AdminStationShapeView): Promise<AdminActionResult<null>> {
  return await tablePlan.takeStationOffThePlan(props.festivalId, stationShape.stationId)
}

async function place(): Promise<void> {
  const stationId = chosenStationId.value
  if (stationId === null) {
    return
  }
  placingRefusal.value = null
  const placed = await tablePlan.placeStation(props.festivalId, stationId, {
    areaId: props.area.areaId,
    geometry: geometryForANewShapeIn(props.area, NEW_STATION_SIZE),
  })
  switch (placed.kind) {
    case 'ok':
      chosenStationId.value = null
      selection.value = { kind: 'station', stationId }
      return
    case 'failed':
      showThePlacingRefusalOf(placed)
      return
    default:
      assertNever(placed)
  }
}
</script>

<template>
  <section class="table-plan-station-list" data-test="table-plan-station-list">
    <h2 class="section-heading text-h6 mb-2">{{ t('admin.stations.title') }}</h2>

    <RemovableStationRows
      v-if="area.stationShapes.length > 0"
      class="mb-3"
      :stations="area.stationShapes"
      :id-of="(stationShape) => stationShape.stationId"
      :name-of="(stationShape) => stationShape.stationName"
      :removal-refusal-of="() => null"
      :is-selected="isSelected"
      row-test-name="station-row"
      :remove-title="t('admin.festivals.labels.removeStationTitle')"
      :remove-body="t('admin.tablePlan.messages.removeStationBody')"
      :remove-through="takeOffThePlan"
    />

    <div class="add-station-to-plan" data-test="add-station-to-plan">
      <BaseSearchAddLine
        v-model:chosen-id="chosenStationId"
        :entries="stationsOnNoPlan"
        entry-title="name"
        entry-value="stationId"
        :label="t('admin.festivals.labels.stationName')"
        :no-entries-text="noStationsToPlaceText"
        :add-label="t('admin.festivals.actions.addStation')"
        search-test-name="station-search"
        add-test-name="add-station"
        @add="place"
      />
      <v-alert
        v-if="placingRefusalText !== null"
        class="refusal mt-3"
        data-test="refusal"
        type="warning"
        variant="tonal"
      >
        {{ placingRefusalText }}
      </v-alert>
    </div>
  </section>
</template>
