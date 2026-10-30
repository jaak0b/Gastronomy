<script setup lang="ts">
import { ref } from 'vue'
import BaseConfirmDialog from '../../shared/components/BaseConfirmDialog.vue'
import BaseActionWithRefusalReason from './BaseActionWithRefusalReason.vue'
import BaseTrashButton from './BaseTrashButton.vue'

defineProps<{
  refusalReason: string | null
  trashTestName: string
  confirmTitle: string
  confirmBody: string
  confirmLabel: string
}>()
const emit = defineEmits<{ asked: []; confirmed: [] }>()

const isAsking = ref(false)

function ask(): void {
  isAsking.value = true
  emit('asked')
}

function confirm(): void {
  isAsking.value = false
  emit('confirmed')
}
</script>

<template>
  <BaseActionWithRefusalReason
    class="base-delete-action"
    :data-test="`${trashTestName}-wrapper`"
    :refusal-reason="refusalReason"
    @click.stop
    @keydown.enter.stop
    @keydown.space.stop
  >
    <template #default="{ refused }">
      <BaseTrashButton
        :data-test="trashTestName"
        :disabled="refused"
        @click="ask"
      />
      <BaseConfirmDialog
        v-if="isAsking"
        :title="confirmTitle"
        :body="confirmBody"
        :confirm-label="confirmLabel"
        @confirm="confirm"
        @cancel="isAsking = false"
      />
    </template>
  </BaseActionWithRefusalReason>
</template>
