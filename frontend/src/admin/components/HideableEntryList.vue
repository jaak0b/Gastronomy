<script setup lang="ts" generic="TEntry, TDraft, TSaved">
import { computed, ref, shallowRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MoveDirection } from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { useEditSession, type EditSessionMode } from '../composables/useEditSession'
import { useRowRefusals } from '../composables/useRowRefusals'
import BaseActionWithRefusalReason from './BaseActionWithRefusalReason.vue'
import BaseHeadingWithCheckbox from './BaseHeadingWithCheckbox.vue'
import BaseHideConfirmDialog from './BaseHideConfirmDialog.vue'
import BaseListRow from './BaseListRow.vue'
import BaseMoveButtons from './BaseMoveButtons.vue'
import BaseShowButton from './BaseShowButton.vue'
import BaseTrashButton from './BaseTrashButton.vue'

const props = defineProps<{
  entries: readonly TEntry[]
  idOf: (entry: TEntry) => string
  nameOf: (entry: TEntry) => string
  isHidden: (entry: TEntry) => boolean
  hidingRefusalOf: (entry: TEntry) => string | null
  isSelected?: (entry: TEntry) => boolean
  rowTestName: string
  newLabel: string
  hideTitle: string
  hideBody: string
  saveThrough: (draft: TDraft, edited: TEntry | null) => Promise<AdminActionResult<TSaved>>
  hideThrough: (entry: TEntry) => Promise<AdminActionResult<unknown>>
  showThrough: (entry: TEntry) => Promise<AdminActionResult<unknown>>
  moveThrough?: (
    entry: TEntry,
    direction: MoveDirection,
    showsHiddenEntries: boolean,
  ) => Promise<AdminActionResult<unknown>>
}>()
const emit = defineEmits<{ saved: [saved: TSaved, mode: EditSessionMode] }>()
defineSlots<{
  heading: () => unknown
  notices?: () => unknown
  details?: (scope: { entry: TEntry }) => unknown
  dialog: (scope: {
    entry: TEntry | null
    errorText: string | null
    save: (draft: TDraft) => Promise<void>
    cancel: () => void
  }) => unknown
}>()

const { t } = useI18n()
const rowRefusals = useRowRefusals()
const showsHidden = ref(false)
const entryAwaitingHiding = shallowRef<TEntry | null>(null)
const session = useEditSession<TEntry, TDraft, TSaved>({
  saveThrough: (draft, edited) => props.saveThrough(draft, edited),
  afterSaving: (saved, mode) => emit('saved', saved, mode),
})

const listedEntries = computed(() =>
  props.entries.filter((entry) => showsHidden.value || !props.isHidden(entry)),
)

async function actOnTheRow(entry: TEntry, action: () => Promise<AdminActionResult<unknown>>): Promise<void> {
  const rowId = props.idOf(entry)
  rowRefusals.forgetRefusalOf(rowId)
  rowRefusals.showRefusalOf(rowId, await action())
}

function askToHide(entry: TEntry): void {
  entryAwaitingHiding.value = entry
}

function stopAskingToHide(): void {
  entryAwaitingHiding.value = null
}

async function confirmHiding(): Promise<void> {
  const entry = entryAwaitingHiding.value
  entryAwaitingHiding.value = null
  if (entry === null) {
    return
  }
  await actOnTheRow(entry, () => props.hideThrough(entry))
}

async function showAgain(entry: TEntry): Promise<void> {
  await actOnTheRow(entry, () => props.showThrough(entry))
}

async function move(entry: TEntry, direction: MoveDirection): Promise<void> {
  const moveThrough = props.moveThrough
  if (moveThrough === undefined) {
    return
  }
  await actOnTheRow(entry, () => moveThrough(entry, direction, showsHidden.value))
}
</script>

<template>
  <div class="hideable-entry-list">
    <BaseHeadingWithCheckbox
      v-model="showsHidden"
      class="mb-3"
      :checkbox-label="t('admin.common.actions.showHidden')"
      checkbox-test-name="show-hidden-entries"
    >
      <slot name="heading" />
    </BaseHeadingWithCheckbox>

    <slot name="notices" />

    <div v-if="listedEntries.length > 0" class="rows mb-3">
      <BaseListRow
        v-for="(entry, position) in listedEntries"
        :key="idOf(entry)"
        :position="position"
        :is-selected="isSelected?.(entry) === true"
        :data-test="rowTestName"
        :data-test-id="idOf(entry)"
      >
        <div class="row-line d-flex align-center flex-wrap ga-3 py-2 px-3">
          <span class="entry-name text-body-1" data-test="entry-name">{{ nameOf(entry) }}</span>
          <v-chip v-if="isHidden(entry)" class="hidden" data-test="hidden" size="small" color="grey">
            {{ t('admin.festivals.labels.hidden') }}
          </v-chip>
          <v-spacer />
          <slot name="details" :entry="entry" />
          <v-btn class="edit-entry" data-test="edit-entry" variant="text" @click="session.openForEdit(entry)">
            {{ t('admin.common.actions.edit') }}
          </v-btn>
          <BaseMoveButtons v-if="moveThrough !== undefined" @move="(direction) => move(entry, direction)" />
          <BaseShowButton
            v-if="isHidden(entry)"
            class="show-entry"
            data-test="show-entry"
            @click="showAgain(entry)"
          />
          <BaseActionWithRefusalReason
            v-else
            class="hide-entry-wrapper"
            data-test="hide-entry-wrapper"
            :refusal-reason="hidingRefusalOf(entry)"
          >
            <template #default="{ refused }">
              <BaseTrashButton
                class="hide-entry"
                data-test="hide-entry"
                :disabled="refused"
                @click="askToHide(entry)"
              />
            </template>
          </BaseActionWithRefusalReason>
        </div>
        <v-alert
          v-if="rowRefusals.refusalTextOf(idOf(entry)) !== null"
          class="refusal mx-3 mb-2"
          data-test="refusal"
          type="warning"
          variant="tonal"
        >
          {{ rowRefusals.refusalTextOf(idOf(entry)) }}
        </v-alert>
      </BaseListRow>
    </div>

    <div class="new-entry-line d-flex justify-end">
      <v-btn class="new-entry" data-test="new-entry" variant="text" @click="session.openForCreate()">
        {{ newLabel }}
      </v-btn>
    </div>

    <slot
      v-if="session.isOpen.value"
      name="dialog"
      v-bind="{
        entry: session.edited.value,
        errorText: session.refusalText.value,
        save: session.save,
        cancel: session.close,
      }"
    />

    <BaseHideConfirmDialog
      v-if="entryAwaitingHiding !== null"
      :title="hideTitle"
      :body="hideBody"
      @confirm="confirmHiding"
      @cancel="stopAskingToHide"
    />
  </div>
</template>
