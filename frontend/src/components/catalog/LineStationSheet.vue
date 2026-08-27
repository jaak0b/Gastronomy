<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { CatalogItem } from '../../core/apiTypes'
import { candidateLocations } from '../../core/routingPreview'

const props = defineProps<{
  item: CatalogItem
  locationNameFor: (locationId: string) => string
}>()
defineEmits<{ choose: [locationId: string] }>()

const { t } = useI18n()
const choices = candidateLocations(props.item)
</script>

<template>
  <section class="line-station-sheet">
    <h2>{{ t('line.whereTitle', { item: item.name }) }}</h2>
    <p>{{ t('line.whereHelp') }}</p>
    <button
      v-for="locationId in choices"
      :key="locationId"
      type="button"
      class="station-choice"
      @click="$emit('choose', locationId)"
    >
      {{ locationNameFor(locationId) }}
    </button>
  </section>
</template>
