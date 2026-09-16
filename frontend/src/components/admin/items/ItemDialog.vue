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
import FormDialog from '../FormDialog.vue'
import { useRefusalText } from '../refusalText'

const props = defineProps<{
  item: AdminItem | null
  errorText: string | null
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
  if (nameIsMissing.value) {
    return
  }
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
  save()
}
</script>

<template>
  <FormDialog
    :title="item === null ? t('admin.items.new') : t('admin.items.edit')"
    :error-text="errorText"
    :save-disabled="nameIsMissing"
    @save="save"
    @cancel="emit('cancel')"
  >
    <v-text-field
      v-model="name"
      class="item-name-field mb-4"
      maxlength="200"
      :label="t('admin.items.title')"
    />
    <div class="category-line d-flex align-center ga-2 mb-4">
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
    <v-number-input
      v-model="productionMinutes"
      class="production-minutes-field mb-2"
      :label="t('admin.items.productionMinutes')"
      :min="0"
      :max="LONGEST_PRODUCTION_MINUTES"
      :precision="1"
      :min-fraction-digits="0"
      :decimal-separator="decimalSeparator"
      control-variant="hidden"
      @keydown.enter.prevent="commitThePreparationTime"
    />
    <v-checkbox
      v-model="isQueueIndependent"
      class="queue-independent-checkbox"
      :label="t('admin.items.prepareIndependently')"
    />
  </FormDialog>
  <CategoryDialog
    v-if="isCreatingCategory"
    :category="null"
    :error-text="categoryRefusalText"
    @save="createCategory"
    @cancel="stopCreatingCategory"
  />
</template>
