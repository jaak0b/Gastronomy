<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../../core/assertNever'
import type { AdminDevice } from '../../../core/apiTypes'
import { useAdminDevicesStore } from '../../../stores/admin/devices'
import ConfirmDialog from '../ConfirmDialog.vue'

const { t } = useI18n()
const devices = useAdminDevicesStore()
const askingAboutId = ref<string | null>(null)
let stopListening: (() => void) | null = null

const refusal = computed(() => {
  const message = devices.errorMessage
  if (message === null) {
    return null
  }
  return message.count === null
    ? t(message.key, message.parameters)
    : t(message.key, message.parameters, message.count)
})

function ownerOf(device: AdminDevice): string {
  switch (device.deviceKind) {
    case 'staffMember':
      return t('admin.devices.ownerStaffMember', { name: device.ownerName })
    case 'station':
      return t('admin.devices.ownerStation', { name: device.ownerName })
    default:
      return assertNever(device.deviceKind)
  }
}

function lastSeenOf(device: AdminDevice): string {
  return t('admin.devices.lastSeen', { time: new Date(device.lastSeenAtUtc).toLocaleString() })
}

async function revoke(): Promise<void> {
  const deviceId = askingAboutId.value
  askingAboutId.value = null
  if (deviceId !== null) {
    await devices.revoke(deviceId)
  }
}

onMounted(async () => {
  stopListening = devices.listen()
  await devices.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-devices">
    <h1 class="text-h5 mb-2">{{ t('admin.devices.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.devices.help') }}</p>

    <v-alert v-if="refusal !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{ refusal }}
    </v-alert>
    <v-alert v-if="devices.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>
    <v-alert v-if="devices.devices.length === 0" class="empty" type="info" variant="tonal">
      {{ t('admin.devices.empty') }}
    </v-alert>

    <v-card v-for="device in devices.devices" :key="device.deviceId" class="device-row mb-2">
      <div class="d-flex align-center ga-2 px-4 py-2">
        <div>
          <div class="owner text-body-1">{{ ownerOf(device) }}</div>
          <div class="last-seen text-body-2 text-medium-emphasis">{{ lastSeenOf(device) }}</div>
        </div>
        <v-spacer />
        <v-btn
          class="revoke"
          variant="text"
          color="error"
          size="large"
          @click="askingAboutId = device.deviceId"
        >
          {{ t('admin.devices.revoke') }}
        </v-btn>
      </div>
    </v-card>

    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.devices.revokeTitle')"
      :body="t('admin.devices.revokeBody')"
      :confirm-label="t('admin.devices.revokeConfirm')"
      @confirm="revoke"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>
