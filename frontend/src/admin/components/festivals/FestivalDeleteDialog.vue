<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminFestivalView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'
import { useRefusalDisplay } from '../../composables/useRefusalDisplay'
import { useAdminFestivalsStore } from '../../stores/festivals'

const props = defineProps<{ festival: AdminFestivalView }>()
const emit = defineEmits<{ deleted: []; cancel: [] }>()

const { t } = useI18n()
const festivals = useAdminFestivalsStore()
const typedName = ref('')
const isDeleting = ref(false)
const { refusal, refusalText } = useRefusalDisplay()

const typedNameMatches = computed(() => typedName.value.trim() === props.festival.name)

async function deleteTheFestival(): Promise<void> {
  if (!typedNameMatches.value) {
    return
  }
  isDeleting.value = true
  refusal.value = null
  const deleted = await festivals.deleteFestival(props.festival.festivalId)
  isDeleting.value = false
  switch (deleted.kind) {
    case 'ok':
      emit('deleted')
      return
    case 'failed':
      refusal.value = deleted.message
      return
    default:
      assertNever(deleted)
  }
}
</script>

<template>
  <BaseFormDialog
    :title="t('admin.festivals.labels.confirmDeleteTitle')"
    :error-text="refusalText"
    :save-label="t('admin.festivals.actions.delete')"
    :save-disabled="!typedNameMatches"
    :busy="isDeleting"
    @save="deleteTheFestival"
    @cancel="emit('cancel')"
  >
    <p class="confirm-delete-body mb-4" data-test="confirm-delete-body">
      {{ t('admin.festivals.messages.confirmDeleteBody') }}
    </p>
    <v-text-field
      v-model="typedName"
      class="confirm-delete-name"
      data-test="confirm-delete-name"
      :label="t('admin.festivals.labels.name')"
    />
  </BaseFormDialog>
</template>
