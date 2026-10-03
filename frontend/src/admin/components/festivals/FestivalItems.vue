<script setup lang="ts">
import { computed, ref, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { AdminCategoryView, AdminItemView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import { groupByCategorySortingItemsByName } from '../../../shared/core/grouping'
import { letteringColourOn } from '../../../shared/core/letteringColour'
import { idsOfEverySecondRow } from '../../core/alternatingRows'
import { useAdminCategoriesStore, type AdminCategoryDraft } from '../../stores/categories'
import { useAdminItemsStore, type AdminItemDraft } from '../../stores/items'
import { useAdminStationsStore } from '../../stores/stations'
import { useEditSession } from '../../composables/useEditSession'
import { useFestivalItemRows } from '../../composables/useFestivalItemRows'
import BaseConfirmDialog from '../../../shared/components/BaseConfirmDialog.vue'
import CategoryDialog from '../categories/CategoryDialog.vue'
import ItemDialog from '../items/ItemDialog.vue'
import FestivalPlacementDialog from './FestivalPlacementDialog.vue'
import StationSelect from './StationSelect.vue'

const props = defineProps<{ festivalId: string; isRunning: boolean }>()

const { t } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const stations = useAdminStationsStore()
const chosenItemId = ref<string | null>(null)
const itemToPlace = ref<AdminItemView | null>(null)
const removedItem = ref<AdminItemView | null>(null)
const itemSession = useEditSession<AdminItemView, AdminItemDraft, AdminItemView | null>({
  saveThrough: (draft, item) => (item === null ? items.create(draft) : items.save(draft)),
  afterSaving: (saved, mode) => {
    switch (mode) {
      case 'create':
        if (saved === null) {
          throw new Error('The laptop accepted a new item without returning it.')
        }
        startPlacing(saved)
        return
      case 'edit':
        return
      default:
        assertNever(mode)
    }
  },
})
const categorySession = useEditSession<AdminCategoryView, AdminCategoryDraft, unknown>({
  saveThrough: (draft, category) =>
    category === null
      ? categories.create(draft)
      : categories.save({ categoryId: category.categoryId, ...draft }),
})
const {
  isOpen: isItemDialogOpen,
  edited: editedItem,
  refusalText: itemRefusalText,
} = itemSession
const {
  isOpen: isCategoryDialogOpen,
  edited: editedCategory,
  refusalText: categoryRefusalText,
} = categorySession

const {
  itemsAtTheFestival,
  rowShownFor,
  rowRefusalText,
  priceIsUnreadable,
  typePrice,
  save,
  changeStations,
  setSoldOut,
  removeFromTheFestival,
} = useFestivalItemRows(toRef(props, 'festivalId'))

const festivalStations = computed(() =>
  stations.stations.filter((station) => station.isAtAnyFestival && station.isActive),
)

const groups = computed(() =>
  groupByCategorySortingItemsByName(
    categories.categories,
    itemsAtTheFestival.value,
    (category) => category.categoryId,
    (item) => item.categoryId,
    (item) => item.name,
  ).filter((group) => group.items.length > 0),
)

const tintedItemIds = computed(() => idsOfEverySecondRow(groups.value, (item) => item.itemId))

const stillToAdd = computed(() =>
  items.items
    .filter((item) => item.isActive && item.atTheFestival === null)
    .sort((left, right) => left.name.localeCompare(right.name)),
)

const chosenItem = computed(
  () => items.items.find((item) => item.itemId === chosenItemId.value) ?? null,
)

function startPlacing(item: AdminItemView): void {
  itemToPlace.value = item
}

function stopPlacing(): void {
  itemToPlace.value = null
}

function add(): void {
  const item = chosenItem.value
  if (item === null) {
    return
  }
  startPlacing(item)
  chosenItemId.value = null
}

async function remove(): Promise<void> {
  const item = removedItem.value
  removedItem.value = null
  if (item === null) {
    return
  }
  await removeFromTheFestival(item)
}
</script>

<template>
  <section class="festival-items mb-4" data-test="festival-items">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3" data-test="section-heading">{{ t('admin.items.title') }}</h2>

        <v-alert
          v-if="items.loadFailed || categories.loadFailed"
          class="error mb-3"
          data-test="load-failed"
          type="error"
          variant="tonal"
        >
          {{ t('admin.common.errors.loadFailed') }}
        </v-alert>

        <div v-if="festivalStations.length === 0" class="festival-item-placeholder" data-test="festival-item-placeholder">
          <div class="item-line d-flex align-center flex-wrap ga-3 py-2 px-3">
            <span class="needs-a-station text-body-1" data-test="needs-a-station">
              {{ t('admin.festivals.messages.needsAStationFirst') }}
            </span>
          </div>
        </div>

        <template v-else>
          <section
            v-for="group in groups"
            :key="group.category.categoryId"
            class="category-section"
          >
            <div class="category-heading d-flex align-center flex-wrap ga-2 mb-1 mt-3">
              <h3
                class="category-name text-subtitle-1 font-weight-bold px-3 py-1 rounded"
                data-test="category-name"
                :style="{
                  backgroundColor: group.category.colourHex,
                  color: letteringColourOn(group.category.colourHex),
                }"
              >
                {{ group.category.name }}
              </h3>
              <v-btn
                class="edit-category"
                data-test="edit-category"
                variant="text"
                @click="categorySession.openForEdit(group.category)"
              >
                {{ t('admin.common.actions.edit') }}
              </v-btn>
            </div>
            <div
              v-for="item in group.items"
              :key="item.itemId"
              class="festival-item-row"
              data-test="festival-item-row"
              :data-test-id="item.itemId"
              :class="{ 'tinted-row': tintedItemIds.has(item.itemId) }"
            >
              <div class="item-line d-flex align-center flex-wrap ga-3 py-2 px-3">
                <span
                  class="name text-body-1"
                  data-test="item-name"
                >
                  {{ item.name }}
                </span>
                <v-chip v-if="!item.isActive" class="deactivated" size="small" color="grey">
                  {{ t('admin.common.labels.deactivated') }}
                </v-chip>
                <v-text-field
                  class="price-field"
                  data-test="price-field"
                  density="compact"
                  inputmode="decimal"
                  hide-details="auto"
                  :model-value="rowShownFor(item.itemId).price.edited"
                  :label="t('admin.items.labels.price')"
                  :error="priceIsUnreadable(item.itemId)"
                  :error-messages="
                    priceIsUnreadable(item.itemId) ? [t('admin.items.errors.priceInvalid')] : []
                  "
                  @update:model-value="(typed: string) => typePrice(item.itemId, typed)"
                  @blur="save(item.itemId)"
                  @keyup.enter="save(item.itemId)"
                />
                <StationSelect
                  :stations="festivalStations"
                  :selected-station-ids="rowShownFor(item.itemId).stations.edited"
                  :error-text="null"
                  @select="(stationIds: string[]) => changeStations(item.itemId, stationIds)"
                />
                <v-spacer />
                <v-switch
                  :key="`${item.itemId}-${item.atTheFestival.isAvailable}-${rowShownFor(item.itemId).soldOutSwitchRenderKey}`"
                  class="sold-out-switch flex-grow-0"
                  data-test="sold-out-switch"
                  density="compact"
                  color="primary"
                  hide-details
                  :model-value="!item.atTheFestival.isAvailable"
                  :label="t('common.labels.soldOut')"
                  @update:model-value="(value: boolean | null) => setSoldOut(item, value === true)"
                />
                <v-btn class="edit-item" data-test="edit-item" variant="text" @click="itemSession.openForEdit(item)">
                  {{ t('admin.common.actions.edit') }}
                </v-btn>
                <span class="remove-item-wrapper" data-test="remove-item-wrapper">
                  <v-btn
                    class="remove-item"
                    data-test="remove-item"
                    icon="mdi-delete"
                    variant="text"
                    color="error"
                    :disabled="isRunning"
                    @click="removedItem = item"
                  />
                  <v-tooltip
                    data-test="remove-item-tooltip"
                    activator="parent"
                    location="top"
                    :disabled="!isRunning"
                  >
                    {{ t('errors.admin.festivals.itemStaysOnTheMenuWhileTheFestivalRuns') }}
                  </v-tooltip>
                </span>
              </div>
              <v-alert
                v-if="rowRefusalText(item.itemId) !== null"
                class="refusal mb-2"
                data-test="refusal"
                type="warning"
                variant="tonal"
              >
                {{ rowRefusalText(item.itemId) }}
              </v-alert>
            </div>
          </section>

          <div class="add-item-line d-flex align-center flex-wrap ga-3 mt-6">
            <v-autocomplete
              v-model="chosenItemId"
              class="item-search flex-grow-1"
              data-test="item-search"
              :items="stillToAdd"
              item-title="name"
              item-value="itemId"
              density="compact"
              hide-details
              :label="t('admin.festivals.labels.itemName')"
              :no-data-text="t('admin.festivals.messages.noItemsToAdd')"
            />
            <v-btn
              class="add-item"
              data-test="add-item"
              color="primary"
              variant="tonal"
              :disabled="chosenItemId === null"
              @click="add"
            >
              {{ t('admin.festivals.actions.addItem') }}
            </v-btn>
            <v-btn class="new-item" data-test="new-item" variant="text" @click="itemSession.openForCreate()">
              {{ t('admin.items.actions.new') }}
            </v-btn>
          </div>
          <v-alert
            v-if="itemRefusalText !== null && !isItemDialogOpen"
            class="refusal mt-3"
            data-test="refusal"
            type="warning"
            variant="tonal"
          >
            {{ itemRefusalText }}
          </v-alert>
        </template>
      </div>
    </v-card>

    <CategoryDialog
      v-if="isCategoryDialogOpen"
      :category="editedCategory"
      :error-text="categoryRefusalText"
      @save="categorySession.save"
      @cancel="categorySession.close"
    />

    <ItemDialog
      v-if="isItemDialogOpen"
      :item="editedItem"
      :error-text="itemRefusalText"
      @save="itemSession.save"
      @cancel="itemSession.close"
    />

    <FestivalPlacementDialog
      v-if="itemToPlace !== null"
      :festival-id="festivalId"
      :item="itemToPlace"
      :stations="festivalStations"
      @placed="stopPlacing"
      @cancel="stopPlacing"
    />

    <BaseConfirmDialog
      v-if="removedItem !== null"
      :title="t('admin.festivals.labels.removeItemTitle')"
      :body="t('admin.festivals.messages.removeItemBody')"
      :confirm-label="t('admin.festivals.actions.remove')"
      @confirm="remove"
      @cancel="removedItem = null"
    />
  </section>
</template>

<style scoped>
.festival-item-row + .festival-item-row {
  border-top: 1px solid rgb(var(--v-border-color), var(--v-border-opacity));
}

.festival-item-row.tinted-row {
  background-color: rgba(var(--v-theme-on-surface), 0.08);
  border-radius: 6px;
}

.festival-item-row .name {
  flex: 0 1 12rem;
}

.festival-item-row .price-field {
  flex: 0 0 9rem;
}

.festival-item-row .station-select-field {
  flex: 0 1 auto;
}

.remove-item-wrapper {
  display: inline-flex;
}
</style>
