<script setup lang="ts">
defineProps<{
  entries: readonly object[]
  entryTitle: string
  entryValue: string
  label: string
  noEntriesText: string
  addLabel: string
  searchTestName: string
  addTestName: string
}>()
const chosenId = defineModel<string | null>('chosenId', { required: true })
const emit = defineEmits<{ add: [] }>()
</script>

<template>
  <div class="base-search-add-line d-flex align-center flex-wrap ga-3">
    <v-autocomplete
      v-model="chosenId"
      class="search-field flex-grow-1"
      :data-test="searchTestName"
      :items="entries"
      :item-title="entryTitle"
      :item-value="entryValue"
      density="compact"
      hide-details
      :label="label"
      :no-data-text="noEntriesText"
    />
    <v-btn
      class="add-button"
      :data-test="addTestName"
      color="primary"
      variant="tonal"
      :disabled="chosenId === null"
      @click="emit('add')"
    >
      {{ addLabel }}
    </v-btn>
    <slot />
  </div>
</template>

<style scoped>
.search-field {
  min-width: 16rem;
}
</style>
