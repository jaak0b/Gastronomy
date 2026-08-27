<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminPrinter, TransportKind } from '../../../stores/admin/printers'
import { assertNever } from '../../../core/assertNever'

const props = defineProps<{ printer: AdminPrinter }>()
const emit = defineEmits<{ save: [printer: AdminPrinter] }>()

const { t } = useI18n()
const transport = ref<TransportKind>(props.printer.transport)
const host = ref(props.printer.host ?? '')
const port = ref(props.printer.port ?? 9100)

const TRANSPORTS: TransportKind[] = ['Network', 'Agent', 'Mock']

function labelFor(kind: TransportKind): string {
  switch (kind) {
    case 'Network':
      return t('admin.printers.kindNetwork')
    case 'Agent':
      return t('admin.printers.kindAgent')
    case 'Mock':
      return t('admin.printers.kindMock')
    default:
      return assertNever(kind)
  }
}

function save(): void {
  emit('save', {
    ...props.printer,
    transport: transport.value,
    host: host.value.trim().length === 0 ? null : host.value.trim(),
    port: port.value,
  })
}
</script>

<template>
  <form class="printer-form" @submit.prevent="save">
    <label>
      <select v-model="transport">
        <option v-for="kind in TRANSPORTS" :key="kind" :value="kind">{{ labelFor(kind) }}</option>
      </select>
    </label>
    <label>
      <input v-model="host" type="text" />
    </label>
    <p class="help">{{ t('admin.printers.hostHelp') }}</p>
    <p class="help">{{ t('admin.printers.sharedHelp') }}</p>
    <button type="submit">{{ t('admin.save') }}</button>
  </form>
</template>
