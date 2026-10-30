<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useBackStepForAMenu } from '../../shared/composables/useBackStep'
import BaseActionWithRefusalReason from './BaseActionWithRefusalReason.vue'

defineProps<{
  entries: readonly object[]
  entryTitle: string
  entryValue: string
  label: string
  unavailableReason: string | null
  addLabel: string
  searchTestName: string
  addTestName: string
}>()
const chosenId = defineModel<string | null>('chosenId', { required: true })
const emit = defineEmits<{ add: [] }>()

const { t } = useI18n()
const searchListIsOpen = useBackStepForAMenu()
</script>

<template>
  <div class="base-search-add-line d-flex align-center flex-wrap ga-3">
    <BaseActionWithRefusalReason
      class="search-add-wrapper flex-grow-1 ga-3 align-center"
      :data-test="`${searchTestName}-wrapper`"
      :refusal-reason="unavailableReason"
    >
      <template #default="{ refused }">
        <v-autocomplete
          v-model="chosenId"
          v-model:menu="searchListIsOpen"
          class="search-field flex-grow-1"
          :data-test="searchTestName"
          :items="entries"
          :item-title="entryTitle"
          :item-value="entryValue"
          hide-details
          :disabled="refused"
          :label="label"
          :no-data-text="t('admin.common.messages.noMatches')"
        />
        <v-btn
          class="add-button"
          :data-test="addTestName"
          color="primary"
          variant="tonal"
          :disabled="refused || chosenId === null"
          @click="emit('add')"
        >
          {{ addLabel }}
        </v-btn>
      </template>
    </BaseActionWithRefusalReason>
    <slot />
  </div>
</template>

<style scoped>
.search-field {
  min-width: 16rem;
}
</style>
