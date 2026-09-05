<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { countedName } from '../../core/countedName'

const props = defineProps<{ categories: string[]; portionsFor: (category: string) => number }>()
const openCategory = defineModel<string | null>({ required: true })

const { t } = useI18n()

function labelFor(category: string): string {
  return countedName(props.portionsFor(category), category, t)
}

function holdsPortions(category: string): boolean {
  return props.portionsFor(category) > 0
}
</script>

<template>
  <v-tabs v-model="openCategory" class="category-tabs" grow show-arrows>
    <v-tab
      v-for="category in categories"
      :key="category"
      :value="category"
      class="category-tab"
      :class="{ 'holds-portions': holdsPortions(category) }"
      size="large"
    >
      {{ labelFor(category) }}
    </v-tab>
  </v-tabs>
</template>

<style scoped>
.category-tab.holds-portions:not(.v-tab--selected) {
  border-bottom: 4px solid rgb(var(--v-theme-success));
}

.category-tab.holds-portions :deep(.v-tab__slider) {
  background-color: rgb(var(--v-theme-success));
}
</style>
