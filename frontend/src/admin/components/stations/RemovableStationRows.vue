<script setup lang="ts" generic="TStation">
import { shallowRef } from 'vue'
import type { AdminActionResult } from '../../core/adminActionResult'
import { useRowRefusals } from '../../composables/useRowRefusals'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseListRow from '../BaseListRow.vue'
import BaseRemoveConfirmDialog from '../BaseRemoveConfirmDialog.vue'
import BaseTrashButton from '../BaseTrashButton.vue'

const props = defineProps<{
  stations: readonly TStation[]
  idOf: (station: TStation) => string
  nameOf: (station: TStation) => string
  removalRefusalOf: (station: TStation) => string | null
  isSelected?: (station: TStation) => boolean
  rowTestName: string
  removeTitle: string
  removeBody: string
  removeThrough: (station: TStation) => Promise<AdminActionResult<unknown>>
}>()
defineSlots<{
  badges?: (scope: { station: TStation }) => unknown
  actions?: (scope: { station: TStation }) => unknown
}>()

const stationRefusals = useRowRefusals()
const stationAwaitingRemoval = shallowRef<TStation | null>(null)

function askToRemove(station: TStation): void {
  stationAwaitingRemoval.value = station
}

function stopAskingToRemove(): void {
  stationAwaitingRemoval.value = null
}

async function confirmRemoval(): Promise<void> {
  const station = stationAwaitingRemoval.value
  stationAwaitingRemoval.value = null
  if (station === null) {
    return
  }
  const stationId = props.idOf(station)
  stationRefusals.forgetRefusalOf(stationId)
  stationRefusals.showRefusalOf(stationId, await props.removeThrough(station))
}
</script>

<template>
  <div class="removable-station-rows">
    <BaseListRow
      v-for="(station, position) in stations"
      :key="idOf(station)"
      :position="position"
      :is-selected="isSelected?.(station) === true"
      :data-test="rowTestName"
      :data-test-id="idOf(station)"
    >
      <div class="row-line d-flex align-center flex-wrap ga-3 py-2 px-3">
        <span class="name text-body-1" data-test="station-name">{{ nameOf(station) }}</span>
        <slot name="badges" :station="station" />
        <v-spacer />
        <slot name="actions" :station="station" />
        <BaseActionWithRefusalReason
          class="remove-station-wrapper"
          data-test="remove-station-wrapper"
          :refusal-reason="removalRefusalOf(station)"
        >
          <template #default="{ refused }">
            <BaseTrashButton
              class="remove-station"
              data-test="remove-station"
              :disabled="refused"
              @click="askToRemove(station)"
            />
          </template>
        </BaseActionWithRefusalReason>
      </div>
      <v-alert
        v-if="stationRefusals.refusalTextOf(idOf(station)) !== null"
        class="refusal mx-3 mb-2"
        data-test="refusal"
        type="warning"
        variant="tonal"
      >
        {{ stationRefusals.refusalTextOf(idOf(station)) }}
      </v-alert>
    </BaseListRow>

    <BaseRemoveConfirmDialog
      v-if="stationAwaitingRemoval !== null"
      :title="removeTitle"
      :body="removeBody"
      @confirm="confirmRemoval"
      @cancel="stopAskingToRemove"
    />
  </div>
</template>
