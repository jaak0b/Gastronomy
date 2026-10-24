<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { navigate } from '../../../shared/router/router'
import { combineReleases } from '../../../shared/core/combineReleases'
import { isDrawnIn, type ShapeSelection } from '../../core/tablePlanSelection'
import { useOpenedFestival } from '../../composables/useOpenedFestival'
import { useAdminFestivalsStore } from '../../stores/festivals'
import { useAdminStationsStore } from '../../stores/stations'
import { useAdminTablePlanStore } from '../../stores/tablePlan'
import TablePlanDrawing from './TablePlanDrawing.vue'
import TablePlanStationList from './TablePlanStationList.vue'
import TablePlanTableList from './TablePlanTableList.vue'

const props = defineProps<{ festivalId: string; areaId: string }>()

const { t } = useI18n()
const festivals = useAdminFestivalsStore()
const stations = useAdminStationsStore()
const tablePlan = useAdminTablePlanStore()
const selectedShape = ref<ShapeSelection | null>(null)
let stopListening: (() => void) | null = null

const { festival, loadTheFestivalOrLeave } = useOpenedFestival(() => props.festivalId)
const area = computed(() => tablePlan.areas.find((candidate) => candidate.areaId === props.areaId) ?? null)
const isUnknownArea = computed(() => tablePlan.loadedFestivalId === props.festivalId && area.value === null)
const festivalHasStations = computed(() => stations.stations.some((station) => station.isAtAnyFestival))
const stationsOnNoPlan = computed(() => {
  const stationIdsOnAPlan = new Set(
    tablePlan.areas.flatMap((each) => each.stationShapes.map((shape) => shape.stationId)),
  )
  return stations.stations.filter(
    (station) => station.isAtAnyFestival && !stationIdsOnAPlan.has(station.stationId),
  )
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

watch([area, selectedShape], ([shownArea, shape]) => {
  if (shape !== null && (shownArea === null || !isDrawnIn(shownArea, shape))) {
    selectedShape.value = null
  }
})

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

      <div v-if="area !== null" class="editor-columns">
        <TablePlanDrawing
          v-model:selected-shape="selectedShape"
          class="drawing-column"
          :festival-id="festivalId"
          :area="area"
        />
        <div class="lists-column" data-test="lists-column">
          <TablePlanTableList
            v-model:selected-shape="selectedShape"
            :festival-id="festivalId"
            :area="area"
            :is-running="festival?.isRunning === true"
          />
          <TablePlanStationList
            v-model:selected-shape="selectedShape"
            class="mt-6"
            :festival-id="festivalId"
            :area="area"
            :stations-on-no-plan="stationsOnNoPlan"
            :festival-has-stations="festivalHasStations"
          />
        </div>
      </div>
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

.editor-columns {
  align-items: flex-start;
  display: flex;
  flex: 1 1 0;
  flex-wrap: wrap;
  gap: 1.5rem;
  min-height: 0;
}

.drawing-column {
  flex: 0 0 auto;
  height: 100%;
  min-height: 20rem;
}

.lists-column {
  flex: 1 1 22rem;
  max-height: 100%;
  max-width: 32rem;
  min-width: 0;
  overflow-y: auto;
}
</style>
