<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useKeyboardInset } from '../../composables/useKeyboardInset'

defineProps<{
  itemName: string
  isCorrecting: boolean
}>()
const emit = defineEmits<{ confirm: [] }>()
const isOpen = defineModel<boolean>('isOpen', { required: true })
const typedNote = defineModel<string>('note', { required: true })

const { t } = useI18n()
const keyboardInset = useKeyboardInset()

const canConfirm = computed(() => typedNote.value.trim().length > 0)

function confirmIfANoteStands(): void {
  if (canConfirm.value) {
    emit('confirm')
  }
}
</script>

<template>
  <v-dialog
    v-model="isOpen"
    data-test="note-dialog-overlay"
    max-width="480"
    :style="{ height: `calc(100% - ${keyboardInset}px)`, bottom: 'auto' }"
  >
    <v-card data-test="note-dialog">
      <v-card-title data-test="note-dialog-title">
        {{ t('phone.catalog.labels.noteTitle', { name: itemName }) }}
      </v-card-title>
      <v-card-text>
        <v-text-field
          v-model="typedNote"
          data-test="note-input"
          maxlength="200"
          autofocus
          :label="t('phone.catalog.labels.itemNote')"
          :placeholder="t('phone.catalog.labels.lineNotePlaceholder')"
          persistent-placeholder
          @keyup.enter="confirmIfANoteStands"
        />
      </v-card-text>
      <v-card-actions>
        <v-btn data-test="note-cancel" variant="text" @click="isOpen = false">
          {{ t('common.actions.cancel') }}
        </v-btn>
        <v-spacer />
        <v-btn
          data-test="note-confirm"
          color="primary"
          variant="tonal"
          :disabled="!canConfirm"
          @click="emit('confirm')"
        >
          {{ isCorrecting ? t('phone.catalog.actions.noteSave') : t('phone.catalog.actions.noteAdd') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
