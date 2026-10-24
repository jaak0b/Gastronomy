<script setup lang="ts">
import { useTemplateRef, type ComponentPublicInstance } from 'vue'
import { useI18n } from 'vue-i18n'

defineProps<{
  title: string
  errorText: string | null
  saveLabel?: string
  cancelLabel?: string
  saveDisabled?: boolean
  busy?: boolean
  closeOnly?: boolean
  wide?: boolean
}>()
const emit = defineEmits<{ save: []; cancel: [] }>()

const { t } = useI18n()
const card = useTemplateRef<ComponentPublicInstance>('card')

function focusTheFirstField(): void {
  const cardElement: unknown = card.value?.$el
  if (cardElement instanceof HTMLElement) {
    cardElement.querySelector<HTMLElement>('input:not([type="hidden"]):not([disabled]), textarea:not([disabled])')?.focus()
  }
}
</script>

<template>
  <v-dialog
    :model-value="true"
    :max-width="wide ? 880 : 560"
    persistent
    scrollable
    @after-enter="focusTheFirstField"
  >
    <v-card ref="card" class="form-dialog" data-test="form-dialog">
      <v-card-title class="form-dialog-title" data-test="form-dialog-title">{{ title }}</v-card-title>
      <v-form @submit.prevent="emit('save')">
        <v-card-text>
          <slot />
          <v-alert v-if="errorText !== null" class="refusal mt-4" data-test="refusal" type="warning" variant="tonal">
            {{ errorText }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn class="form-cancel" data-test="form-cancel" variant="text" :disabled="busy" @click="emit('cancel')">
            {{ cancelLabel ?? t('common.actions.cancel') }}
          </v-btn>
          <v-btn
            v-if="!closeOnly"
            class="form-save"
            data-test="form-save"
            type="submit"
            color="primary"
            :disabled="saveDisabled || busy"
          >
            {{ saveLabel ?? t('common.actions.save') }}
          </v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>
