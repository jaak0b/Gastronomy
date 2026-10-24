<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { AdminStationView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import type { AdminActionResult } from '../../core/adminActionResult'
import { useAdminStationsStore, type StationDraft } from '../../stores/stations'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useStationRefusalReasons } from '../../composables/useStationRefusalReasons'
import BaseSearchAddLine from '../BaseSearchAddLine.vue'
import RemovableStationRows from '../stations/RemovableStationRows.vue'
import StationDialog from '../stations/StationDialog.vue'

const props = defineProps<{ festivalId: string }>()

const { t } = useI18n()
const stations = useAdminStationsStore()
const chosenStationId = ref<string | null>(null)
const isCreating = ref(false)
const editingStation = ref<AdminStationView | null>(null)
const { refusal, refusalText } = useRefusalDisplay()
const { refusal: stationRefusal, refusalText: stationRefusalText } = useRefusalDisplay()
const { removalRefusalOf } = useStationRefusalReasons()

const atTheFestival = computed(() =>
  stations.stations.filter((station) => station.isAtAnyFestival),
)
const stillToAdd = computed(() =>
  stations.stations.filter((station) => station.isActive && !station.isAtAnyFestival),
)
const noStationsToAddText = computed(() =>
  stations.stations.some((station) => station.isActive)
    ? t('admin.festivals.messages.noStationsToAdd')
    : t('admin.festivals.messages.noStationSwitchedOn'),
)

async function removeFromTheFestival(station: AdminStationView): Promise<AdminActionResult<null>> {
  refusal.value = null
  return await stations.removeFromTheFestival(props.festivalId, station.stationId)
}

async function add(): Promise<void> {
  const stationId = chosenStationId.value
  if (stationId === null) {
    return
  }
  refusal.value = null
  const added = await stations.addToTheFestival(props.festivalId, stationId)
  switch (added.kind) {
    case 'ok':
      chosenStationId.value = null
      return
    case 'failed':
      refusal.value = added.message
      return
    default:
      assertNever(added)
  }
}

function startCreating(): void {
  refusal.value = null
  isCreating.value = true
}

function stopCreating(): void {
  refusal.value = null
  isCreating.value = false
}

async function create(draft: StationDraft): Promise<void> {
  refusal.value = null
  const created = await stations.create(draft)
  switch (created.kind) {
    case 'ok':
      isCreating.value = false
      chosenStationId.value = created.value.stationId
      return
    case 'failed':
      refusal.value = created.message
      return
    default:
      assertNever(created)
  }
}

function startEditing(station: AdminStationView): void {
  stationRefusal.value = null
  editingStation.value = station
}

function stopEditing(): void {
  stationRefusal.value = null
  editingStation.value = null
}

async function saveStation(station: StationDraft): Promise<void> {
  stationRefusal.value = null
  const saved = await stations.save(station)
  switch (saved.kind) {
    case 'ok':
      stopEditing()
      return
    case 'failed':
      stationRefusal.value = saved.message
      return
    default:
      assertNever(saved)
  }
}
</script>

<template>
  <section class="festival-stations mb-4" data-test="festival-stations">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3" data-test="section-heading">{{ t('admin.stations.title') }}</h2>

        <div class="rows mb-3">
          <RemovableStationRows
            :stations="atTheFestival"
            :id-of="(station) => station.stationId"
            :name-of="(station) => station.name"
            :removal-refusal-of="removalRefusalOf"
            row-test-name="festival-station-row"
            :remove-title="t('admin.festivals.labels.removeStationTitle')"
            :remove-body="t('admin.festivals.messages.removeStationBody')"
            :remove-through="removeFromTheFestival"
          >
            <template #badges="{ station }">
              <v-chip v-if="!station.isActive" class="deactivated" data-test="deactivated" size="small" color="grey">
                {{ t('admin.common.labels.deactivated') }}
              </v-chip>
            </template>
            <template #actions="{ station }">
              <v-btn class="edit-station" data-test="edit-station" variant="text" @click="startEditing(station)">
                {{ t('admin.common.actions.edit') }}
              </v-btn>
            </template>
          </RemovableStationRows>

          <div v-if="atTheFestival.length === 0" class="festival-station-placeholder" data-test="festival-station-placeholder">
            <div class="row-line d-flex align-center flex-wrap ga-3 py-2 px-3">
              <span class="text-body-1">&nbsp;</span>
            </div>
          </div>
        </div>

        <BaseSearchAddLine
          v-model:chosen-id="chosenStationId"
          class="mt-6"
          :entries="stillToAdd"
          entry-title="name"
          entry-value="stationId"
          :label="t('admin.festivals.labels.stationName')"
          :no-entries-text="noStationsToAddText"
          :add-label="t('admin.festivals.actions.addStation')"
          search-test-name="station-search"
          add-test-name="add-station"
          @add="add"
        >
          <v-btn class="new-station" data-test="new-station" variant="text" @click="startCreating">
            {{ t('admin.stations.actions.new') }}
          </v-btn>
        </BaseSearchAddLine>
        <v-alert
          v-if="refusalText !== null && !isCreating"
          class="refusal mt-3"
          data-test="refusal"
          type="warning"
          variant="tonal"
        >
          {{ refusalText }}
        </v-alert>
      </div>
    </v-card>

    <StationDialog
      v-if="editingStation !== null"
      :station="editingStation"
      :error-text="stationRefusalText"
      @save="saveStation"
      @cancel="stopEditing"
    />

    <StationDialog
      v-if="isCreating"
      :station="null"
      :error-text="refusalText"
      @save="create"
      @cancel="stopCreating"
    />
  </section>
</template>
