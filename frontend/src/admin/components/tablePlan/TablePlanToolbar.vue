<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminAreaView, AdminStationView, AdminTableView } from '../../../shared/api/generatedSchemas'
import BaseIconAction from '../../../shared/components/BaseIconAction.vue'
import { useBackStepForAMenu } from '../../../shared/composables/useBackStep'
import { canZoomIn, canZoomOut, hiddenTablesOf } from '../../core/tablePlanPlacement'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import TablePlanToolbarHiddenTables from './TablePlanToolbarHiddenTables.vue'

const props = defineProps<{
  area: AdminAreaView
  stationsOnNoPlan: AdminStationView[]
  zoomStep: number
}>()
const emit = defineEmits<{
  newTable: []
  placeStation: [stationId: string]
  showTable: [table: AdminTableView]
  zoomIn: []
  zoomOut: []
}>()

const { t } = useI18n()
const stationsToPlaceAreOpen = useBackStepForAMenu()

const hiddenTables = computed(() => hiddenTablesOf(props.area))

const stationsUnavailableReason = computed(() =>
  props.stationsOnNoPlan.length === 0 ? t('admin.common.messages.noStationsAvailable') : null,
)
</script>

<template>
  <div class="table-plan-toolbar d-flex align-center flex-wrap ga-2 px-2 py-1" data-test="table-plan-toolbar">
    <v-btn
      class="new-table"
      data-test="new-table"
      variant="text"
      @click="emit('newTable')"
    >
      {{ t('admin.tablePlan.actions.newTable') }}
    </v-btn>

    <BaseActionWithRefusalReason
      class="add-station-wrapper"
      data-test="add-station-wrapper"
      :refusal-reason="stationsUnavailableReason"
    >
      <template #default="{ refused }">
        <v-menu v-model="stationsToPlaceAreOpen" location="bottom start">
          <template #activator="{ props: menuActivator }">
            <v-btn
              v-bind="menuActivator"
              class="add-station"
              data-test="add-station"
              variant="text"
              append-icon="mdi-menu-down"
              :disabled="refused"
            >
              {{ t('admin.tablePlan.actions.addStation') }}
            </v-btn>
          </template>
          <v-list data-test="stations-to-place" max-height="20rem">
            <v-list-item
              v-for="station in stationsOnNoPlan"
              :key="station.stationId"
              data-test="station-to-place"
              :data-test-id="station.stationId"
              :title="station.name"
              @click="emit('placeStation', station.stationId)"
            />
          </v-list>
        </v-menu>
      </template>
    </BaseActionWithRefusalReason>

    <v-spacer />

    <BaseIconAction
      class="zoom-out"
      data-test="zoom-out"
      icon="mdi-magnify-minus-outline"
      location="bottom"
      :label="t('admin.tablePlan.actions.zoomOut')"
      :disabled="!canZoomOut(zoomStep)"
      @click="emit('zoomOut')"
    />
    <BaseIconAction
      class="zoom-in"
      data-test="zoom-in"
      icon="mdi-magnify-plus-outline"
      location="bottom"
      :label="t('admin.tablePlan.actions.zoomIn')"
      :disabled="!canZoomIn(zoomStep)"
      @click="emit('zoomIn')"
    />

    <TablePlanToolbarHiddenTables
      v-if="hiddenTables.length > 0"
      :hidden-tables="hiddenTables"
      @show-table="emit('showTable', $event)"
    />
  </div>
</template>

<style scoped>
.table-plan-toolbar {
  border-bottom: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
}
</style>
