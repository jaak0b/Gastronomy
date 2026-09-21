<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { AdminStationView } from '../../../shared/api/generatedSchemas'
import BaseFormDialog from '../BaseFormDialog.vue'

const props = defineProps<{ station: AdminStationView | null; errorText: string | null }>()
const emit = defineEmits<{
  save: [value: { stationId?: string; name: string; sortOrder: number }]
  cancel: []
}>()

const { t } = useI18n()
const name = ref(props.station?.name ?? '')
const sortOrder = ref(props.station?.sortOrder ?? 1)

function save(): void {
  emit('save', {
    stationId: props.station?.stationId,
    name: name.value,
    sortOrder: sortOrder.value,
  })
}
</script>

<template>
  <BaseFormDialog
    :title="station === null ? t('admin.stations.new') : t('admin.stations.edit')"
    :error-text="errorText"
    :save-disabled="name.trim().length === 0"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="station-name-field"
      maxlength="40"
      :label="t('admin.stations.title')"
    />
  </BaseFormDialog>
</template>
