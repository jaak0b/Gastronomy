<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CatalogItem } from '../../core/apiTypes'
import { itemState } from '../../core/catalogItemState'
import { assertNever } from '../../core/assertNever'
import { formatPrice } from '../../core/totals'
import type { AppLanguage } from '../../core/apiTypes'

const props = defineProps<{ item: CatalogItem; quantity: number; language: AppLanguage }>()
defineEmits<{ add: []; remove: [] }>()

const { t } = useI18n()

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
</script>

<template>
  <div class="item-button" :class="{ 'is-sold-out': isSoldOut }">
    <button type="button" class="add" :disabled="isSoldOut" @click="$emit('add')">
      <span class="name">{{ item.name }}</span>
      <span class="price">{{ price }}</span>
      <span v-if="isSoldOut" class="sold-out">{{ t('catalog.soldOut') }}</span>
    </button>
    <div v-if="quantity > 0" class="quantity-control">
      <button
        type="button"
        class="remove"
        :aria-label="t('catalog.removeOne')"
        @click="$emit('remove')"
      >
        {{ t('catalog.removeOne') }}
      </button>
      <span class="quantity">{{ quantity }}</span>
    </div>
  </div>
</template>
