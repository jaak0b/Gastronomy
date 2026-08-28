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
  <v-sheet class="total-display d-flex align-center pa-3" border rounded>
    <span class="label text-body-1 text-medium-emphasis">{{ t('review.total') }}</span>
    <v-spacer />
    <span class="amount text-h4">{{ total }}</span>
  </v-sheet>
</template>
