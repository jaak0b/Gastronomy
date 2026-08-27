<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminPrinter, TransportKind } from '../../../stores/admin/printers'
import { assertNever } from '../../../core/assertNever'

const props = defineProps<{ printer: AdminPrinter }>()
const emit = defineEmits<{ save: [printer: AdminPrinter] }>()

const { t } = useI18n()
const transport = ref<TransportKind>(props.printer.transportKind)
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
    transportKind: transport.value,
    host: host.value.trim().length === 0 ? null : host.value.trim(),
    port: port.value,
  })
}
</script>

<template>
  <v-form class="printer-form pa-4" @submit.prevent="save">
    <v-select
      v-model="transport"
      class="transport-field"
      :label="t('admin.printers.title')"
      :items="TRANSPORTS.map((kind) => ({ title: labelFor(kind), value: kind }))"
    />
    <v-text-field v-model="host" class="host-field" :label="t('admin.printers.hostHelp')" />
    <p class="help text-medium-emphasis">{{ t('admin.printers.sharedHelp') }}</p>
    <v-btn type="submit" color="primary" class="mt-2">{{ t('admin.save') }}</v-btn>
  </v-form>
</template>
