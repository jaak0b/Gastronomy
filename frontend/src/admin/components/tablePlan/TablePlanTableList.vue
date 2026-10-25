<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminAreaView, AdminTableView, SavedTableView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import type { AdminActionResult } from '../../core/adminActionResult'
import { NEW_TABLE_SIZE, geometryForANewShapeIn } from '../../core/tablePlanPlacement'
import type { ShapeSelection } from '../../core/tablePlanSelection'
import type { EditSessionMode } from '../../composables/useEditSession'
import { useAdminTablePlanStore } from '../../stores/tablePlan'
import HideableEntryList from '../HideableEntryList.vue'
import TablePlanNameDialog from './TablePlanNameDialog.vue'

const props = defineProps<{ festivalId: string; area: AdminAreaView }>()
const selectedShape = defineModel<ShapeSelection | null>('selectedShape', { required: true })

const { t } = useI18n()
const tablePlan = useAdminTablePlanStore()

const selectedTableId = computed<string | null>(() => {
  const selection = selectedShape.value
  if (selection === null) {
    return null
  }
  switch (selection.kind) {
    case 'table':
      return selection.tableId
    case 'station':
      return null
    default:
      return assertNever(selection)
  }
})

async function saveTheTable(
  name: string,
  table: AdminTableView | null,
): Promise<AdminActionResult<SavedTableView | null>> {
  return table === null
    ? await tablePlan.createTable(props.festivalId, {
        areaId: props.area.areaId,
        name,
        geometry: geometryForANewShapeIn(props.area, NEW_TABLE_SIZE),
      })
    : await tablePlan.saveTable(props.festivalId, table.tableId, {
        areaId: props.area.areaId,
        name,
        geometry: table.geometry,
      })
}

function isSelected(table: AdminTableView): boolean {
  return table.tableId === selectedTableId.value
}

function hidingRefusalOf(table: AdminTableView): string | null {
  return table.hasItemsToHandOutOrSettleAtTheRunningFestival
    ? t('errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown')
    : null
}

function selectTheNewTable(saved: SavedTableView | null, mode: EditSessionMode): void {
  switch (mode) {
    case 'create':
      if (saved !== null) {
        selectedShape.value = { kind: 'table', tableId: saved.tableId }
      }
      return
    case 'edit':
      return
    default:
      assertNever(mode)
  }
}
</script>

<template>
  <section class="table-plan-table-list" data-test="table-plan-table-list">
    <HideableEntryList
      :entries="area.tables"
      :id-of="(table: AdminTableView) => table.tableId"
      :name-of="(table: AdminTableView) => table.name"
      :is-hidden="(table: AdminTableView) => table.isHidden"
      :hiding-refusal-of="hidingRefusalOf"
      :is-selected="isSelected"
      row-test-name="table-row"
      :new-label="t('admin.tablePlan.actions.newTable')"
      :hide-title="t('admin.tablePlan.labels.hideTableTitle')"
      :hide-body="t('admin.tablePlan.messages.hideTableBody')"
      :save-through="saveTheTable"
      :hide-through="(table: AdminTableView) => tablePlan.hideTable(festivalId, table.tableId)"
      :show-through="(table: AdminTableView) => tablePlan.showTable(festivalId, table.tableId)"
      @saved="selectTheNewTable"
    >
      <template #heading>
        <h2 class="section-heading text-h6">{{ t('admin.tablePlan.labels.tables') }}</h2>
      </template>
      <template #dialog="{ entry: table, errorText, save, cancel }">
        <TablePlanNameDialog
          :title="table === null ? t('admin.tablePlan.actions.newTable') : t('admin.tablePlan.actions.editTable')"
          :name-label="t('admin.tablePlan.labels.tableName')"
          :saved-name="table?.name ?? ''"
          :error-text="errorText"
          @save="save"
          @cancel="cancel"
        />
      </template>
    </HideableEntryList>
  </section>
</template>
