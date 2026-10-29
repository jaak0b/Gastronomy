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
      :name="nameOf(station)"
      name-test-name="station-name"
      :refusal-text="stationRefusals.refusalTextOf(idOf(station))"
      :data-test="rowTestName"
      :data-test-id="idOf(station)"
    >
      <template #facts>
        <slot name="badges" :station="station" />
      </template>
      <template #actions>
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
      </template>
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
