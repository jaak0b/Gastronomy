<script setup lang="ts">
import { computed, ref, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminErrorMessage } from '../../core/adminErrorMessage'
import type { AdminItem } from '../../../shared/api/apiTypes'
import { assertNever } from '../../../shared/core/assertNever'
import { groupByCategorySortingItemsByName } from '../../../shared/core/grouping'
import { letteringColourOn } from '../../../shared/core/letteringColour'
import { useAdminCategoriesStore } from '../../stores/categories'
import { useAdminItemsStore, type AdminItemDraft } from '../../stores/items'
import { useAdminStationsStore } from '../../stores/stations'
import { useFestivalItemRows } from '../../composables/useFestivalItemRows'
import { useRefusalText } from '../../composables/useRefusalText'
import BaseConfirmDialog from '../BaseConfirmDialog.vue'
import ItemDialog from '../items/ItemDialog.vue'
import FestivalPlacementDialog from './FestivalPlacementDialog.vue'
import StationSelect from './StationSelect.vue'

const props = defineProps<{ festivalId: string; isRunning: boolean }>()

const { t } = useI18n()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const stations = useAdminStationsStore()
const chosenItemId = ref<string | null>(null)
const itemToPlace = ref<AdminItem | null>(null)
const isCreating = ref(false)
const removedItem = ref<AdminItem | null>(null)
const createRefusal = ref<AdminErrorMessage | null>(null)

const createRefusalText = useRefusalText(createRefusal)

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
  stations.stations.filter((station) => station.isAtTheFestival && station.isActive),
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

const tintedItemIds = computed(() => {
  const tinted = new Set<string>()
  let position = 0
  for (const group of groups.value) {
    for (const item of group.items) {
      if (position % 2 === 1) {
        tinted.add(item.itemId)
      }
      position += 1
    }
  }
  return tinted
})

const stillToAdd = computed(() =>
  items.items
    .filter((item) => item.isActive && item.atTheFestival === null)
    .sort((left, right) => left.name.localeCompare(right.name)),
)

const chosenItem = computed(
  () => items.items.find((item) => item.itemId === chosenItemId.value) ?? null,
)

function startPlacing(item: AdminItem): void {
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

function startCreating(): void {
  createRefusal.value = null
  isCreating.value = true
}

function stopCreating(): void {
  createRefusal.value = null
  isCreating.value = false
}

async function create(draft: AdminItemDraft): Promise<void> {
  createRefusal.value = null
  const created = await items.create(draft)
  switch (created.kind) {
    case 'ok':
      isCreating.value = false
      startPlacing(created.value)
      return
    case 'failed':
      createRefusal.value = created.message
      return
    default:
      assertNever(created)
  }
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
  <section class="festival-items mb-4">
    <v-card variant="outlined">
      <div class="pa-4">
        <h2 class="section-heading text-h6 mb-3">{{ t('admin.items.title') }}</h2>

        <v-alert
          v-if="items.loadFailed || categories.loadFailed"
          class="error mb-3"
          type="error"
          variant="tonal"
        >
          {{ t('admin.loadFailed') }}
        </v-alert>

        <div v-if="festivalStations.length === 0" class="festival-item-placeholder">
          <div class="item-line d-flex align-center flex-wrap ga-3 py-2 px-3">
            <span class="needs-a-station text-body-1">
              {{ t('admin.festival.needsAStationFirst') }}
            </span>
          </div>
        </div>

        <template v-else>
          <section
            v-for="group in groups"
            :key="group.category.categoryId"
            class="category-section"
          >
            <h3
              class="category-name text-subtitle-1 font-weight-bold px-3 py-1 rounded d-inline-block mb-1 mt-3"
              :style="{
                backgroundColor: group.category.colourHex,
                color: letteringColourOn(group.category.colourHex),
              }"
            >
              {{ group.category.name }}
            </h3>
            <div
              v-for="item in group.items"
              :key="item.itemId"
              class="festival-item-row"
              :class="{ 'tinted-row': tintedItemIds.has(item.itemId) }"
            >
              <div class="item-line d-flex align-center flex-wrap ga-3 py-2 px-3">
                <span class="name text-body-1">{{ item.name }}</span>
                <v-chip v-if="!item.isActive" class="deactivated" size="small" color="grey">
                  {{ t('admin.deactivated') }}
                </v-chip>
                <v-text-field
                  class="price-field"
                  density="compact"
                  inputmode="decimal"
                  hide-details="auto"
                  :model-value="rowShownFor(item.itemId).price.edited"
                  :label="t('admin.items.price')"
                  :error="priceIsUnreadable(item.itemId)"
                  :error-messages="
                    priceIsUnreadable(item.itemId) ? [t('admin.items.priceInvalid')] : []
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
                  density="compact"
                  color="primary"
                  hide-details
                  :model-value="!item.atTheFestival.isAvailable"
                  :label="t('admin.items.soldOut')"
                  @update:model-value="(value: boolean | null) => setSoldOut(item, value === true)"
                />
                <span class="remove-item-wrapper">
                  <v-btn
                    class="remove-item"
                    variant="text"
                    :disabled="isRunning"
                    @click="removedItem = item"
                  >
                    {{ t('admin.festival.remove') }}
                  </v-btn>
                  <v-tooltip activator="parent" location="top" :disabled="!isRunning">
                    {{ t('admin.itemStaysOnTheMenuWhileTheFestivalRuns') }}
                  </v-tooltip>
                </span>
              </div>
              <v-alert
                v-if="rowRefusalText(item.itemId) !== null"
                class="refusal mb-2"
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
              :items="stillToAdd"
              item-title="name"
              item-value="itemId"
              density="compact"
              hide-details
              :label="t('admin.festival.itemName')"
              :no-data-text="t('admin.festival.noItemsToAdd')"
            />
            <v-btn
              class="add-item"
              color="primary"
              variant="tonal"
              :disabled="chosenItemId === null"
              @click="add"
            >
              {{ t('admin.festival.addItem') }}
            </v-btn>
            <v-btn class="new-item" variant="text" @click="startCreating">
              {{ t('admin.items.new') }}
            </v-btn>
          </div>
          <v-alert
            v-if="createRefusalText !== null && !isCreating"
            class="refusal mt-3"
            type="warning"
            variant="tonal"
          >
            {{ createRefusalText }}
          </v-alert>
        </template>
      </div>
    </v-card>

    <ItemDialog
      v-if="isCreating"
      :item="null"
      :error-text="createRefusalText"
      @save="create"
      @cancel="stopCreating"
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
      :title="t('admin.festival.removeItemTitle')"
      :body="t('admin.festival.removeItemBody')"
      :confirm-label="t('admin.festival.remove')"
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
