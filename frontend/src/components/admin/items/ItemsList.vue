<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminCategory, AppLanguage } from '../../../core/apiTypes'
import { groupByCategory } from '../../../core/grouping'
import { letteringColourOn } from '../../../core/letteringColour'
import { formatPrice } from '../../../core/totals'
import {
  useAdminCategoriesStore,
  type AdminCategoryDraft,
} from '../../../stores/admin/categories'
import { useAdminFestivalsStore } from '../../../stores/admin/festivals'
import {
  useAdminItemsStore,
  type AdminItem,
  type AdminItemDraft,
  type MenuPlacement,
} from '../../../stores/admin/items'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import CategoryDialog from '../categories/CategoryDialog.vue'
import ConfirmDialog from '../ConfirmDialog.vue'
import { useRefusalText } from '../refusalText'
import ItemForm from './ItemForm.vue'
import MenuItemDialog from './MenuItemDialog.vue'
import NewItemDialog from './NewItemDialog.vue'
import StationChips from './StationChips.vue'

const { t, locale } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const stations = useAdminStationsStore()
const festivals = useAdminFestivalsStore()
const editingId = ref<string | null>(null)
const isCreating = ref(false)
const showsDeactivated = ref(false)
const askingAboutId = ref<string | null>(null)
const askingAboutCategoryId = ref<string | null>(null)
const renamedCategory = ref<AdminCategory | null>(null)
const isCreatingCategory = ref(false)
const placedItem = ref<AdminItem | null>(null)
let stopListening: (() => void) | null = null

const aFestivalIsPicked = computed(() => festivals.pickedFestivalId !== null)
const festivalStations = computed(() =>
  stations.stations.filter((station) => station.isAtTheFestival && station.isActive),
)

const shownItems = computed(() =>
  items.items.filter((item) => showsDeactivated.value || item.isActive),
)

const menuGroups = computed(() =>
  groupByCategory(
    categories.categories,
    shownItems.value.filter((item) => item.atTheFestival !== null),
    (category) => category.categoryId,
    (item) => item.categoryId,
    (item) => item.name,
  ),
)

const restOfTheItems = computed(() =>
  [...shownItems.value.filter((item) => item.atTheFestival === null)].sort((left, right) =>
    left.name.localeCompare(right.name),
  ),
)

const itemRefusal = useRefusalText([() => items.errorMessage])
const categoryRefusal = useRefusalText([() => categories.errorMessage])
const refusal = computed(() => itemRefusal.value ?? categoryRefusal.value)
const isCategoryDialogOpen = computed(
  () => isCreatingCategory.value || renamedCategory.value !== null,
)

function priceOf(item: AdminItem): string {
  return item.atTheFestival === null
    ? ''
    : formatPrice(item.atTheFestival.priceCents, locale.value as AppLanguage)
}

function stationIdsOf(item: AdminItem): string[] {
  return item.atTheFestival?.stationIds ?? []
}

async function save(item: AdminItemDraft): Promise<void> {
  const saved = await items.save(item)
  if (saved) {
    editingId.value = null
    isCreating.value = false
  }
}

function forgetRefusals(): void {
  items.forgetError()
  categories.forgetError()
}

function toggleEditing(itemId: string): void {
  editingId.value = editingId.value === itemId ? null : itemId
  forgetRefusals()
}

function startCreating(): void {
  isCreating.value = true
  forgetRefusals()
}

function stopCreating(): void {
  isCreating.value = false
  forgetRefusals()
}

function startPlacing(item: AdminItem): void {
  forgetRefusals()
  placedItem.value = item
}

function stopPlacing(): void {
  forgetRefusals()
  placedItem.value = null
}

async function putOnTheMenu(placement: MenuPlacement): Promise<void> {
  const item = placedItem.value
  if (item === null) {
    return
  }
  if (await items.putOnTheMenu(item.itemId, placement)) {
    placedItem.value = null
  }
}

async function toggleStation(item: AdminItem, stationId: string): Promise<void> {
  const chosen = stationIdsOf(item)
  const stationIds = chosen.includes(stationId)
    ? chosen.filter((id) => id !== stationId)
    : [...chosen, stationId]
  await items.putOnTheMenu(item.itemId, {
    priceCents: item.atTheFestival?.priceCents ?? 0,
    stationIds,
  })
}

async function deactivate(): Promise<void> {
  const itemId = askingAboutId.value
  askingAboutId.value = null
  if (itemId !== null) {
    await items.setActive(itemId, false)
  }
}

function startCreatingCategory(): void {
  forgetRefusals()
  renamedCategory.value = null
  isCreatingCategory.value = true
}

function startRenamingCategory(category: AdminCategory): void {
  forgetRefusals()
  isCreatingCategory.value = false
  renamedCategory.value = category
}

function stopEditingCategory(): void {
  forgetRefusals()
  isCreatingCategory.value = false
  renamedCategory.value = null
}

async function saveCategory(draft: AdminCategoryDraft): Promise<void> {
  const category = renamedCategory.value
  const saved =
    category === null
      ? (await categories.create(draft)) !== null
      : await categories.save({ categoryId: category.categoryId, ...draft })
  if (saved) {
    stopEditingCategory()
  }
}

async function deactivateCategory(): Promise<void> {
  const categoryId = askingAboutCategoryId.value
  askingAboutCategoryId.value = null
  if (categoryId !== null) {
    await categories.setActive(categoryId, false)
  }
}

onMounted(async () => {
  stopListening = categories.listen()
  await festivals.load()
  await categories.load()
  await items.load()
  await stations.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-items">
    <v-alert
      v-if="refusal !== null && editingId === null && !isCreating && !isCategoryDialogOpen"
      class="refusal mb-4"
      type="warning"
      variant="tonal"
    >
      {{ refusal }}
    </v-alert>
    <v-alert
      v-if="items.loadFailed || categories.loadFailed"
      class="error"
      type="error"
      variant="tonal"
    >
      {{ t('admin.loadFailed') }}
    </v-alert>
    <v-alert
      v-if="!aFestivalIsPicked"
      class="no-festival-picked mb-4"
      type="info"
      variant="tonal"
    >
      {{ t('admin.items.noFestivalPicked') }}
    </v-alert>

    <v-checkbox
      v-model="showsDeactivated"
      class="show-deactivated"
      :label="t('admin.showDeactivated')"
    />

    <template v-if="aFestivalIsPicked">
      <h2 class="on-the-menu-heading text-h6 mb-2">{{ t('admin.items.onTheMenu') }}</h2>
      <section
        v-for="group in menuGroups"
        :key="group.category.categoryId"
        class="category-section"
      >
        <div class="category-heading d-flex align-center ga-2 py-2">
          <h3
            class="category-name text-subtitle-1 font-weight-bold px-3 py-1 rounded"
            :style="{
              backgroundColor: group.category.colourHex,
              color: letteringColourOn(group.category.colourHex),
            }"
          >
            {{ group.category.name }}
          </h3>
          <v-chip v-if="!group.category.isActive" class="deactivated" size="small" color="grey">
            {{ t('admin.deactivated') }}
          </v-chip>
          <v-spacer />
          <v-btn
            class="rename-category"
            icon="mdi-pencil"
            variant="text"
            :aria-label="t('admin.categories.edit')"
            @click="startRenamingCategory(group.category)"
          />
          <v-btn
            class="move-category-up"
            icon="mdi-arrow-up"
            variant="text"
            :aria-label="t('admin.categories.moveUp')"
            @click="categories.move(group.category.categoryId, 'up')"
          />
          <v-btn
            class="move-category-down"
            icon="mdi-arrow-down"
            variant="text"
            :aria-label="t('admin.categories.moveDown')"
            @click="categories.move(group.category.categoryId, 'down')"
          />
          <v-btn
            v-if="group.category.isActive"
            class="deactivate-category"
            icon="mdi-eye-off"
            variant="text"
            color="error"
            :aria-label="t('admin.categories.deactivate')"
            @click="askingAboutCategoryId = group.category.categoryId"
          />
          <v-btn
            v-else
            class="activate-category"
            variant="text"
            @click="categories.setActive(group.category.categoryId, true)"
          >
            {{ t('admin.categories.activate') }}
          </v-btn>
        </div>
        <v-card v-for="item in group.items" :key="item.itemId" class="item-row mb-2">
          <div class="item-line d-flex align-center flex-wrap ga-2 px-4 py-2">
            <span class="name text-body-1">{{ item.name }}</span>
            <v-btn class="price" variant="text" @click="startPlacing(item)">
              {{ priceOf(item) }}
            </v-btn>
            <v-chip v-if="!item.isActive" class="deactivated" size="small" color="grey">
              {{ t('admin.deactivated') }}
            </v-chip>
            <v-spacer />
            <v-btn
              v-if="item.isActive"
              class="sold-out-toggle"
              variant="text"
              @click="items.setAvailability(item.itemId, !item.atTheFestival?.isAvailable)"
            >
              {{
                item.atTheFestival?.isAvailable
                  ? t('admin.items.soldOut')
                  : t('admin.items.soldOutUndo')
              }}
            </v-btn>
            <v-btn class="edit" variant="text" @click="toggleEditing(item.itemId)">
              {{ t('admin.edit') }}
            </v-btn>
            <v-btn class="take-off-the-menu" variant="text" @click="items.takeOffTheMenu(item.itemId)">
              {{ t('admin.items.takeOffTheMenu') }}
            </v-btn>
            <v-btn
              v-if="item.isActive"
              class="deactivate"
              icon="mdi-delete"
              variant="text"
              color="error"
              :aria-label="t('admin.deactivate')"
              @click="askingAboutId = item.itemId"
            />
            <v-btn
              v-else
              class="reactivate"
              variant="text"
              @click="items.setActive(item.itemId, true)"
            >
              {{ t('admin.items.activate') }}
            </v-btn>
          </div>
          <div class="item-stations px-4 pb-3">
            <StationChips
              :stations="festivalStations"
              :selected-station-ids="stationIdsOf(item)"
              @toggle="(stationId: string) => toggleStation(item, stationId)"
            />
          </div>
          <v-expand-transition>
            <ItemForm
              v-if="editingId === item.itemId"
              :item="item"
              :error-text="itemRefusal"
              @save="save"
            />
          </v-expand-transition>
        </v-card>
      </section>

      <h2 class="not-on-the-menu-heading text-h6 mt-6 mb-2">
        {{ t('admin.items.notOnTheMenu') }}
      </h2>
    </template>

    <v-card v-for="item in restOfTheItems" :key="item.itemId" class="item-row rest mb-2">
      <div class="item-line d-flex align-center ga-2 px-4 py-2">
        <span class="name text-body-1">{{ item.name }}</span>
        <v-chip v-if="!item.isActive" class="deactivated" size="small" color="grey">
          {{ t('admin.deactivated') }}
        </v-chip>
        <v-spacer />
        <v-btn
          v-if="aFestivalIsPicked"
          class="put-on-the-menu"
          color="primary"
          variant="tonal"
          @click="startPlacing(item)"
        >
          {{ t('admin.items.putOnTheMenu') }}
        </v-btn>
        <v-btn class="edit" variant="text" @click="toggleEditing(item.itemId)">
          {{ t('admin.edit') }}
        </v-btn>
        <v-btn
          v-if="item.isActive"
          class="deactivate"
          icon="mdi-delete"
          variant="text"
          color="error"
          :aria-label="t('admin.deactivate')"
          @click="askingAboutId = item.itemId"
        />
        <v-btn
          v-else
          class="reactivate"
          variant="text"
          @click="items.setActive(item.itemId, true)"
        >
          {{ t('admin.items.activate') }}
        </v-btn>
      </div>
      <v-expand-transition>
        <ItemForm
          v-if="editingId === item.itemId"
          :item="item"
          :error-text="itemRefusal"
          @save="save"
        />
      </v-expand-transition>
    </v-card>

    <p class="categories-shared text-medium-emphasis mt-6 mb-2">
      {{ t('admin.categories.sharedAcrossFestivals') }}
    </p>
    <div class="d-flex ga-2">
      <v-btn class="new-item" color="primary" @click="startCreating">
        {{ t('admin.items.new') }}
      </v-btn>
      <v-btn class="new-category" color="primary" variant="tonal" @click="startCreatingCategory">
        {{ t('admin.categories.new') }}
      </v-btn>
    </div>

    <NewItemDialog
      v-if="isCreating"
      :error-text="itemRefusal"
      @save="save"
      @cancel="stopCreating"
    />
    <MenuItemDialog
      v-if="placedItem !== null"
      :key="placedItem.itemId"
      :item-name="placedItem.name"
      :stations="festivalStations"
      :price-cents="placedItem.atTheFestival?.priceCents ?? null"
      :station-ids="stationIdsOf(placedItem)"
      :error-text="itemRefusal"
      @confirm="putOnTheMenu"
      @cancel="stopPlacing"
    />
    <CategoryDialog
      v-if="isCategoryDialogOpen"
      :key="renamedCategory?.categoryId ?? 'new'"
      :category="renamedCategory"
      :error-text="categoryRefusal"
      @save="saveCategory"
      @cancel="stopEditingCategory"
    />
    <ConfirmDialog
      v-if="askingAboutId !== null"
      :title="t('admin.items.deactivateTitle')"
      :body="t('admin.items.deactivateBody')"
      :confirm-label="t('admin.items.deactivateConfirm')"
      @confirm="deactivate"
      @cancel="askingAboutId = null"
    />
    <ConfirmDialog
      v-if="askingAboutCategoryId !== null"
      :title="t('admin.categories.deactivateTitle')"
      :body="t('admin.categories.deactivateBody')"
      :confirm-label="t('admin.categories.deactivateConfirm')"
      @confirm="deactivateCategory"
      @cancel="askingAboutCategoryId = null"
    />
  </v-container>
</template>
