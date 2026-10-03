<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { OpenTableView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { isHeldBackByAnotherTable } from '../../core/openItems'
import OpenTablePanel from './OpenTablePanel.vue'

const props = defineProps<{
  tables: OpenTableView[]
  selectedItemIds: string[]
  language: AppLanguage
  itemsWithoutAnOrderCount: number
  everythingIsSettled: boolean
}>()
const emit = defineEmits<{
  'toggle-item': [orderItemId: string]
  'set-whole-table': [table: OpenTableView, isWanted: boolean]
}>()
const openedTables = defineModel<string[]>('openedTables', { required: true })

const { t } = useI18n()

function isHeldBack(table: OpenTableView): boolean {
  return isHeldBackByAnotherTable(props.tables, props.selectedItemIds, table.tableName)
}
</script>

<template>
  <v-alert
    v-if="itemsWithoutAnOrderCount > 0"
    class="mb-2"
    data-test="list-incomplete"
    type="warning"
    variant="tonal"
  >
    {{
      t(
        'phone.openItems.messages.listIncomplete',
        { count: itemsWithoutAnOrderCount },
        itemsWithoutAnOrderCount,
      )
    }}
  </v-alert>
  <v-alert v-if="everythingIsSettled" data-test="empty" type="info" variant="tonal">
    {{ t('phone.openItems.messages.empty') }}
  </v-alert>
  <v-expansion-panels v-model="openedTables" data-test="tables" multiple>
    <OpenTablePanel
      v-for="table in tables"
      :key="table.tableName"
      :table="table"
      :selected-item-ids="selectedItemIds"
      :language="language"
      :is-held-back-by-another-table="isHeldBack(table)"
      @toggle-item="emit('toggle-item', $event)"
      @set-whole-table="emit('set-whole-table', table, $event)"
    />
  </v-expansion-panels>
</template>
