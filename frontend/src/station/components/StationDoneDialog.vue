<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { itemLineText, type ItemLine } from '../../shared/core/stationBoard'

defineProps<{ tableName: string; lines: ItemLine[] }>()
const emit = defineEmits<{ confirmed: []; cancelled: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog class="station-done-dialog" model-value persistent fullscreen>
    <v-card class="card d-flex flex-column">
      <v-card-title class="title">{{ t('station.confirmDoneTitle') }}</v-card-title>
      <v-card-text class="body flex-grow-1">
        <div class="row row-table">
          <span class="label">{{ t('station.tableIs', { name: tableName }) }}</span>
        </div>
        <ul class="units">
          <li v-for="line in lines" :key="line.key" class="unit">
            {{ itemLineText(line, t) }}
          </li>
        </ul>
      </v-card-text>
      <v-card-actions class="actions justify-end">
        <v-btn class="cancel" variant="outlined" size="large" @click="emit('cancelled')">
          {{ t('station.cancel') }}
        </v-btn>
        <v-btn
          class="confirm"
          color="primary"
          variant="flat"
          size="large"
          @click="emit('confirmed')"
        >
          {{ t('station.prepared') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.card {
  height: 100%;
  border-radius: 0;
}

.body {
  font-size: 1.05rem;
  line-height: 1.4;
}

.row + .row {
  margin-top: 0.75rem;
}

.label {
  font-weight: 600;
}

.units {
  margin: 1rem 0 0;
  padding: 0;
  list-style: none;
  font-size: 1.25rem;
}

.unit + .unit {
  margin-top: 0.5rem;
}

.title {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  line-height: 1.35;
  padding-block: 1rem;
}

.actions {
  gap: 0.75rem;
  padding: 0 1rem 1rem;
}

.actions .v-btn {
  min-width: 12rem;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
}

.actions .v-btn :deep(.v-btn__content) {
  white-space: normal;
}
</style>
