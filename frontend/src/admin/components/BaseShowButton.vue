<script setup lang="ts">
import BaseIconAction from './BaseIconAction.vue'

defineProps<{ label: string }>()
</script>

<template>
  <BaseIconAction icon="mdi-eye" :label="label" />
</template>
