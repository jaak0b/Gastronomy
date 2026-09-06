<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { invitationQrView, type InvitationQr } from '../../../core/invitationQr'
import type { Invitation } from '../../../core/apiTypes'

const props = defineProps<{ invitation: Invitation; qr: InvitationQr }>()
defineEmits<{ close: []; renew: [] }>()

const { t } = useI18n()
const wasCopied = ref(false)
const copyingIsUnavailable = ref(false)

const view = computed(() => invitationQrView(props.qr))

const station = computed(() => props.invitation.station ?? null)

const title = computed(() =>
  station.value === null ? t('admin.enrol.title') : t('admin.enrol.titleStation'),
)

const instruction = computed(() =>
  station.value === null
    ? t('admin.enrol.forSomebodyKnown')
    : t('admin.enrol.forStation', { name: station.value.name }),
)

watch(
  () => props.invitation.invitationId,
  () => {
    wasCopied.value = false
    copyingIsUnavailable.value = false
  },
)

async function copyUrl(): Promise<void> {
  const clipboard = navigator.clipboard ?? null
  if (clipboard === null) {
    copyingIsUnavailable.value = true
    return
  }
  try {
    await clipboard.writeText(props.invitation.qrUrl)
    wasCopied.value = true
    copyingIsUnavailable.value = false
  } catch {
    wasCopied.value = false
    copyingIsUnavailable.value = true
  }
}
</script>

<template>
  <v-card class="invitation-panel mt-4">
    <v-card-title>{{ title }}</v-card-title>
    <v-card-text>
      <template v-if="view.messageKey === null">
        <p class="instruction">{{ instruction }}</p>
        <img
          v-if="view.imageUrl !== null"
          class="qr-image mt-3"
          :src="view.imageUrl"
          :alt="t('admin.enrol.qrAlt')"
          width="220"
        />
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
        <p v-if="copyingIsUnavailable" class="copy-unavailable text-medium-emphasis mt-2">
          {{ t('admin.enrol.copyUnavailable') }}
        </p>
      </template>
      <v-alert v-else class="qr-gone" type="warning" variant="tonal">
        {{ t(view.messageKey) }}
      </v-alert>
    </v-card-text>
    <v-card-actions>
      <v-btn
        v-if="view.messageKey !== null"
        class="renew-code"
        color="primary"
        variant="text"
        @click="$emit('renew')"
      >
        {{ t('admin.enrol.newQrCode') }}
      </v-btn>
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
