<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { CatalogItemView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { itemState } from '../../core/catalogItemState'
import { assertNever } from '../../../shared/core/assertNever'
import type { EstimateRange } from '../../core/estimates'
import { estimateRangeText } from '../../core/estimateWording'
import { formatPrice } from '../../core/totals'
import { buildItemPositionsView, type ItemPosition, type PositionGroup } from '../../core/itemPositions'
import { needsStationChoice } from '../../core/routingPreview'
import { useKeyboardInset } from '../../composables/useKeyboardInset'

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
const keyboardInset = useKeyboardInset()

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

const estimate = computed(() =>
  isSoldOut.value ? null : estimateRangeText(props.estimateRange, t, props.language),
)

const view = computed(() => buildItemPositionsView(props.positions, props.item.priceCents))

const countTimesPrice = computed(() =>
  t('catalog.countTimesPrice', { count: view.value.totalCount, price: price.value }),
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

const canConfirm = computed(() => typedNote.value.trim().length > 0)

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
  <div class="item-row" :class="{ 'is-sold-out': isSoldOut }">
    <div class="item-line d-flex align-start">
      <div class="item-body flex-grow-1">
        <div class="item-head d-flex align-start">
          <v-btn class="add flex-grow-1" variant="text" :disabled="isSoldOut" @click="emit('add')">
            <span class="name-line">
              <span class="name text-body-1">{{ item.name }}</span>
              <span class="unit-price text-body-2 text-medium-emphasis">
                {{ unitPriceText }}
              </span>
            </span>
            <span class="facts text-body-2 text-medium-emphasis">
              <span v-if="estimate !== null" class="estimate">{{ estimate }}</span>
              <span v-if="isSoldOut" class="sold-out">{{ t('catalog.soldOut') }}</span>
            </span>
          </v-btn>
          <span v-if="view.totalCount > 0" class="article-total text-body-1">{{ articleTotal }}</span>
          <v-btn
            class="add-note add-note-in-header"
            variant="text"
            :disabled="isSoldOut"
            @click="askForANote"
          >
            {{ t('catalog.addNote') }}
          </v-btn>
        </div>
      </div>
    </div>

    <div
      v-for="group in view.rows"
      :key="group.indexes[0]"
      class="note-group d-flex align-center ga-2"
    >
      <span class="group-count">{{ group.indexes.length }}</span>
      <div class="group-label text-body-2 text-start flex-grow-1 d-flex align-center">
        <button
          v-if="group.stationName !== null"
          class="group-station"
          @click="emit('changeStation', group.indexes)"
        >
          {{ group.stationName }}
        </button>
        <span v-else-if="stationNameForRow(group) !== null" class="group-station-fixed">
          {{ stationNameForRow(group) }}
        </span>
        <span v-if="group.note !== null && stationNameForRow(group) !== null" class="group-separator">
          &middot;
        </span>
        <button
          v-if="group.note !== null"
          class="group-note text-medium-emphasis"
          @click="correctTheNoteFor(group)"
        >
          {{ group.note }}
        </button>
      </div>
      <v-btn
        class="group-remove stepper"
        icon="mdi-minus"
        variant="text"
        @click="emit('removeOne', mostRecentIndexIn(group))"
      />
      <v-btn
        class="group-add stepper"
        icon="mdi-plus"
        variant="text"
        :disabled="isSoldOut"
        @click="emit('addLikeGroup', group.note, group.stationId)"
      />
    </div>

    <v-dialog v-model="isAsking" max-width="480" :style="{ height: `calc(100% - ${keyboardInset}px)`, bottom: 'auto' }">
      <v-card class="note-dialog">
        <v-card-title class="title">{{ t('catalog.noteTitle', { name: item.name }) }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="typedNote"
            class="note-input"
            maxlength="200"
            autofocus
            :label="t('catalog.itemNote')"
            :placeholder="t('catalog.lineNotePlaceholder')"
            persistent-placeholder
            @keyup.enter="canConfirm && confirm()"
          />
        </v-card-text>
        <v-card-actions>
          <v-btn class="note-cancel" variant="text" @click="isAsking = false">
            {{ t('catalog.noteCancel') }}
          </v-btn>
          <v-spacer />
          <v-btn class="note-confirm" color="primary" variant="tonal" :disabled="!canConfirm" @click="confirm">
            {{ groupBeingCorrected === null ? t('catalog.noteAdd') : t('catalog.noteSave') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
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
  min-height: 48px;
  border-radius: 8px;
  padding-inline: 8px;
  text-transform: none;
  letter-spacing: normal;
}

.add-note-in-header {
  flex: 0 0 auto;
  align-self: center;
}

.name-line {
  display: flex;
  gap: 0.5rem;
  align-items: baseline;
  flex-wrap: wrap;
}

.unit-price,
.article-total {
  white-space: nowrap;
}

.article-total {
  flex: 0 0 auto;
  align-self: center;
  padding-inline: 0.75rem;
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

.add {
  min-width: 0;
  min-height: 64px;
  height: auto;
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
  gap: 0.5rem;
  align-items: center;
  white-space: nowrap;
  padding-inline: 0;
  padding-bottom: 0;
}

.name {
  min-width: 0;
  text-align: start;
  overflow-wrap: anywhere;
}

.sold-out {
  flex: 0 0 auto;
  white-space: nowrap;
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
