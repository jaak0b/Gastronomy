<script setup lang="ts">
import type { MoveDirection } from '../../shared/api/generatedSchemas'

const emit = defineEmits<{ move: [direction: MoveDirection] }>()
</script>

<template>
  <span class="base-move-buttons d-inline-flex align-center">
    <v-btn class="move-up" data-test="move-up" icon="mdi-arrow-up" variant="text" @click="emit('move', 'up')" />
    <v-btn class="move-down" data-test="move-down" icon="mdi-arrow-down" variant="text" @click="emit('move', 'down')" />
  </span>
</template>
