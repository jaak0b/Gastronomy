<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminStation } from '../../../stores/admin/stations'

const props = defineProps<{ station: AdminStation | null }>()
const emit = defineEmits<{
  save: [value: { stationId?: string; name: string; sortOrder: number }]
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
  <v-form class="station-form pa-4" @submit.prevent="save">
    <v-text-field
      v-model="name"
      class="station-name-field mb-4"
      maxlength="40"
      :label="t('admin.stations.title')"
    />
    <v-btn type="submit" color="primary" :disabled="name.trim().length === 0">
      {{ t('admin.save') }}
    </v-btn>
  </v-form>
</template>
