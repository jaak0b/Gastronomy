<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminStaffMember } from '../../../stores/admin/staff'
import FormDialog from '../FormDialog.vue'

const props = defineProps<{ staffMember: AdminStaffMember; errorText: string | null }>()
const emit = defineEmits<{ save: [name: string]; cancel: [] }>()

const { t } = useI18n()
const name = ref(props.staffMember.name)

const nameIsMissing = computed(() => name.value.trim().length === 0)

function save(): void {
  if (nameIsMissing.value) {
    return
  }
  emit('save', name.value.trim())
}
</script>

<template>
  <FormDialog
    :title="t('admin.staff.rename')"
    :error-text="errorText"
    :save-disabled="nameIsMissing"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="staff-name-field"
      maxlength="40"
      :label="t('admin.staff.name')"
    />
  </FormDialog>
</template>
