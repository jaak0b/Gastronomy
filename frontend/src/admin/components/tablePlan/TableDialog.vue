<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminTableView } from '../../../shared/api/generatedSchemas'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'

const props = defineProps<{ table: AdminTableView | null; errorText: string | null }>()
const emit = defineEmits<{
  save: [value: { name: string }]
  cancel: []
}>()

const { t } = useI18n()
const name = ref(props.table?.name ?? '')

function save(): void {
  emit('save', { name: name.value })
}
</script>

<template>
  <BaseFormDialog
    :title="table === null ? t('admin.tablePlan.actions.newTable') : t('admin.tablePlan.actions.editTable')"
    :error-text="errorText"
    :save-disabled="name.trim().length === 0"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="table-name-field"
      data-test="table-name-field"
      :label="t('admin.tablePlan.labels.tableName')"
    />
  </BaseFormDialog>
</template>
