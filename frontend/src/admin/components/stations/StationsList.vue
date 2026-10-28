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
import { useStationRefusalReasons } from '../../composables/useStationRefusalReasons'
import StationDialog from './StationDialog.vue'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseHeadingWithCheckbox from '../BaseHeadingWithCheckbox.vue'
import BaseShowButton from '../BaseShowButton.vue'
import BaseTrashButton from '../BaseTrashButton.vue'

interface StationRow {
  station: AdminStationView
  deactivationRefusal: string | null
}

const { t } = useI18n()
const stations = useAdminStationsStore()
const enrolment = useAdminEnrolmentStore()
const editingStation = ref<AdminStationView | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const { refusal, refusalText, showRefusalOf } = useRefusalDisplay()
const { switchOffRefusalOf } = useStationRefusalReasons()
let stopListening: (() => void) | null = null

const rows = computed<StationRow[]>(() =>
  stations.stations
    .filter((station) => showsDeactivated.value || station.isActive)
    .map((station) => ({
      station,
      deactivationRefusal: switchOffRefusalOf(station),
    })),
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
    <BaseHeadingWithCheckbox
      v-model="showsDeactivated"
      class="admin-heading mb-4"
      :checkbox-label="t('admin.common.actions.showDeactivated')"
      checkbox-test-name="show-deactivated"
    >
      <h1 class="text-h5">{{ t('admin.stations.title') }}</h1>
    </BaseHeadingWithCheckbox>

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
      data-test="refusal"
      type="warning"
      variant="tonal"
    >
      {{ refusalText }}
    </v-alert>

    <v-card
      v-for="{ station, deactivationRefusal } in rows"
      :key="station.stationId"
      class="station-row mb-2"
      data-test="station-row"
    >
      <div class="d-flex align-center flex-wrap ga-2 px-4 py-2">
        <span class="name text-h6" data-test="name">{{ station.name }}</span>
        <v-chip v-if="!station.isActive" class="deactivated" data-test="deactivated" size="small" color="grey">
          {{ t('admin.common.labels.deactivated') }}
        </v-chip>
        <v-chip v-if="!station.hasDevice" class="no-tablet" size="small" color="warning">
          {{ t('admin.stations.labels.noTablet') }}
        </v-chip>
        <v-spacer />
        <v-btn class="set-up-device" data-test="set-up-device" variant="text" @click="inviteStation(station.stationId)">
          {{ t('admin.stations.actions.setUpDevice') }}
        </v-btn>
        <BaseEditButton class="edit" data-test="edit" @click="startEditing(station)" />
        <BaseActionWithRefusalReason
          v-if="station.isActive"
          class="deactivate-wrapper"
          data-test="deactivate-wrapper"
          :refusal-reason="deactivationRefusal"
        >
          <template #default="{ refused }">
            <BaseTrashButton
              class="deactivate"
              data-test="deactivate"
              :disabled="refused"
              @click="askingAboutId = station.stationId"
            />
          </template>
        </BaseActionWithRefusalReason>
        <BaseShowButton
          v-else
          class="reactivate"
          data-test="reactivate"
          :label="t('admin.stations.actions.activate')"
          @click="reactivate(station.stationId)"
        />
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

    <v-btn class="new-station mt-6" data-test="new-station" color="primary" @click="startCreating">
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
