<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AdminItemDraft } from '../../../stores/admin/items'
import ItemForm from './ItemForm.vue'

defineProps<{ errorText: string | null }>()
const emit = defineEmits<{ save: [item: AdminItemDraft]; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog :model-value="true" max-width="560" persistent scrollable>
    <v-card class="new-item-dialog" role="dialog" aria-modal="true">
      <v-card-title class="new-item-title">{{ t('admin.items.new') }}</v-card-title>
      <ItemForm
        :item="null"
        :error-text="errorText"
        is-cancellable
        @save="(item: AdminItemDraft) => emit('save', item)"
        @cancel="emit('cancel')"
      />
    </v-card>
  </v-dialog>
</template>
