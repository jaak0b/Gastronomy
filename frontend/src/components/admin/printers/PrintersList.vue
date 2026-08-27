<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminPrintersStore, type AdminPrinter } from '../../../stores/admin/printers'
import PrinterForm from './PrinterForm.vue'
import MockFaultPanel from './MockFaultPanel.vue'

const { t } = useI18n()
const printers = useAdminPrintersStore()
const editing = ref<string | null>(null)

function statusKey(printer: AdminPrinter): string {
  if (printer.isFaulty) {
    return 'admin.printers.faulty'
  }
  if (!printer.isOnline) {
    return 'admin.printers.offline'
  }
  if (printer.isPaperEnd) {
    return 'admin.printers.paperEnd'
  }
  if (printer.isCoverOpen) {
    return 'admin.printers.coverOpen'
  }
  if (printer.isPaperNearEnd) {
    return 'admin.printers.paperNearEnd'
  }
  return 'admin.printers.online'
}

async function save(printer: AdminPrinter): Promise<void> {
  await printers.save(printer)
  editing.value = null
}

onMounted(printers.load)
</script>

<template>
  <v-container class="admin-printers">
    <h1 class="text-h5 mb-2">{{ t('admin.printers.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.printers.testPrintHelp') }}</p>

    <v-alert v-if="printers.errorMessage !== null" class="refusal mb-4" type="warning" variant="tonal">
      {{
        printers.errorMessage.count === null
          ? t(printers.errorMessage.key, printers.errorMessage.parameters)
          : t(
              printers.errorMessage.key,
              printers.errorMessage.parameters,
              printers.errorMessage.count,
            )
      }}
    </v-alert>
    <v-alert v-if="printers.loadFailed" class="error" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-card v-for="printer in printers.printers" :key="printer.stationId" class="printer-row mb-3">
      <v-card-item>
        <v-card-title>{{ printer.stationName }}</v-card-title>
        <v-card-subtitle class="status">{{ t(statusKey(printer)) }}</v-card-subtitle>
      </v-card-item>
      <v-card-text>
        <p v-if="printer.waitingTicketCount > 0" class="waiting">
          {{
            t(
              'admin.printers.waiting',
              { count: printer.waitingTicketCount },
              printer.waitingTicketCount,
            )
          }}
        </p>
        <p v-if="printer.lastChangedAtUtc !== null" class="last-heard text-medium-emphasis">
          {{ t('admin.printers.lastHeard', { time: printer.lastChangedAtUtc }) }}
        </p>
        <p v-if="printer.sharedWithStationNames.length > 0" class="shared text-medium-emphasis">
          {{ t('admin.printers.shared', { names: printer.sharedWithStationNames.join(', ') }) }}
        </p>
        <p class="help text-medium-emphasis">{{ t('admin.printers.reconnectHelp') }}</p>
      </v-card-text>
      <v-card-actions>
        <v-btn class="test-print" variant="text" @click="printers.testPrint(printer.stationId)">
          {{ t('admin.printers.testPrint') }}
        </v-btn>
        <v-btn class="reconnect" variant="text" @click="printers.reconnect(printer.stationId)">
          {{ t('admin.printers.reconnect') }}
        </v-btn>
        <v-btn class="edit" variant="text" @click="editing = printer.stationId">
          {{ t('admin.edit') }}
        </v-btn>
      </v-card-actions>
      <PrinterForm v-if="editing === printer.stationId" :printer="printer" @save="save" />
      <MockFaultPanel
        v-if="printer.transportKind === 'Mock'"
        :mock-folder-path="printer.mockFolderPath"
        @apply="(fault, mode) => printers.setMockFault(printer.stationId, fault, mode)"
      />
    </v-card>
  </v-container>
</template>
