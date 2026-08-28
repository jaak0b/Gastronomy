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
  <div class="item-row align-center rounded border" :class="{ 'is-sold-out': isSoldOut }">
    <div class="quantity-control d-flex align-center">
      <template v-if="quantity > 0">
        <v-btn
          class="remove"
          icon="mdi-minus"
          variant="text"
          size="large"
          :aria-label="t('catalog.removeOne')"
          @click="$emit('remove')"
        />
        <span class="quantity text-h6">{{ quantity }}</span>
      </template>
    </div>
    <v-btn class="add" variant="text" height="64" :disabled="isSoldOut" @click="$emit('add')">
      <span class="name text-body-1">{{ item.name }}</span>
      <span v-if="isSoldOut" class="sold-out text-caption">{{ t('catalog.soldOut') }}</span>
      <span class="price text-body-1">{{ price }}</span>
    </v-btn>
  </div>
</template>

<style scoped>
.item-row {
  display: grid;
  grid-template-columns: 96px 1fr;
}

.quantity-control {
  justify-content: flex-start;
}

.add :deep(.v-btn__content) {
  display: flex;
  width: 100%;
  justify-content: space-between;
  gap: 12px;
}

.quantity {
  min-width: 2ch;
  text-align: center;
}
</style>
