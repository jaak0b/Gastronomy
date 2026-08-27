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
  <section class="mock-fault-panel">
    <h3>{{ t('admin.printers.faultTitle') }}</h3>
    <p class="help">{{ t('admin.printers.faultHelp') }}</p>
    <label>
      <select v-model="fault">
        <option v-for="value in FAULTS" :key="value" :value="value">{{ labelFor(value) }}</option>
      </select>
    </label>
    <label>
      <select v-model="mode">
        <option value="Once">{{ t('admin.printers.fault.once') }}</option>
        <option value="Sticky">{{ t('admin.printers.fault.sticky') }}</option>
      </select>
    </label>
    <button type="button" @click="emit('apply', fault, mode)">{{ t('admin.save') }}</button>
    <p v-if="mockFolderPath !== null" class="folder">
      {{ t('admin.printers.mockFolder', { path: mockFolderPath }) }}
    </p>
  </section>
</template>
