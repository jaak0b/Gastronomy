<script setup lang="ts">
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminPrinter, SavePrinter } from '../../../stores/admin/printers'

const props = defineProps<{ printer: AdminPrinter | null; name: string }>()
const emit = defineEmits<{ change: [value: SavePrinter] }>()

const { t } = useI18n()

const saved = props.printer?.printerType === 'EpsonTmT20ivNetworkPrinter' ? props.printer : null
const host = ref(saved?.host ?? '')
const port = ref(saved?.port ?? 9100)

function report(): void {
  emit('change', {
    printerType: 'EpsonTmT20ivNetworkPrinter',
    printerId: props.printer?.printerId,
    name: props.name,
    host: host.value.trim(),
    port: port.value,
  })
}

watch([host, port, () => props.name], report, { immediate: true })
</script>

<template>
  <div class="network-printer-fields">
    <v-text-field
      v-model="host"
      class="host-field mb-4"
      :label="t('admin.printers.hostLabel')"
    />
    <v-text-field
      v-model.number="port"
      class="port-field mb-2"
      type="number"
      :label="t('admin.printers.portLabel')"
    />
  </div>
</template>
