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
import TablePlanAdding from './TablePlanAdding.vue'
import TablePlanAreas from './TablePlanAreas.vue'
import TablePlanDrawing from './TablePlanDrawing.vue'
import TablePlanShapeDetails from './TablePlanShapeDetails.vue'

const props = defineProps<{ festivalId: string }>()

const { t } = useI18n()
const festivals = useAdminFestivalsStore()
const stations = useAdminStationsStore()
const tablePlan = useAdminTablePlanStore()
const selectedAreaId = ref<string | null>(null)
const selectedShape = ref<ShapeSelection | null>(null)
let stopListening: (() => void) | null = null

const { festival, loadTheFestivalOrLeave } = useOpenedFestival(() => props.festivalId)
const selectedArea = computed(
  () => tablePlan.areas.find((area) => area.areaId === selectedAreaId.value) ?? null,
)
const stationsToPlace = computed(() => {
  const placedStationIds = new Set(
    tablePlan.areas.flatMap((area) => area.stationShapes.map((shape) => shape.stationId)),
  )
  return stations.stations.filter(
    (station) => station.isAtAnyFestival && !placedStationIds.has(station.stationId),
  )
})

watch(
  () => tablePlan.areas,
  (areas) => {
    if (!areas.some((area) => area.areaId === selectedAreaId.value)) {
      selectedAreaId.value = areas[0]?.areaId ?? null
    }
  },
  { immediate: true },
)

watch([selectedArea, selectedShape], ([area, shape]) => {
  if (shape !== null && (area === null || !isDrawnIn(area, shape))) {
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
    <div class="editor-heading d-flex align-center flex-wrap ga-3 px-4 py-2">
      <v-btn
        class="back-to-festival"
        data-test="back-to-festival"
        variant="text"
        prepend-icon="mdi-arrow-left"
        @click="navigate(`/admin/festivals/${festivalId}`)"
      >
        {{ t('common.actions.back') }}
      </v-btn>
      <h1 class="text-h5">{{ t('admin.tablePlan.title') }}</h1>
      <span v-if="festival !== null" class="festival-name text-h6 text-medium-emphasis" data-test="festival-name">
        {{ festival.name }}
      </span>
    </div>
    <v-alert
      v-if="festivals.loadFailed || tablePlan.loadFailed || stations.loadFailed"
      class="error mx-4 mb-2"
      type="error"
      variant="tonal"
    >
      {{ t('admin.common.errors.loadFailed') }}
    </v-alert>

    <div v-if="festival !== null" class="editor-columns">
      <TablePlanAreas
        v-model:selected-area-id="selectedAreaId"
        class="areas-column"
        :festival-id="festivalId"
        :areas="tablePlan.areas"
      />
      <TablePlanDrawing
        v-if="selectedArea !== null"
        v-model:selected-shape="selectedShape"
        class="drawing-column"
        :festival-id="festivalId"
        :area="selectedArea"
      />
      <p v-else class="drawing-column no-area text-body-1 text-medium-emphasis" data-test="no-area">
        {{ t('admin.tablePlan.messages.createAnAreaFirst') }}
      </p>
      <div v-if="selectedArea !== null" class="selection-column">
        <TablePlanShapeDetails
          :festival-id="festivalId"
          :areas="tablePlan.areas"
          :selection="selectedShape"
        />
        <TablePlanAdding
          v-model:selected-shape="selectedShape"
          :festival-id="festivalId"
          :area="selectedArea"
          :stations-to-place="stationsToPlace"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.table-plan-editor {
  display: flex;
  flex: 1 1 0;
  flex-direction: column;
  min-height: 0;
}

.editor-columns {
  display: grid;
  flex: 1 1 0;
  gap: 1rem;
  grid-template-columns: 18rem minmax(0, 1fr) 20rem;
  grid-template-rows: minmax(0, 1fr);
  min-height: 0;
  padding: 0 1rem 1rem;
}

.areas-column,
.selection-column {
  min-height: 0;
  overflow-y: auto;
}

.drawing-column {
  min-height: 0;
}
</style>
