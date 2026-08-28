<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminPrinter, PrinterType, SavePrinter } from '../../../stores/admin/printers'
import TestPrinterFields from './TestPrinterFields.vue'
import NetworkPrinterFields from './NetworkPrinterFields.vue'

const props = defineProps<{
  printer: AdminPrinter | null
  printerType: PrinterType
  isCancellable?: boolean
}>()
const emit = defineEmits<{ save: [value: SavePrinter]; cancel: [] }>()

const { t } = useI18n()

const FIELDS = {
  TestPrinter: TestPrinterFields,
  EpsonTmT20ivNetworkPrinter: NetworkPrinterFields,
}

const name = ref(props.printer?.name ?? '')
const pending = ref<SavePrinter | null>(null)

function save(): void {
  if (pending.value !== null) {
    emit('save', pending.value)
  }
}
</script>

<template>
  <v-form class="printer-form" @submit.prevent="save">
    <v-card-text>
      <v-text-field
        v-model="name"
        class="name-field mb-4"
        :label="t('admin.printers.nameLabel')"
      />
      <component
        :is="FIELDS[props.printerType]"
        :printer="props.printer"
        :name="name"
        @change="(value: SavePrinter) => (pending = value)"
      />
    </v-card-text>
    <v-card-actions>
      <v-btn type="submit" color="primary" :disabled="name.trim().length === 0">
        {{ t('admin.save') }}
      </v-btn>
      <v-btn v-if="props.isCancellable" class="cancel" variant="text" @click="emit('cancel')">
        {{ t('admin.cancel') }}
      </v-btn>
    </v-card-actions>
  </v-form>
</template>
