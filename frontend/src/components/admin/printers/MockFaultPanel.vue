<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MockFault, MockFaultMode } from '../../../stores/admin/printers'
import { assertNever } from '../../../core/assertNever'

defineProps<{ mockFolderPath: string | null }>()
const emit = defineEmits<{ apply: [fault: MockFault, mode: MockFaultMode] }>()

const { t } = useI18n()
const fault = ref<MockFault>('None')
const mode = ref<MockFaultMode>('Once')

const FAULTS: MockFault[] = [
  'None',
  'PaperEnd',
  'CoverOpen',
  'ConnectTimeout',
  'DropSocketEarly',
  'DropSocketMidJob',
  'UnknownOutcome',
]

function labelFor(value: MockFault): string {
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
</script>

<template>
  <v-sheet class="mock-fault-panel pa-4 ma-4" border rounded>
    <div class="text-subtitle-1">{{ t('admin.printers.faultTitle') }}</div>
    <p class="help text-medium-emphasis mb-2">{{ t('admin.printers.faultHelp') }}</p>
    <v-select
      v-model="fault"
      class="fault-field"
      :items="FAULTS.map((value) => ({ title: labelFor(value), value }))"
    />
    <v-select
      v-model="mode"
      class="mode-field"
      :items="[
        { title: t('admin.printers.fault.once'), value: 'Once' },
        { title: t('admin.printers.fault.sticky'), value: 'Sticky' },
      ]"
    />
    <v-btn class="apply-fault mt-2" @click="emit('apply', fault, mode)">{{ t('admin.save') }}</v-btn>
    <p v-if="mockFolderPath !== null" class="folder text-medium-emphasis mt-2">
      {{ t('admin.printers.mockFolder', { path: mockFolderPath }) }}
    </p>
  </v-sheet>
</template>
