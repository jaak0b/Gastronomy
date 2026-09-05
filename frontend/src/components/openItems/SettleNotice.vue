<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { SettleNotice } from '../../core/openItems'

const props = defineProps<{ notice: SettleNotice; closable: boolean }>()
const emit = defineEmits<{ dismiss: [] }>()

const { t } = useI18n()

const wording = computed(() =>
  props.notice.count === null
    ? t(props.notice.key, props.notice.parameters)
    : t(props.notice.key, props.notice.parameters, props.notice.count),
)
</script>

<template>
  <v-alert
    class="settle-notice"
    type="warning"
    variant="tonal"
    :closable="closable"
    @click:close="emit('dismiss')"
  >
    {{ wording }}
  </v-alert>
</template>
