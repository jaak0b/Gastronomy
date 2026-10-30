<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminUnitView } from '../../../shared/api/generatedSchemas'
import type { UnitDraft } from '../../stores/units'
import BaseFormDialog from '../../../shared/components/BaseFormDialog.vue'

const props = defineProps<{ unit: Pick<AdminUnitView, 'name'> | null; errorText: string | null }>()
const emit = defineEmits<{ save: [draft: UnitDraft]; cancel: [] }>()

const { t } = useI18n()
const name = ref(props.unit?.name ?? '')

const title = computed(() =>
  props.unit === null
    ? t('admin.units.actions.new')
    : t('admin.units.labels.editTitle'),
)

const isIncomplete = computed(() => name.value.trim().length === 0)

function save(): void {
  if (isIncomplete.value) {
    return
  }
  emit('save', { name: name.value })
}
</script>

<template>
  <BaseFormDialog
    :title="title"
    :error-text="errorText"
    :save-disabled="isIncomplete"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="unit-name-field"
      data-test="unit-name-field"
      maxlength="200"
      :label="t('admin.units.labels.name')"
    />
  </BaseFormDialog>
</template>
