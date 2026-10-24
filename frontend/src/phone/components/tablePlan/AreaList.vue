<script setup lang="ts">
import type { TablePlanAreaView } from '../../../shared/api/generatedSchemas'

defineProps<{ areas: TablePlanAreaView[] }>()
const emit = defineEmits<{ 'open-area': [areaId: string] }>()
</script>

<template>
  <div class="area-list" data-test="area-list">
    <v-btn
      v-for="area in areas"
      :key="area.areaId"
      class="area-button"
      data-test="area-button"
      :data-test-id="area.areaId"
      variant="outlined"
      size="x-large"
      block
      @click="emit('open-area', area.areaId)"
    >
      {{ area.name }}
    </v-btn>
  </div>
</template>

<style scoped>
.area-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.area-button {
  height: auto;
  min-height: 4.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
}

.area-button :deep(.v-btn__content) {
  white-space: normal;
}
</style>
