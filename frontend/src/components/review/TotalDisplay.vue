<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../core/apiTypes'
import { formatPrice } from '../../core/totals'

const props = defineProps<{ totalCents: number; language: AppLanguage }>()

const { t } = useI18n()
const total = computed(() => formatPrice(props.totalCents, props.language))
</script>

<template>
  <v-sheet class="total-display pa-4 my-4" border rounded>
    <span class="label text-medium-emphasis">{{ t('review.total') }}</span>
    <div class="amount text-h4">{{ total }}</div>
    <p class="help text-medium-emphasis">{{ t('review.totalHelp') }}</p>
  </v-sheet>
</template>
