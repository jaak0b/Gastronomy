<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ title: string; body: string; confirmLabel: string }>()
const emit = defineEmits<{ confirm: []; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <div class="confirm-backdrop">
    <section
      class="confirm-dialog"
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-title"
    >
      <h2 id="confirm-title" class="confirm-title">{{ title }}</h2>
      <p class="confirm-body">{{ body }}</p>
      <button type="button" class="confirm" @click="emit('confirm')">{{ confirmLabel }}</button>
      <button type="button" class="cancel" @click="emit('cancel')">{{ t('admin.cancel') }}</button>
    </section>
  </div>
</template>
