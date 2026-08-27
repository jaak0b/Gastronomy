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
  <v-card class="item-button" :class="{ 'is-sold-out': isSoldOut }" variant="outlined">
    <v-btn class="add" block variant="text" height="88" :disabled="isSoldOut" @click="$emit('add')">
      <div class="d-flex flex-column align-center">
        <span class="name text-body-1">{{ item.name }}</span>
        <span class="price text-caption">{{ price }}</span>
        <span v-if="isSoldOut" class="sold-out text-caption">{{ t('catalog.soldOut') }}</span>
      </div>
    </v-btn>
    <v-card-actions v-if="quantity > 0" class="quantity-control">
      <v-btn
        class="remove"
        icon="mdi-minus"
        variant="text"
        :aria-label="t('catalog.removeOne')"
        @click="$emit('remove')"
      />
      <span class="quantity text-h6">{{ quantity }}</span>
    </v-card-actions>
  </v-card>
</template>
