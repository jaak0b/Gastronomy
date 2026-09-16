<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{
  title: string
  errorText: string | null
  saveLabel?: string
  saveDisabled?: boolean
  busy?: boolean
}>()
const emit = defineEmits<{ save: []; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog :model-value="true" max-width="560" persistent scrollable>
    <v-card class="form-dialog" role="dialog" aria-modal="true">
      <v-card-title class="form-dialog-title">{{ title }}</v-card-title>
      <v-form @submit.prevent="emit('save')">
        <v-card-text>
          <slot />
          <v-alert v-if="errorText !== null" class="refusal mt-4" type="warning" variant="tonal">
            {{ errorText }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn class="form-cancel" variant="text" :disabled="busy" @click="emit('cancel')">
            {{ t('admin.cancel') }}
          </v-btn>
          <v-btn
            class="form-save"
            type="submit"
            color="primary"
            :disabled="saveDisabled || busy"
          >
            {{ saveLabel ?? t('admin.save') }}
          </v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>
