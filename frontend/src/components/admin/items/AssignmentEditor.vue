<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminLocation } from '../../../stores/admin/locations'

const props = defineProps<{
  itemName: string
  locations: AdminLocation[]
  selectedLocationIds: string[]
}>()
const emit = defineEmits<{ toggle: [locationId: string] }>()

const { t } = useI18n()

const selectedNames = computed(() =>
  props.locations
    .filter((location) => props.selectedLocationIds.includes(location.locationId))
    .map((location) => location.name),
)

const previewText = computed(() => {
  if (selectedNames.value.length === 0) {
    return t('admin.items.needsLocation')
  }
  if (selectedNames.value.length === 1) {
    return t('admin.assignment.preview', {
      item: props.itemName,
      location: selectedNames.value[0],
    })
  }
  return t('admin.assignment.previewChoice', {
    item: props.itemName,
    locations: selectedNames.value.join(', '),
  })
})
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
    <v-alert class="preview" type="info" variant="tonal" density="compact">
      {{ previewText }}
    </v-alert>
  </v-sheet>
</template>
