<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AdminLocation } from '../../../stores/admin/locations'

const props = defineProps<{
  itemName: string
  locations: AdminLocation[]
  selectedLocationIds: string[]
}>()
const emit = defineEmits<{ toggle: [locationId: string] }>()

const { t } = useI18n()

</script>

<template>
  <v-sheet class="assignment-editor pa-4 mb-4" border rounded>
    <div class="text-subtitle-1">{{ t('admin.assignment.title') }}</div>
    <p class="help text-medium-emphasis">{{ t('admin.assignment.help') }}</p>
    <v-checkbox
      v-for="location in locations"
      :key="location.locationId"
      :label="location.name"
      :model-value="selectedLocationIds.includes(location.locationId)"
      @update:model-value="emit('toggle', location.locationId)"
    />
  </v-sheet>
</template>
