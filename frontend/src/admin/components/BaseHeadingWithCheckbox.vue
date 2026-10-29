<script setup lang="ts">
defineProps<{ checkboxLabel: string; checkboxTestName: string }>()
const checked = defineModel<boolean>({ required: true })
</script>

<template>
  <div class="base-heading-with-checkbox d-flex align-center flex-wrap justify-space-between ga-2">
    <slot />
    <v-checkbox
      v-model="checked"
      class="heading-checkbox flex-grow-0"
      :data-test="checkboxTestName"
      hide-details
      :label="checkboxLabel"
    />
  </div>
</template>
