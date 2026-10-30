<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ text: string }>()
const emit = defineEmits<{ reload: [] }>()

const { t } = useI18n()
</script>

<template>
  <div class="load-failure-notice" data-test="load-failure">
    <v-alert class="mb-4" data-test="load-failure-text" type="warning" variant="tonal">
      {{ text }}
    </v-alert>
    <v-btn-wide data-test="load-failure-reload" variant="outlined" @click="emit('reload')">
      {{ t('common.actions.reload') }}
    </v-btn-wide>
  </div>
</template>
