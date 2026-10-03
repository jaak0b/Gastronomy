<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{
  title: string
  errorText: string | null
  saveLabel?: string
  cancelLabel?: string
  saveDisabled?: boolean
  busy?: boolean
  closeOnly?: boolean
}>()
const emit = defineEmits<{ save: []; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog :model-value="true" max-width="560" persistent scrollable>
    <v-card class="form-dialog" data-test="form-dialog">
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
