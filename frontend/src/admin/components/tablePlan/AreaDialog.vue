<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminAreaView } from '../../../shared/api/generatedSchemas'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'

const props = defineProps<{ area: AdminAreaView | null; errorText: string | null }>()
const emit = defineEmits<{
  save: [value: { name: string }]
  cancel: []
}>()

const { t } = useI18n()
const name = ref(props.area?.name ?? '')

function save(): void {
  emit('save', { name: name.value })
}
</script>

<template>
  <BaseFormDialog
    :title="area === null ? t('admin.tablePlan.actions.newArea') : t('admin.tablePlan.actions.editArea')"
    :error-text="errorText"
    :save-disabled="name.trim().length === 0"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="area-name-field"
      data-test="area-name-field"
      :label="t('admin.tablePlan.labels.areaName')"
    />
  </BaseFormDialog>
</template>
