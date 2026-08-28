<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminPrinter,
  SavePrinter,
  SimulatedFault,
  SimulatedFaultMode,
} from '../../../stores/admin/printers'
import { assertNever } from '../../../core/assertNever'

const props = defineProps<{ printer: AdminPrinter | null; name: string }>()
const emit = defineEmits<{ change: [value: SavePrinter] }>()

const { t } = useI18n()

const FAULTS: SimulatedFault[] = [
  'None',
  'PaperEnd',
  'CoverOpen',
  'ConnectTimeout',
  'DropSocketEarly',
  'DropSocketMidJob',
  'UnknownOutcome',
]

const saved = props.printer?.printerType === 'TestPrinter' ? props.printer : null
const fault = ref<SimulatedFault>(saved?.simulatedFault ?? 'None')
const mode = ref<SimulatedFaultMode>(saved?.simulatedFaultMode ?? 'Once')

function labelFor(value: SimulatedFault): string {
  switch (value) {
    case 'None':
      return t('admin.printers.fault.none')
    case 'PaperEnd':
      return t('admin.printers.fault.paperEnd')
    case 'CoverOpen':
      return t('admin.printers.fault.coverOpen')
    case 'ConnectTimeout':
      return t('admin.printers.fault.connectTimeout')
    case 'DropSocketEarly':
      return t('admin.printers.fault.dropEarly')
    case 'DropSocketMidJob':
      return t('admin.printers.fault.dropMidJob')
    case 'UnknownOutcome':
      return t('admin.printers.fault.unknown')
    default:
      return assertNever(value)
  }
}

function report(): void {
  emit('change', {
    printerType: 'TestPrinter',
    printerId: props.printer?.printerId,
    name: props.name,
    simulatedFault: fault.value,
    simulatedFaultMode: mode.value,
  })
}

watch([fault, mode, () => props.name], report, { immediate: true })
</script>

<template>
  <div class="test-printer-fields">
    <div class="text-subtitle-1">{{ t('admin.printers.faultTitle') }}</div>
    <p class="help text-medium-emphasis mb-2">{{ t('admin.printers.faultHelp') }}</p>
    <v-select
      v-model="fault"
      class="fault-field mb-4"
      :label="t('admin.printers.faultLabel')"
      :items="FAULTS.map((value) => ({ title: labelFor(value), value }))"
    />
    <v-select
      v-model="mode"
      class="mode-field mb-2"
      :label="t('admin.printers.faultModeLabel')"
      :items="[
        { title: t('admin.printers.fault.once'), value: 'Once' },
        { title: t('admin.printers.fault.sticky'), value: 'Sticky' },
      ]"
    />
  </div>
</template>
