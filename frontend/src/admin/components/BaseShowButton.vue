<script setup lang="ts">
import BaseIconAction from '../../shared/components/BaseIconAction.vue'

defineProps<{ label: string }>()
</script>

<template>
  <BaseIconAction icon="mdi-eye" :label="label" />
</template>
