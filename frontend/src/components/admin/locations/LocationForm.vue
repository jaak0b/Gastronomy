<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminLocation } from '../../../stores/admin/locations'

const props = defineProps<{ location: AdminLocation | null }>()
const emit = defineEmits<{
  save: [value: { locationId?: string; name: string; sortOrder: number }]
}>()

const { t } = useI18n()
const name = ref(props.location?.name ?? '')
const sortOrder = ref(props.location?.sortOrder ?? 1)

function save(): void {
  emit('save', {
    locationId: props.location?.locationId,
    name: name.value,
    sortOrder: sortOrder.value,
  })
}
</script>

<template>
  <v-form class="location-form pa-4" @submit.prevent="save">
    <v-text-field v-model="name" :label="t('admin.locations.title')" />
    <v-btn type="submit" color="primary" :disabled="name.trim().length === 0">
      {{ t('admin.save') }}
    </v-btn>
  </v-form>
</template>
