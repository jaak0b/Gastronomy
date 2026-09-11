<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CatalogItem } from '../../core/apiTypes'
import { withEstimate } from '../../core/estimateWording'
import { candidateStations } from '../../core/routingPreview'

const props = defineProps<{
  item: CatalogItem
  stationNameFor: (stationId: string) => string
  estimateFor: (stationId: string) => number | null
  currentStationId: string | null
  withNote: boolean
  focusTheNote: boolean
}>()
const emit = defineEmits<{ choose: [stationId: string, note: string | null]; cancel: [] }>()

const { t } = useI18n()
const choices = candidateStations(props.item)
const typedNote = ref('')
const noteInput = ref<{ focus: () => void } | null>(null)

onMounted(() => {
  if (props.focusTheNote) {
    noteInput.value?.focus()
  }
})

function choose(stationId: string): void {
  const note = typedNote.value.trim()
  emit('choose', stationId, note.length > 0 ? note : null)
}
</script>

<template>
  <v-dialog :model-value="true" max-width="480" persistent>
    <v-card class="line-station-sheet">
      <v-card-title class="station-where-title">{{ t('line.whereTitle', { item: item.name }) }}</v-card-title>
      <v-card-text v-if="withNote">
        <v-text-field
          ref="noteInput"
          v-model="typedNote"
          class="station-note-input"
          maxlength="200"
          :label="t('catalog.itemNote')"
          :placeholder="t('catalog.lineNotePlaceholder')"
          persistent-placeholder
        />
      </v-card-text>
      <v-card-actions class="flex-column align-stretch">
        <v-btn
          v-for="stationId in choices"
          :key="stationId"
          class="station-choice mb-2"
          variant="tonal"
          block
          @click="choose(stationId)"
        >
          {{ withEstimate(stationNameFor(stationId), estimateFor(stationId), t) }}
          <span v-if="stationId === currentStationId" class="station-current text-medium-emphasis">
            {{ t('line.currentStation') }}
          </span>
        </v-btn>
        <v-btn class="cancel-station-choice" variant="text" block @click="emit('cancel')">
          {{ t('line.cancel') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.station-where-title {
  white-space: normal;
  overflow-wrap: anywhere;
}

.station-choice {
  white-space: normal;
  height: auto;
  min-height: 3rem;
  padding-block: 0.75rem;
  text-transform: none;
}
</style>
