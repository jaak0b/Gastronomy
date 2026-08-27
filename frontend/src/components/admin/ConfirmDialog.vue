<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ title: string; body: string; confirmLabel: string }>()
const emit = defineEmits<{ confirm: []; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog :model-value="true" max-width="480" persistent>
    <v-card class="confirm-dialog" role="dialog" aria-modal="true">
      <v-card-title class="confirm-title">{{ title }}</v-card-title>
      <v-card-text class="confirm-body">{{ body }}</v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn class="cancel" variant="text" @click="emit('cancel')">
          {{ t('admin.cancel') }}
        </v-btn>
        <v-btn class="confirm" color="error" @click="emit('confirm')">
          {{ confirmLabel }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
