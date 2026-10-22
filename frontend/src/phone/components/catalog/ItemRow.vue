<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { CatalogItemView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { itemState } from '../../core/catalogItemState'
import { assertNever } from '../../../shared/core/assertNever'
import type { EstimateRange } from '../../core/estimates'
import { estimateRangeText } from '../../core/estimateWording'
import { formatPrice } from '../../../shared/core/money'
import { buildItemPositionsView, type ItemPosition, type PositionGroup } from '../../core/itemPositions'
import { needsStationChoice } from '../../core/routingPreview'
import ItemNoteDialog from './ItemNoteDialog.vue'

interface FactAfterThePrice {
  kind: 'estimate' | 'sold-out'
  text: string
}

const props = defineProps<{
  item: CatalogItemView
  positions: ItemPosition[]
  language: AppLanguage
  estimateRange: EstimateRange | null
  stationNameFor: (stationId: string) => string
}>()
const emit = defineEmits<{
  add: []
  addWithANote: [note: string]
  addWithANoteAtAStation: []
  addLikeGroup: [note: string | null, stationId: string | null]
  removeOne: [index: number]
  renameNote: [indexes: number[], note: string]
  changeStation: [indexes: number[]]
}>()

const { t } = useI18n()

const isAsking = ref(false)
const typedNote = ref('')
const groupBeingCorrected = ref<PositionGroup | null>(null)

const isSoldOut = computed(() => {
  const state = itemState(props.item)
  switch (state) {
    case 'available':
      return false
    case 'soldOut':
      return true
    default:
      return assertNever(state)
  }
})

const price = computed(() => formatPrice(props.item.priceCents, props.language))

const factAfterThePrice = computed<FactAfterThePrice | null>(() => {
  if (isSoldOut.value) {
    return { kind: 'sold-out', text: t('common.labels.soldOut') }
  }
  const waitingTime = estimateRangeText(props.estimateRange, t, props.language)
  return waitingTime === null ? null : { kind: 'estimate', text: waitingTime }
})

const view = computed(() => buildItemPositionsView(props.positions, props.item.priceCents))

const countTimesPrice = computed(() =>
  t('phone.catalog.labels.countTimesPrice', { count: view.value.totalCount, price: price.value }),
)

const unitPriceText = computed(() =>
  view.value.totalCount > 0 ? countTimesPrice.value : price.value,
)

const articleTotal = computed(() => formatPrice(view.value.articleTotalCents, props.language))

const defaultStationName = computed(() => {
  if (needsStationChoice(props.item)) {
    return null
  }
  const stationId = props.item.stationIds[0]
  return stationId === undefined ? null : props.stationNameFor(stationId)
})

function askForANote(): void {
  if (needsStationChoice(props.item)) {
    emit('addWithANoteAtAStation')
    return
  }
  groupBeingCorrected.value = null
  typedNote.value = ''
  isAsking.value = true
}

function correctTheNoteFor(group: PositionGroup): void {
  if (group.note === null) {
    return
  }
  groupBeingCorrected.value = group
  typedNote.value = group.note
  isAsking.value = true
}

function confirm(): void {
  const note = typedNote.value.trim()
  const corrected = groupBeingCorrected.value
  isAsking.value = false
  if (corrected === null) {
    emit('addWithANote', note)
    return
  }
  emit('renameNote', corrected.indexes, note)
}

function mostRecentIndexIn(group: PositionGroup): number {
  return group.indexes[group.indexes.length - 1]
}

function stationNameForRow(group: PositionGroup): string | null {
  return group.stationName ?? defaultStationName.value
}
</script>

<template>
  <div class="item-row" data-test="item-row">
    <div class="item-line d-flex align-start">
      <div class="item-body flex-grow-1">
        <div class="item-head" data-test="item-head">
          <v-btn
            class="add add-by-name"
            data-test="add-by-name"
            variant="text"
            :disabled="isSoldOut"
            @click="emit('add')"
          >
            <span class="name text-body-1" data-test="name">{{ item.name }}</span>
          </v-btn>
          <div class="price-line d-flex align-start" data-test="price-line">
            <v-btn
              class="add add-by-price"
              data-test="add-by-price"
              variant="text"
              tabindex="-1"
              :disabled="isSoldOut"
              @click="emit('add')"
            >
              <span class="facts text-body-2 text-medium-emphasis" data-test="facts">
                <span class="fact">
                  <span data-test="unit-price">{{ unitPriceText }}</span>
                  <span v-if="factAfterThePrice !== null" data-test="fact-separator">&nbsp;&middot;</span>
                </span>
                <span v-if="factAfterThePrice !== null" class="fact" :data-test="factAfterThePrice.kind">{{ factAfterThePrice.text }}</span>
              </span>
            </v-btn>
            <span v-if="view.totalCount > 0" class="article-total text-body-1" data-test="article-total">{{ articleTotal }}</span>
            <v-btn
              class="add-note"
              data-test="add-note"
              variant="text"
              :disabled="isSoldOut"
              @click="askForANote"
            >
              {{ t('phone.catalog.actions.addNote') }}
            </v-btn>
          </div>
        </div>
      </div>
    </div>

    <div
      v-for="group in view.rows"
      :key="group.indexes[0]"
      class="note-group d-flex align-center ga-2"
      data-test="note-group"
    >
      <span class="group-count" data-test="group-count">{{ group.indexes.length }}</span>
      <div class="group-label text-body-2 text-start flex-grow-1 d-flex align-center" data-test="group-label">
        <button
          v-if="group.stationName !== null"
          class="group-station"
          data-test="group-station"
          @click="emit('changeStation', group.indexes)"
        >
          {{ group.stationName }}
        </button>
        <span v-else-if="stationNameForRow(group) !== null" class="group-station-fixed" data-test="group-station-fixed">
          {{ stationNameForRow(group) }}
        </span>
        <span v-if="group.note !== null && stationNameForRow(group) !== null" class="group-separator">
          &middot;
        </span>
        <button
          v-if="group.note !== null"
          class="group-note text-medium-emphasis"
          data-test="group-note"
          @click="correctTheNoteFor(group)"
        >
          {{ group.note }}
        </button>
      </div>
      <v-btn
        class="group-remove stepper"
        data-test="group-remove"
        icon="mdi-minus"
        variant="text"
        @click="emit('removeOne', mostRecentIndexIn(group))"
      />
      <v-btn
        class="group-add stepper"
        data-test="group-add"
        icon="mdi-plus"
        variant="text"
        :disabled="isSoldOut"
        @click="emit('addLikeGroup', group.note, group.stationId)"
      />
    </div>

    <ItemNoteDialog
      v-model:is-open="isAsking"
      v-model:note="typedNote"
      :item-name="item.name"
      :is-correcting="groupBeingCorrected !== null"
      @confirm="confirm"
    />
  </div>
</template>

<style scoped>
.item-row + .item-row {
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.item-line {
  min-width: 0;
  min-height: 64px;
  padding-block: 4px;
}

.add-note {
  flex: 0 0 auto;
  min-height: var(--price-line-min-height);
  border-radius: 8px;
  padding-inline: 8px;
  text-transform: none;
  letter-spacing: normal;
}

.article-total {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
  min-height: var(--price-line-min-height);
  white-space: nowrap;
  padding-inline: 8px 4px;
}

.group-count {
  min-width: 2ch;
  font-size: 22px;
  font-weight: 700;
  text-align: center;
}

.stepper {
  flex: 0 0 auto;
  min-width: 48px;
  min-height: 48px;
  border-radius: 8px;
}

.item-body {
  min-width: 0;
}

.group-label {
  min-width: 0;
  overflow: hidden;
  white-space: nowrap;
}

.item-head {
  min-width: 0;
}

.price-line {
  --price-line-min-height: 48px;
  --fact-line-height: 1.25rem;
  min-width: 0;
}

.add {
  min-width: 0;
  height: auto;
}

.add-by-name {
  width: 100%;
  padding-block: 8px 0;
  padding-inline: 0.5rem;
}

.add-by-price {
  flex: 1 1 0;
  min-height: var(--price-line-min-height);
  padding-block: calc((var(--price-line-min-height) - var(--fact-line-height)) / 2);
  padding-inline: 0.5rem 0;
}

.add :deep(.v-btn__content) {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  justify-content: center;
  align-items: stretch;
  white-space: normal;
}

.facts {
  display: flex;
  flex-wrap: wrap;
  column-gap: 0.25em;
  line-height: var(--fact-line-height);
  text-align: start;
}

.fact {
  min-width: 0;
  overflow-wrap: anywhere;
}

.name {
  min-width: 0;
  text-align: start;
  overflow-wrap: anywhere;
}

.note-group {
  min-height: 2.25rem;
  padding-block: 2px;
  padding-inline-start: 1.5rem;
  padding-inline-end: 0.5rem;
}

.group-station,
.group-note {
  background: none;
  border: none;
  color: inherit;
  text-align: start;
  padding-block: 4px;
  padding-inline: 0;
  min-height: 2.25rem;
}

.group-station,
.group-station-fixed,
.group-separator {
  flex: 0 0 auto;
  white-space: nowrap;
}

.group-station-fixed {
  padding-block: 4px;
  padding-inline: 0;
  min-height: 2.25rem;
  color: inherit;
}

.group-separator {
  margin-inline: 0.35em;
}

.group-note {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
