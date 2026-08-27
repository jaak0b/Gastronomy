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
  <section class="admin-printers">
    <h1>{{ t('admin.printers.title') }}</h1>
    <p class="help">{{ t('admin.printers.testPrintHelp') }}</p>
    <p v-if="printers.errorMessage !== null" class="refusal error">
      {{
        printers.errorMessage.count === null
          ? t(printers.errorMessage.key, printers.errorMessage.parameters)
          : t(
              printers.errorMessage.key,
              printers.errorMessage.parameters,
              printers.errorMessage.count,
            )
      }}
    </p>
    <p v-if="printers.loadFailed" class="error">{{ t('admin.loadFailed') }}</p>
    <p v-else-if="printers.printers.length === 0" class="empty">
      {{ t('admin.printers.empty') }}
    </p>
    <article v-for="printer in printers.printers" :key="printer.locationId" class="printer-row">
      <h2>{{ printer.locationName }}</h2>
      <p class="status">{{ t(statusKey(printer)) }}</p>
      <p v-if="printer.waitingTicketCount > 0" class="waiting">
        {{
          t(
            'admin.printers.waiting',
            { count: printer.waitingTicketCount },
            printer.waitingTicketCount,
          )
        }}
      </p>
      <p v-if="printer.lastChangedAtUtc !== null" class="last-heard">
        {{ t('admin.printers.lastHeard', { time: printer.lastChangedAtUtc }) }}
      </p>
      <p v-if="printer.sharedWithLocationNames.length > 0" class="shared">
        {{ t('admin.printers.shared', { names: printer.sharedWithLocationNames.join(', ') }) }}
      </p>
      <button type="button" @click="printers.testPrint(printer.locationId)">
        {{ t('admin.printers.testPrint') }}
      </button>
      <button type="button" @click="printers.reconnect(printer.locationId)">
        {{ t('admin.printers.reconnect') }}
      </button>
      <p class="help">{{ t('admin.printers.reconnectHelp') }}</p>
      <button type="button" @click="editing = printer.locationId">{{ t('admin.edit') }}</button>
      <PrinterForm v-if="editing === printer.locationId" :printer="printer" @save="save" />
      <MockFaultPanel
        v-if="printer.transportKind === 'Mock'"
        :mock-folder-path="printer.mockFolderPath"
        @apply="(fault, mode) => printers.setMockFault(printer.locationId, fault, mode)"
      />
    </article>
  </section>
</template>
