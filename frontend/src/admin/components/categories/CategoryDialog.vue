<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { AdminCategoryView } from '../../../shared/api/generatedSchemas'
import type { AdminCategoryDraft } from '../../stores/categories'
import BaseFormDialog from '../BaseFormDialog.vue'

const COLOUR_OF_A_NEW_CATEGORY = '#607D8B'

const props = defineProps<{ category: AdminCategoryView | null; errorText: string | null }>()
const emit = defineEmits<{ save: [draft: AdminCategoryDraft]; cancel: [] }>()

const { t } = useI18n()
const name = ref(props.category?.name ?? '')
const colourHex = ref(props.category?.colourHex ?? COLOUR_OF_A_NEW_CATEGORY)

function save(): void {
  emit('save', { name: name.value.trim(), colourHex: colourHex.value.toUpperCase() })
}
</script>

<template>
  <BaseFormDialog
    :title="category === null ? t('admin.categories.new') : t('admin.categories.edit')"
    :error-text="errorText"
    :save-disabled="name.trim().length === 0"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="category-name-field mb-4"
      maxlength="200"
      :label="t('admin.categories.name')"
    />
    <label class="category-colour-label d-flex align-center ga-3">
      <span>{{ t('admin.categories.colour') }}</span>
      <input v-model="colourHex" class="category-colour-field" type="color" />
    </label>
  </BaseFormDialog>
</template>

<style scoped>
.category-colour-field {
  width: 64px;
  height: 40px;
  border: none;
  background: none;
  cursor: pointer;
}
</style>
