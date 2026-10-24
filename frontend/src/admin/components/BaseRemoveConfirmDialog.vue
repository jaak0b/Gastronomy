<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseConfirmDialog from '../../shared/components/BaseConfirmDialog.vue'

defineProps<{ title: string; body: string }>()
const emit = defineEmits<{ confirm: []; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <BaseConfirmDialog
    :title="title"
    :body="body"
    :confirm-label="t('admin.festivals.actions.remove')"
    @confirm="emit('confirm')"
    @cancel="emit('cancel')"
  />
</template>
