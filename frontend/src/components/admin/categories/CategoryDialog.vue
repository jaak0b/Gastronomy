<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminCategory } from '../../../core/apiTypes'
import type { AdminCategoryDraft } from '../../../stores/admin/categories'

const COLOUR_OF_A_NEW_CATEGORY = '#607D8B'

const props = defineProps<{ category: AdminCategory | null; errorText: string | null }>()
const emit = defineEmits<{ save: [draft: AdminCategoryDraft]; cancel: [] }>()

const { t } = useI18n()
const name = ref(props.category?.name ?? '')
const colourHex = ref(props.category?.colourHex ?? COLOUR_OF_A_NEW_CATEGORY)

function save(): void {
  emit('save', { name: name.value.trim(), colourHex: colourHex.value.toUpperCase() })
}
</script>

<template>
  <v-dialog :model-value="true" max-width="480" persistent>
    <v-card class="category-dialog" role="dialog" aria-modal="true">
      <v-card-title class="category-dialog-title">
        {{ category === null ? t('admin.categories.new') : t('admin.categories.edit') }}
      </v-card-title>
      <v-form @submit.prevent="save">
        <v-card-text>
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
          <v-alert v-if="errorText !== null" class="refusal mt-4" type="warning" variant="tonal">
            {{ errorText }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn class="cancel-category" variant="text" @click="emit('cancel')">
            {{ t('admin.cancel') }}
          </v-btn>
          <v-btn
            class="save-category"
            type="submit"
            color="primary"
            :disabled="name.trim().length === 0"
          >
            {{ t('admin.save') }}
          </v-btn>
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
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
