<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { AdminCategoryView, AdminItemView, type MoveDirection } from '../../../shared/api/generatedSchemas'
import type { PositionedRow } from '../../core/alternatingRows'
import BaseActionWithRefusalReason from '../BaseActionWithRefusalReason.vue'
import BaseEditButton from '../BaseEditButton.vue'
import BaseListRow from '../BaseListRow.vue'
import BaseEmptyListText from '../../../shared/components/BaseEmptyListText.vue'
import BaseIconAction from '../../../shared/components/BaseIconAction.vue'
import BaseMoveButtons from '../BaseMoveButtons.vue'
import BaseShowButton from '../BaseShowButton.vue'
import BaseTrashButton from '../BaseTrashButton.vue'
import CategoryLabel from '../../../shared/components/CategoryLabel.vue'

const props = defineProps<{
  category: AdminCategoryView
  rows: PositionedRow<AdminItemView>[]
  showsEmptyListText: boolean
  isAFestivalRunning: boolean
}>()
const emit = defineEmits<{
  'rename-category': []
  'move-category': [direction: MoveDirection]
  'deactivate-category': []
  'activate-category': []
  'edit-ingredients': [item: AdminItemView]
  'edit-item': [item: AdminItemView]
  'deactivate-item': [item: AdminItemView]
  'reactivate-item': [item: AdminItemView]
}>()

const { t } = useI18n()

function isOnTheRunningFestivalsMenu(item: AdminItemView): boolean {
  return props.isAFestivalRunning && item.atTheFestival !== null
}
</script>

<template>
  <section class="category-section" data-test="category-section">
    <div class="category-heading d-flex align-center ga-2 py-2">
      <CategoryLabel data-test="category-name" :category="category" :heading-level="2" />
      <v-chip
        v-if="!category.isActive"
        data-test="category-deactivated"
        color="grey"
      >
        {{ t('admin.common.labels.deactivated') }}
      </v-chip>
      <v-spacer />
      <BaseMoveButtons @move="(direction) => emit('move-category', direction)" />
      <BaseEditButton data-test="rename-category" @click="emit('rename-category')" />
      <BaseIconAction
        v-if="category.isActive"
        data-test="deactivate-category"
        icon="mdi-eye-off"
        color="error"
        :label="t('admin.categories.actions.deactivateConfirm')"
        @click="emit('deactivate-category')"
      />
      <BaseShowButton
        v-else
        data-test="activate-category"
        :label="t('admin.categories.actions.activate')"
        @click="emit('activate-category')"
      />
    </div>
    <BaseEmptyListText v-if="showsEmptyListText" class="mb-2" />
    <BaseListRow
      v-for="{ item, position } in rows"
      :key="item.itemId"
      data-test="item-row"
      :data-test-id="item.itemId"
      :position="position"
      :name="item.name"
      name-test-name="item-name"
    >
      <template #facts>
        <v-chip v-if="!item.isActive" data-test="item-deactivated" color="grey">
          {{ t('admin.common.labels.deactivated') }}
        </v-chip>
      </template>
      <template #actions>
        <v-btn data-test="edit-ingredients" variant="text" @click="emit('edit-ingredients', item)">
          {{ t('admin.ingredients.actions.edit') }}
        </v-btn>
        <BaseEditButton data-test="edit-item" @click="emit('edit-item', item)" />
        <BaseActionWithRefusalReason
          v-if="item.isActive"
          class="deactivate-wrapper"
          data-test="deactivate-wrapper"
          :refusal-reason="isOnTheRunningFestivalsMenu(item) ? t('errors.admin.items.isOnTheRunningFestivalsMenu') : null"
        >
          <template #default="{ refused }">
            <BaseTrashButton data-test="deactivate-item" :disabled="refused" @click="emit('deactivate-item', item)" />
          </template>
        </BaseActionWithRefusalReason>
        <BaseShowButton
          v-else
          data-test="reactivate-item"
          :label="t('admin.items.actions.activate')"
          @click="emit('reactivate-item', item)"
        />
      </template>
    </BaseListRow>
  </section>
</template>
