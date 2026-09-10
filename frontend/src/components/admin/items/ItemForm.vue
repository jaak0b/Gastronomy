<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  formatProductionMinutes,
  parseProductionMinutes,
} from '../../../core/productionMinutes'
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

const { t } = useI18n()
const categories = useAdminCategoriesStore()
const name = ref(props.item?.name ?? '')
const categoryId = ref<string | null>(props.item?.categoryId ?? null)
const categoryIsMissing = ref(false)
const isCreatingCategory = ref(false)
const productionMinutesText = ref(formatProductionMinutes(props.item?.productionMinutes ?? null))
const productionMinutesAreUnreadable = ref(false)
const sortOrder = ref(props.item?.sortOrder ?? 1)

watch(categoryId, () => {
  categoryIsMissing.value = false
})

const offeredCategories = computed(() =>
  categories.categories.filter(
    (category) => category.isActive || category.categoryId === props.item?.categoryId,
  ),
)

const categoryRefusal = useRefusalText([() => categories.errorMessage])

function startCreatingCategory(): void {
  categories.forgetError()
  isCreatingCategory.value = true
}

function stopCreatingCategory(): void {
  categories.forgetError()
  isCreatingCategory.value = false
}

async function createCategory(draft: AdminCategoryDraft): Promise<void> {
  const created = await categories.create(draft)
  if (created === null) {
    return
  }
  categoryId.value = created.categoryId
  isCreatingCategory.value = false
}

function save(): void {
  const minutes = parseProductionMinutes(productionMinutesText.value)
  productionMinutesAreUnreadable.value = minutes.kind === 'invalid'
  const chosenCategoryId = categoryId.value
  categoryIsMissing.value = chosenCategoryId === null
  if (minutes.kind === 'invalid' || chosenCategoryId === null) {
    return
  }
  emit('save', {
    itemId: props.item?.itemId,
    name: name.value,
    categoryId: chosenCategoryId,
    sortOrder: sortOrder.value,
    productionMinutes: minutes.minutes,
  })
}
</script>

<template>
  <v-card class="item-form mt-4">
    <v-form @submit.prevent="save">
      <v-card-text>
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
        <v-text-field
          v-model="productionMinutesText"
          class="production-minutes-field mb-2"
          :label="t('admin.items.productionMinutes')"
          inputmode="numeric"
          :error="productionMinutesAreUnreadable"
          :error-messages="
            productionMinutesAreUnreadable ? [t('admin.items.productionMinutesInvalid')] : []
          "
        />
        <v-alert v-if="errorText !== null" class="error" type="error" variant="tonal">
          {{ errorText }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-btn
          class="save-item"
          type="submit"
          color="primary"
          :disabled="name.trim().length === 0"
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
      :error-text="categoryRefusal"
      @save="createCategory"
      @cancel="stopCreatingCategory"
    />
  </v-card>
</template>
