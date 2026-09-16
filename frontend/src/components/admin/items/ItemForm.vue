<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminErrorMessage } from '../../../core/adminErrorMessage'
import { assertNever } from '../../../core/assertNever'
import { LONGEST_PRODUCTION_MINUTES } from '../../../core/productionMinutes'
import {
  useAdminCategoriesStore,
  type AdminCategoryDraft,
} from '../../../stores/admin/categories'
import type { AdminItem, AdminItemDraft } from '../../../stores/admin/items'
import CategoryDialog from '../categories/CategoryDialog.vue'
import { useRefusalText } from '../refusalText'

const props = defineProps<{
  item: AdminItem | null
  errorText: string | null
  isCancellable?: boolean
}>()
const emit = defineEmits<{ save: [item: AdminItemDraft]; cancel: [] }>()

const { t, locale } = useI18n()
const categories = useAdminCategoriesStore()
const name = ref(props.item?.name ?? '')
const categoryId = ref<string | null>(props.item?.categoryId ?? null)
const categoryIsMissing = ref(false)
const isCreatingCategory = ref(false)
const productionMinutes = ref<number | null>(props.item?.productionMinutes ?? null)
const isQueueIndependent = ref(props.item?.isQueueIndependent ?? false)
const sortOrder = ref(props.item?.sortOrder ?? 1)

watch(categoryId, () => {
  categoryIsMissing.value = false
})

const offeredCategories = computed(() =>
  categories.categories.filter(
    (category) => category.isActive || category.categoryId === props.item?.categoryId,
  ),
)

const decimalSeparator = computed(() => (locale.value === 'de' ? ',' : '.'))

const nameIsMissing = computed(() => name.value.trim().length === 0)

const categoryRefusal = ref<AdminErrorMessage | null>(null)
const categoryRefusalText = useRefusalText(categoryRefusal)

function startCreatingCategory(): void {
  categoryRefusal.value = null
  isCreatingCategory.value = true
}

function stopCreatingCategory(): void {
  categoryRefusal.value = null
  isCreatingCategory.value = false
}

async function createCategory(draft: AdminCategoryDraft): Promise<void> {
  categoryRefusal.value = null
  const created = await categories.create(draft)
  switch (created.kind) {
    case 'ok':
      categoryId.value = created.value.categoryId
      isCreatingCategory.value = false
      return
    case 'failed':
      categoryRefusal.value = created.message
      return
    default:
      assertNever(created)
  }
}

function save(): void {
  const chosenCategoryId = categoryId.value
  categoryIsMissing.value = chosenCategoryId === null
  if (chosenCategoryId === null) {
    return
  }
  emit('save', {
    itemId: props.item?.itemId,
    name: name.value,
    categoryId: chosenCategoryId,
    sortOrder: sortOrder.value,
    productionMinutes: productionMinutes.value,
    isQueueIndependent: isQueueIndependent.value,
  })
}

function commitThePreparationTime(event: KeyboardEvent): void {
  const field = event.target as HTMLElement
  field.blur()
  if (nameIsMissing.value) {
    return
  }
  save()
}
</script>

<template>
  <div class="item-form mt-4">
    <v-form @submit.prevent="save">
      <v-card-text>
        <v-row align="center">
          <v-col cols="12" md="8">
            <v-text-field
              v-model="name"
              class="item-name-field"
              maxlength="200"
              :label="t('admin.items.title')"
            />
          </v-col>
          <v-col cols="12" md="4">
            <div class="category-line d-flex align-center ga-2">
              <v-select
                v-model="categoryId"
                class="category-field flex-grow-1"
                :label="t('admin.items.category')"
                :items="offeredCategories"
                item-title="name"
                item-value="categoryId"
                :no-data-text="t('admin.categories.noneYet')"
                :error="categoryIsMissing"
                :error-messages="categoryIsMissing ? [t('admin.itemCategoryUnknown')] : []"
              />
              <v-btn class="new-category" variant="text" @click="startCreatingCategory">
                {{ t('admin.categories.new') }}
              </v-btn>
            </div>
          </v-col>
        </v-row>
        <v-row align="center">
          <v-col cols="12" md="4">
            <v-number-input
              v-model="productionMinutes"
              class="production-minutes-field"
              :label="t('admin.items.productionMinutes')"
              :min="0"
              :max="LONGEST_PRODUCTION_MINUTES"
              :precision="1"
              :min-fraction-digits="0"
              :decimal-separator="decimalSeparator"
              control-variant="hidden"
              @keydown.enter.prevent="commitThePreparationTime"
            />
          </v-col>
          <v-col cols="12" md="8">
            <v-checkbox
              v-model="isQueueIndependent"
              class="queue-independent-checkbox"
              :label="t('admin.items.prepareIndependently')"
            />
          </v-col>
        </v-row>
        <v-alert v-if="errorText !== null" class="error mt-2" type="error" variant="tonal">
          {{ errorText }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-btn
          class="save-item"
          type="submit"
          color="primary"
          :disabled="nameIsMissing"
        >
          {{ t('admin.save') }}
        </v-btn>
        <v-btn v-if="props.isCancellable" class="cancel" variant="text" @click="emit('cancel')">
          {{ t('admin.cancel') }}
        </v-btn>
      </v-card-actions>
    </v-form>
    <CategoryDialog
      v-if="isCreatingCategory"
      :category="null"
      :error-text="categoryRefusalText"
      @save="createCategory"
      @cancel="stopCreatingCategory"
    />
  </div>
</template>
