<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AdminTableView } from '../../../shared/api/generatedSchemas'
import { useBackStepForAMenu } from '../../../shared/composables/useBackStep'
import BaseShowButton from '../BaseShowButton.vue'

defineProps<{ hiddenTables: AdminTableView[] }>()
const emit = defineEmits<{ showTable: [table: AdminTableView] }>()

const { t } = useI18n()
const hiddenTablesAreOpen = useBackStepForAMenu()
</script>

<template>
  <v-menu v-model="hiddenTablesAreOpen" location="bottom end">
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
</template>
