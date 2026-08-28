<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  useAdminPrintersStore,
  type AdminPrinter,
  type PrinterType,
  type SavePrinter,
} from '../../../stores/admin/printers'
import { assertNever } from '../../../core/assertNever'
import ConfirmDialog from '../ConfirmDialog.vue'
import PrinterForm from './PrinterForm.vue'
import NewPrinterDialog from './NewPrinterDialog.vue'

const { t } = useI18n()
const printers = useAdminPrintersStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const askingAboutId = ref<string | null>(null)

const refusal = computed(() => {
  const message = printers.errorMessage
  if (message === null) {
    return null
  }
  return message.count === null
    ? t(message.key, message.parameters)
    : t(message.key, message.parameters, message.count)
})

function labelForType(type: PrinterType): string {
  switch (type) {
    case 'TestPrinter':
      return t('admin.printers.typeTestPrinter')
    case 'EpsonTmT20ivNetworkPrinter':
      return t('admin.printers.typeEpsonTmT20ivNetworkPrinter')
    default:
      return assertNever(type)
  }
}

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

async function save(value: SavePrinter): Promise<void> {
  await printers.save(value)
  editingId.value = null
  isCreating.value = false
}

async function remove(): Promise<void> {
  const printerId = askingAboutId.value
  askingAboutId.value = null
  if (printerId !== null) {
    await printers.remove(printerId)
  }
}

onMounted(printers.load)
</script>

<template>
  <v-container class="admin-printers">
    <h1 class="text-h5 mb-2">{{ t('admin.printers.title') }}</h1>
    <p class="help text-medium-emphasis mb-4">{{ t('admin.printers.help') }}</p>

    <v-alert v-if="printers.loadFailed" class="error mb-4" type="error" variant="tonal">
      {{ t('admin.loadFailed') }}
    </v-alert>

    <v-card v-for="printer in printers.printers" :key="printer.printerId" class="printer-row mb-3">
      <div class="d-flex align-center ga-2 px-4 pt-3">
        <span class="name text-h6">{{ printer.name }}</span>
        <v-chip class="status" size="small">{{ t(statusKey(printer)) }}</v-chip>
        <v-spacer />
        <span class="type text-medium-emphasis">{{ labelForType(printer.printerType) }}</span>
      </div>
      <v-card-text class="py-2">
        <p v-if="printer.stationNames.length > 0" class="stations">
          {{ t('admin.printers.stationsOnThisPrinter', { names: printer.stationNames.join(', ') }) }}
        </p>
        <p v-if="printer.waitingTicketCount > 0" class="waiting">
          {{
            t(
              'admin.printers.waiting',
              { count: printer.waitingTicketCount },
              printer.waitingTicketCount,
            )
          }}
        </p>
        <v-alert
          v-if="refusal !== null && askingAboutId === null"
          class="refusal mt-2"
          type="warning"
          variant="tonal"
        >
          {{ refusal }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-btn class="test-print" variant="text" @click="printers.testPrint(printer.printerId)">
          {{ t('admin.printers.testPrint') }}
        </v-btn>
        <v-btn class="reconnect" variant="text" @click="printers.reconnect(printer.printerId)">
          {{ t('admin.printers.reconnect') }}
        </v-btn>
        <v-btn
          class="edit"
          variant="text"
          @click="editingId = editingId === printer.printerId ? null : printer.printerId"
        >
          {{ t('admin.edit') }}
        </v-btn>
        <v-spacer />
        <v-btn
          class="delete"
          icon="mdi-delete"
          variant="text"
          color="error"
          :aria-label="t('admin.printers.delete')"
          @click="askingAboutId = printer.printerId"
        />
      </v-card-actions>
      <v-expand-transition>
        <PrinterForm
          v-if="editingId === printer.printerId"
          :printer="printer"
          :printer-type="printer.printerType"
          @save="save"
        />
      </v-expand-transition>
    </v-card>

    <v-btn class="new-printer" color="primary" @click="isCreating = true">
      {{ t('admin.printers.new') }}
    </v-btn>

    <NewPrinterDialog v-if="isCreating" @save="save" @cancel="isCreating = false" />

    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.printers.deleteTitle')"
      :body="t('admin.printers.deleteBody')"
      :confirm-label="t('admin.printers.deleteConfirm')"
      @confirm="remove"
      @cancel="askingAboutId = null"
    />
  </v-container>
</template>
