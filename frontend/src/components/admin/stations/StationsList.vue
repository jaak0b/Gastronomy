<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import { useAdminEnrolmentStore } from '../../../stores/admin/enrolment'
import ConfirmDialog from '../ConfirmDialog.vue'
import InvitationPanel from '../enrolment/InvitationPanel.vue'
import { useRefusalText } from '../refusalText'
import StationForm from './StationForm.vue'

const { t } = useI18n()
const stations = useAdminStationsStore()
const enrolment = useAdminEnrolmentStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
let stopListening: (() => void) | null = null

const shown = computed(() =>
  stations.stations.filter((station) => showsDeactivated.value || station.isActive),
)

const refusalText = useRefusalText([() => enrolment.errorMessage, () => stations.errorMessage])

function inviteStation(stationId: string): void {
  void enrolment.createInvitation({ kind: 'station', stationId })
}

async function save(value: Parameters<typeof stations.save>[0]): Promise<void> {
  const wasSaved = await stations.save(value)
  if (!wasSaved) {
    return
  }
  editingId.value = null
  isCreating.value = false
}

async function deactivate(): Promise<void> {
  const stationId = askingAboutId.value
  askingAboutId.value = null
  if (stationId !== null) {
    await stations.setActive(stationId, false)
  }
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
    <h1 class="text-h5 mb-2">{{ t('admin.stations.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.stations.help') }}</p>

    <v-alert
      v-if="enrolment.enrolledStationName !== null"
      class="enrolled mb-4"
      type="success"
      variant="tonal"
    >
      {{ t('admin.enrol.doneStation', { name: enrolment.enrolledStationName }) }}
    </v-alert>
    <v-alert v-if="refusalText !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{ refusalText }}
    </v-alert>
    <v-alert v-if="stations.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <v-card v-for="station in shown" :key="station.stationId" class="station-row mb-2">
      <div class="d-flex align-center ga-2 px-4 py-2">
        <span class="name text-h6">{{ station.name }}</span>
        <v-chip v-if="!station.isActive" class="deactivated" size="small" color="grey">
          {{ t('admin.deactivated') }}
        </v-chip>
        <v-chip v-if="!station.hasDevice" class="no-tablet" size="small" color="warning">
          {{ t('admin.stations.noTablet') }}
        </v-chip>
        <v-spacer />
        <v-btn class="set-up-device" variant="text" @click="inviteStation(station.stationId)">
          {{ t('admin.stations.setUpDevice') }}
        </v-btn>
        <v-btn
          class="edit"
          variant="text"
          @click="editingId = editingId === station.stationId ? null : station.stationId"
        >
          {{ t('admin.edit') }}
        </v-btn>
        <v-btn
          v-if="station.isActive"
          class="deactivate"
          icon="mdi-delete"
          variant="text"
          color="error"
          :aria-label="t('admin.deactivate')"
          @click="askingAboutId = station.stationId"
        />
        <v-btn
          v-else
          class="reactivate"
          variant="text"
          @click="stations.setActive(station.stationId, true)"
        >
          {{ t('admin.stations.activate') }}
        </v-btn>
      </div>
      <v-expand-transition>
        <StationForm
          v-if="editingId === station.stationId"
          :station="station"
          @save="save"
        />
      </v-expand-transition>
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

    <v-btn class="new-station" color="primary" @click="isCreating = true">
      {{ t('admin.stations.new') }}
    </v-btn>

    <v-card v-if="isCreating" class="station-row mb-2">
      <StationForm :station="null" @save="save" />
    </v-card>

    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.stations.deactivateTitle')"
      :body="t('admin.stations.deactivateBody')"
      :confirm-label="t('admin.stations.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>
