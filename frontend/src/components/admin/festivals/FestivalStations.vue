<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  useAdminStationsStore,
  type AdminStation,
  type StationDraft,
} from '../../../stores/admin/stations'
import ConfirmDialog from '../ConfirmDialog.vue'
import { useRefusalText } from '../refusalText'
import StationForm from '../stations/StationForm.vue'

const props = defineProps<{ festivalId: string }>()

const { t } = useI18n()
const stations = useAdminStationsStore()
const chosenStationId = ref<string | null>(null)
const isCreating = ref(false)
const removedStation = ref<AdminStation | null>(null)
const refusedStationId = ref<string | null>(null)

const refusalText = useRefusalText([() => stations.errorMessage])

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
  if (await stations.addToTheFestival(props.festivalId, stationId)) {
    chosenStationId.value = null
  }
}

function startCreating(): void {
  stations.forgetError()
  refusedStationId.value = null
  isCreating.value = true
}

function stopCreating(): void {
  stations.forgetError()
  isCreating.value = false
}

async function create(draft: StationDraft): Promise<void> {
  const stationId = await stations.create(draft)
  if (stationId === null) {
    return
  }
  isCreating.value = false
  chosenStationId.value = stationId
}

async function remove(): Promise<void> {
  const station = removedStation.value
  removedStation.value = null
  if (station === null) {
    return
  }
  refusedStationId.value = station.stationId
  await stations.removeFromTheFestival(props.festivalId, station.stationId)
}
</script>

<template>
  <section class="festival-stations mb-4">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3">{{ t('admin.stations.title') }}</h2>

        <div class="rows mb-3">
          <div
            v-for="station in atTheFestival"
            :key="station.stationId"
            class="festival-station-row"
          >
            <div class="row-line d-flex align-center flex-wrap ga-3 py-2">
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
            <div class="row-line d-flex align-center flex-wrap ga-3 py-2">
              <span class="text-body-1">&nbsp;</span>
            </div>
          </div>
        </div>

        <div class="add-station-line d-flex align-center flex-wrap ga-3">
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

    <v-dialog v-if="isCreating" :model-value="true" max-width="560" persistent scrollable>
      <v-card class="new-station-dialog" role="dialog" aria-modal="true">
        <v-card-title class="new-station-title">{{ t('admin.stations.new') }}</v-card-title>
        <StationForm :station="null" @save="create" />
        <v-alert v-if="refusalText !== null" class="error mx-4 mb-4" type="error" variant="tonal">
          {{ refusalText }}
        </v-alert>
        <v-card-actions>
          <v-spacer />
          <v-btn class="cancel" variant="text" @click="stopCreating">
            {{ t('admin.cancel') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <ConfirmDialog
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
</style>
