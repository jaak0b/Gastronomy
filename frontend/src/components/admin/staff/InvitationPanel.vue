<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Invitation } from '../../../stores/admin/staff'

const props = defineProps<{ invitation: Invitation }>()
defineEmits<{ close: [] }>()

const { t } = useI18n()
const QR_IMAGE_URL = '/api/admin/enrolment/invitations/current/qr.svg'
const wasCopied = ref(false)

const instruction = computed(() =>
  props.invitation.staffMember === null
    ? t('admin.enrol.forSomebodyNew')
    : t('admin.enrol.forSomebodyKnown', { name: props.invitation.staffMember.name }),
)

async function copyUrl(): Promise<void> {
  await navigator.clipboard.writeText(props.invitation.qrUrl)
  wasCopied.value = true
}
</script>

<template>
  <v-card class="invitation-panel mt-4">
    <v-card-title>{{ t('admin.enrol.title') }}</v-card-title>
    <v-card-text>
      <p class="instruction">{{ instruction }}</p>
      <img class="qr-image mt-3" :src="QR_IMAGE_URL" :alt="t('admin.enrol.qrAlt')" width="220" />
      <p class="validity text-medium-emphasis mt-2">{{ t('admin.enrol.validity') }}</p>
      <div class="d-flex align-center ga-2 mt-4">
        <code class="qr-url flex-grow-1 pa-2 rounded">{{ invitation.qrUrl }}</code>
        <v-btn
          class="copy-url"
          variant="text"
          size="small"
          :icon="wasCopied ? 'mdi-check' : 'mdi-content-copy'"
          :aria-label="wasCopied ? t('admin.enrol.copied') : t('admin.enrol.copyUrl')"
          @click="copyUrl"
        />
      </div>
    </v-card-text>
    <v-card-actions>
      <v-btn variant="text" @click="$emit('close')">{{ t('admin.cancel') }}</v-btn>
    </v-card-actions>
  </v-card>
</template>

<style scoped>
.qr-url {
  background-color: rgb(var(--v-theme-surface-light));
  font-size: 0.85rem;
  overflow-wrap: anywhere;
}
</style>
