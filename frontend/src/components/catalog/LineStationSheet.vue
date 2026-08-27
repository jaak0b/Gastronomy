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
  <v-dialog :model-value="true" max-width="480" persistent>
    <v-card class="line-station-sheet">
      <v-card-title>{{ t('line.whereTitle', { item: item.name }) }}</v-card-title>
      <v-card-text>{{ t('line.whereHelp') }}</v-card-text>
      <v-card-actions class="flex-column align-stretch">
        <v-btn
          v-for="locationId in choices"
          :key="locationId"
          class="station-choice mb-2"
          variant="tonal"
          block
          @click="$emit('choose', locationId)"
        >
          {{ locationNameFor(locationId) }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
