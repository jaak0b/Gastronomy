<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminErrorMessage } from '../../core/adminErrorMessage'
import type { AdminStation } from '../../../shared/api/apiTypes'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminStationsStore, type StationDraft } from '../../stores/stations'
import BaseConfirmDialog from '../BaseConfirmDialog.vue'
import { useRefusalText } from '../../composables/useRefusalText'
import StationDialog from '../stations/StationDialog.vue'

const props = defineProps<{ festivalId: string }>()

const { t } = useI18n()
const stations = useAdminStationsStore()
const chosenStationId = ref<string | null>(null)
const isCreating = ref(false)
const removedStation = ref<AdminStation | null>(null)
const refusedStationId = ref<string | null>(null)
const refusal = ref<AdminErrorMessage | null>(null)

const refusalText = useRefusalText(refusal)

const atTheFestival = computed(() =>
  stations.stations.filter((station) => station.isAtTheFestival),
)
const stillToAdd = computed(() =>
  stations.stations.filter((station) => station.isActive && !station.isAtTheFestival),
)

async function add(): Promise<void> {
  const stationId = chosenStationId.value
  if (stationId === null) {
    return
  }
  refusedStationId.value = null
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
  refusedStationId.value = null
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
      chosenStationId.value = created.value
      return
    case 'failed':
      refusal.value = created.message
      return
    default:
      assertNever(created)
  }
}

async function remove(): Promise<void> {
  const station = removedStation.value
  removedStation.value = null
  if (station === null) {
    return
  }
  refusedStationId.value = station.stationId
  refusal.value = null
  const removed = await stations.removeFromTheFestival(props.festivalId, station.stationId)
  switch (removed.kind) {
    case 'ok':
      return
    case 'failed':
      refusal.value = removed.message
      return
    default:
      assertNever(removed)
  }
}
</script>

<template>
  <section class="festival-stations mb-4">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3">{{ t('admin.stations.title') }}</h2>

        <div class="rows mb-3">
          <div
            v-for="(station, position) in atTheFestival"
            :key="station.stationId"
            class="festival-station-row"
            :class="{ 'tinted-row': position % 2 === 1 }"
          >
            <div class="row-line d-flex align-center flex-wrap ga-3 py-2 px-3">
              <span class="name text-body-1">{{ station.name }}</span>
              <v-chip v-if="!station.isActive" class="deactivated" size="small" color="grey">
                {{ t('admin.deactivated') }}
              </v-chip>
              <v-spacer />
              <v-btn class="remove-station" variant="text" @click="removedStation = station">
                {{ t('admin.festival.remove') }}
              </v-btn>
            </div>
            <v-alert
              v-if="refusalText !== null && refusedStationId === station.stationId"
              class="refusal mb-2"
              type="warning"
              variant="tonal"
            >
              {{ refusalText }}
            </v-alert>
          </div>

          <div v-if="atTheFestival.length === 0" class="festival-station-placeholder">
            <div class="row-line d-flex align-center flex-wrap ga-3 py-2 px-3">
              <span class="text-body-1">&nbsp;</span>
            </div>
          </div>
        </div>

        <div class="add-station-line d-flex align-center flex-wrap ga-3 mt-6">
          <v-autocomplete
            v-model="chosenStationId"
            class="station-search flex-grow-1"
            :items="stillToAdd"
            item-title="name"
            item-value="stationId"
            density="compact"
            hide-details
            :label="t('admin.festival.stationName')"
            :no-data-text="t('admin.festival.noStationsToAdd')"
          />
          <v-btn
            class="add-station"
            color="primary"
            variant="tonal"
            :disabled="chosenStationId === null"
            @click="add"
          >
            {{ t('admin.festival.addStation') }}
          </v-btn>
          <v-btn class="new-station" variant="text" @click="startCreating">
            {{ t('admin.stations.new') }}
          </v-btn>
        </div>
        <v-alert
          v-if="refusalText !== null && refusedStationId === null && !isCreating"
          class="refusal mt-3"
          type="warning"
          variant="tonal"
        >
          {{ refusalText }}
        </v-alert>
      </div>
    </v-card>

    <StationDialog
      v-if="isCreating"
      :station="null"
      :error-text="refusalText"
      @save="create"
      @cancel="stopCreating"
    />

    <BaseConfirmDialog
      v-if="removedStation !== null"
      :title="t('admin.festival.removeStationTitle')"
      :body="t('admin.festival.removeStationBody')"
      :confirm-label="t('admin.festival.remove')"
      @confirm="remove"
      @cancel="removedStation = null"
    />
  </section>
</template>

<style scoped>
.festival-station-row + .festival-station-row {
  border-top: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
}

.festival-station-row.tinted-row {
  background-color: rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 6px;
}
</style>
