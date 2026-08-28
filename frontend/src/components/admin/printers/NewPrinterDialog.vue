<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PrinterType, SavePrinter } from '../../../stores/admin/printers'
import { assertNever } from '../../../core/assertNever'
import PrinterForm from './PrinterForm.vue'

const emit = defineEmits<{ save: [value: SavePrinter]; cancel: [] }>()

const { t } = useI18n()

const PRINTER_TYPES: PrinterType[] = ['TestPrinter', 'EpsonTmT20ivNetworkPrinter']

const chosenType = ref<PrinterType>('TestPrinter')

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

function hintForType(type: PrinterType): string {
  switch (type) {
    case 'TestPrinter':
      return t('admin.printers.typeTestPrinterHint')
    case 'EpsonTmT20ivNetworkPrinter':
      return t('admin.printers.typeEpsonTmT20ivNetworkPrinterHint')
    default:
      return assertNever(type)
  }
}

const hint = computed(() => hintForType(chosenType.value))
</script>

<template>
  <v-dialog :model-value="true" max-width="560" persistent>
    <v-card class="new-printer-dialog" role="dialog" aria-modal="true">
      <v-card-title class="new-printer-title">{{ t('admin.printers.newTitle') }}</v-card-title>
      <v-card-text class="pb-0">
        <v-select
          v-model="chosenType"
          class="printer-type-field mb-2"
          :label="t('admin.printers.typeLabel')"
          :items="PRINTER_TYPES.map((type) => ({ title: labelForType(type), value: type }))"
        />
        <p class="type-hint text-medium-emphasis mb-1">{{ hint }}</p>
        <p class="type-fixed text-medium-emphasis">{{ t('admin.printers.typeFixed') }}</p>
      </v-card-text>
      <PrinterForm
        :printer="null"
        :printer-type="chosenType"
        is-cancellable
        @save="(value: SavePrinter) => emit('save', value)"
        @cancel="emit('cancel')"
      />
    </v-card>
  </v-dialog>
</template>
