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
    .filter((location) => props.selectedLocationIds.includes(location.id))
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
  <fieldset class="assignment-editor">
    <legend>{{ t('admin.assignment.title') }}</legend>
    <p class="help">{{ t('admin.assignment.help') }}</p>
    <label v-for="location in locations" :key="location.id">
      <input
        type="checkbox"
        :checked="selectedLocationIds.includes(location.id)"
        @change="emit('toggle', location.id)"
      />
      <span>{{ location.name }}</span>
    </label>
    <p class="preview">{{ previewText }}</p>
  </fieldset>
</template>
