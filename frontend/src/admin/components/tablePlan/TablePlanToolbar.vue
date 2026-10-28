<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminAreaView, AdminStationView, AdminTableView } from '../../../shared/api/generatedSchemas'
import { hiddenTablesOf } from '../../core/tablePlanPlacement'
import BaseShowButton from '../BaseShowButton.vue'

const props = defineProps<{
  area: AdminAreaView
  stationsOnNoPlan: AdminStationView[]
  festivalHasStations: boolean
}>()
const emit = defineEmits<{
  newTable: []
  placeStation: [stationId: string]
  showTable: [table: AdminTableView]
}>()

const { t } = useI18n()

const hiddenTables = computed(() => hiddenTablesOf(props.area))

const noStationsToPlaceText = computed(() =>
  props.festivalHasStations
    ? t('admin.tablePlan.messages.noStationsToPlace')
    : t('admin.tablePlan.messages.festivalHasNoStations'),
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

    <v-menu location="bottom start">
      <template #activator="{ props: menuActivator }">
        <v-btn
          v-bind="menuActivator"
          class="add-station"
          data-test="add-station"
          variant="text"
          append-icon="mdi-menu-down"
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
        <v-list-item
          v-if="stationsOnNoPlan.length === 0"
          data-test="no-station-to-place"
          disabled
          :title="noStationsToPlaceText"
        />
      </v-list>
    </v-menu>

    <v-spacer />

    <v-menu v-if="hiddenTables.length > 0" location="bottom end">
      <template #activator="{ props: menuActivator }">
        <v-btn
          v-bind="menuActivator"
          class="hidden-tables"
          data-test="hidden-tables"
          variant="text"
          append-icon="mdi-menu-down"
        >
          {{ t('admin.tablePlan.actions.hiddenTables') }}
        </v-btn>
      </template>
      <v-list data-test="hidden-tables-list" max-height="20rem">
        <v-list-item
          v-for="table in hiddenTables"
          :key="table.tableId"
          data-test="hidden-table"
          :data-test-id="table.tableId"
          :title="table.name"
        >
          <template #append>
            <BaseShowButton
              class="show-table"
              data-test="show-table"
              :label="t('admin.festivals.actions.show')"
              @click="emit('showTable', table)"
            />
          </template>
        </v-list-item>
      </v-list>
    </v-menu>
  </div>
</template>

<style scoped>
.table-plan-toolbar {
  border-bottom: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
}
</style>
