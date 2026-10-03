<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { AdminStationView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { useAdminStationsStore } from '../../stores/stations'
import { useAdminEnrolmentStore } from '../../stores/enrolment'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import InvitationPanel from '../enrolment/InvitationPanel.vue'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import StationDialog from './StationDialog.vue'

const { t } = useI18n()
const stations = useAdminStationsStore()
const enrolment = useAdminEnrolmentStore()
const editingStation = ref<AdminStationView | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const { refusal, refusalText, showRefusalOf } = useRefusalDisplay()
let stopListening: (() => void) | null = null

const shown = computed(() =>
  stations.stations.filter((station) => showsDeactivated.value || station.isActive),
)

const isStationDialogOpen = computed(
  () => isCreating.value || editingStation.value !== null,
)

async function inviteStation(stationId: string): Promise<void> {
  refusal.value = null
  showRefusalOf(await enrolment.createInvitation({ kind: 'station', stationId }))
}

function startEditing(station: AdminStationView): void {
  editingStation.value = station
  refusal.value = null
}

function stopEditing(): void {
  editingStation.value = null
  refusal.value = null
}

function startCreating(): void {
  isCreating.value = true
  refusal.value = null
}

function stopCreating(): void {
  isCreating.value = false
  refusal.value = null
}

async function save(value: Parameters<typeof stations.save>[0]): Promise<void> {
  refusal.value = null
  const saved = await stations.save(value)
  switch (saved.kind) {
    case 'ok':
      editingStation.value = null
      isCreating.value = false
      return
    case 'failed':
      refusal.value = saved.message
      return
    default:
      assertNever(saved)
  }
}

async function deactivate(): Promise<void> {
  const stationId = askingAboutId.value
  askingAboutId.value = null
  if (stationId !== null) {
    refusal.value = null
    showRefusalOf(await stations.setActive(stationId, false))
  }
}

async function reactivate(stationId: string): Promise<void> {
  refusal.value = null
  showRefusalOf(await stations.setActive(stationId, true))
}

onMounted(async () => {
  stopListening = stations.listen()
  await stations.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-stations">
    <div class="admin-heading d-flex align-center flex-wrap justify-space-between ga-2 mb-4">
      <h1 class="text-h5">{{ t('admin.stations.title') }}</h1>
      <v-checkbox
        v-model="showsDeactivated"
        class="show-deactivated"
        density="compact"
        hide-details
        :label="t('admin.common.actions.showDeactivated')"
      />
    </div>

    <v-alert
      v-if="enrolment.enrolledStationName !== null"
      class="enrolled mb-4"
      type="success"
      variant="tonal"
    >
      {{ t('admin.enrolment.messages.doneStation', { name: enrolment.enrolledStationName }) }}
    </v-alert>
    <v-alert
      v-if="refusalText !== null && !isStationDialogOpen"
      class="refusal mb-4"
      type="warning"
      variant="tonal"
    >
      {{ refusalText }}
    </v-alert>
    <v-alert v-if="stations.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.common.errors.loadFailed') }}
    </v-alert>

    <v-card v-for="station in shown" :key="station.stationId" class="station-row mb-2">
      <div class="d-flex align-center flex-wrap ga-2 px-4 py-2">
        <span class="name text-h6">{{ station.name }}</span>
        <v-chip v-if="!station.isActive" class="deactivated" size="small" color="grey">
          {{ t('admin.common.labels.deactivated') }}
        </v-chip>
        <v-chip v-if="!station.hasDevice" class="no-tablet" size="small" color="warning">
          {{ t('admin.stations.labels.noTablet') }}
        </v-chip>
        <v-spacer />
        <v-btn class="set-up-device" variant="text" @click="inviteStation(station.stationId)">
          {{ t('admin.stations.actions.setUpDevice') }}
        </v-btn>
        <v-btn class="edit" variant="text" @click="startEditing(station)">
          {{ t('admin.common.actions.edit') }}
        </v-btn>
        <v-btn
          v-if="station.isActive"
          class="deactivate"
          icon="mdi-delete"
          variant="text"
          color="error"
          @click="askingAboutId = station.stationId"
        />
        <v-btn
          v-else
          class="reactivate"
          variant="text"
          @click="reactivate(station.stationId)"
        >
          {{ t('admin.stations.actions.activate') }}
        </v-btn>
      </div>
      <v-expand-transition>
        <InvitationPanel
          v-if="enrolment.invitation?.station?.id === station.stationId"
          :invitation="enrolment.invitation"
          :qr="enrolment.invitationQr"
          @close="enrolment.closeInvitation"
          @renew="inviteStation(station.stationId)"
        />
      </v-expand-transition>
    </v-card>

    <v-btn class="new-station mt-6" color="primary" @click="startCreating">
      {{ t('admin.stations.actions.new') }}
    </v-btn>

    <StationDialog
      v-if="editingStation !== null"
      :station="editingStation"
      :error-text="refusalText"
      @save="save"
      @cancel="stopEditing"
    />
    <StationDialog
      v-if="isCreating"
      :station="null"
      :error-text="refusalText"
      @save="save"
      @cancel="stopCreating"
    />

    <BaseConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.stations.labels.deactivateTitle')"
      :confirm-label="t('admin.stations.actions.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>
