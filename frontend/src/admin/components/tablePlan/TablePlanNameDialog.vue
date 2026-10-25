<script setup lang="ts">
import { ref } from 'vue'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'

const props = defineProps<{ title: string; nameLabel: string; savedName: string; errorText: string | null }>()
const emit = defineEmits<{
  save: [name: string]
  cancel: []
}>()

const name = ref(props.savedName)
</script>

<template>
  <BaseFormDialog
    :title="title"
    :error-text="errorText"
    :save-disabled="name.trim().length === 0"
    @save="emit('save', name)"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="table-plan-name-field"
      data-test="table-plan-name-field"
      maxlength="40"
      :label="nameLabel"
    />
  </BaseFormDialog>
</template>
