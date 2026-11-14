<script setup lang="ts">
import BaseIconAction from '../../shared/components/BaseIconAction.vue'

defineProps<{ label: string }>()
</script>

<template>
  <BaseIconAction icon="mdi-delete" color="error" :label="label" />
</template>
