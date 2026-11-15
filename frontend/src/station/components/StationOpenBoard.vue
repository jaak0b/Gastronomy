<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseEmptyListText from '../../shared/components/BaseEmptyListText.vue'
import {
  articleTotalText,
  deliveryModeColourToken,
  articlePartText,
  type ArticleTotal,
} from '../../shared/core/stationBoard'

defineProps<{
  togetherCount: number
  asItComesCount: number
  articles: ArticleTotal[]
  showsEmptyListText: boolean
  failureText: string | null
}>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
</script>

<template>
  <section class="station-open-board" data-test="station-open-board">
    <div class="board-head d-flex align-center ga-3 mb-4">
      <h2 class="board-heading app-section-title flex-grow-1" data-test="board-heading">{{ t('station.board.labels.openArticlesHeading') }}</h2>
      <v-btn class="back-to-orders" data-test="back-to-orders" variant="outlined" @click="emit('close')">
        {{ t('station.board.actions.backToOrders') }}
      </v-btn>
    </div>
    <v-alert v-if="failureText !== null" class="board-failed mb-4" data-test="board-failed" type="warning" variant="tonal">
      {{ failureText }}
    </v-alert>
    <div class="mode-counts d-flex flex-wrap ga-2" data-test="mode-counts">
      <v-chip
        class="stat-together"
        data-test="stat-together"
        :color="deliveryModeColourToken('together')"
        variant="flat"
      >
        {{ t('station.board.labels.statsTogetherChip', { count: togetherCount }) }}
      </v-chip>
      <v-chip
        class="stat-as-it-comes"
        data-test="stat-as-it-comes"
        :color="deliveryModeColourToken('asItComes')"
        variant="flat"
      >
        {{ t('station.board.labels.statsAsItComesChip', { count: asItComesCount }) }}
      </v-chip>
    </div>
    <BaseEmptyListText v-if="showsEmptyListText" class="mt-5" />
    <div class="open-articles d-flex flex-column ga-3 mt-5">
      <v-card
        v-for="article in articles"
        :key="article.key"
        class="open-article pa-3"
        data-test="open-article"
        :data-test-id="article.key"
        variant="outlined"
      >
        <h3 class="article-total app-section-title mb-0" data-test="article-total">{{ articleTotalText(article, t) }}</h3>
        <ul v-if="article.partsByNote.length > 0" class="article-parts ps-6 mt-2" data-test="article-parts">
          <li
            v-for="part in article.partsByNote"
            :key="part.key"
            class="article-part"
            data-test="article-part"
            :class="{ 'font-weight-bold': part.note !== null }"
          >
            {{ articlePartText(part, t) }}
          </li>
        </ul>
      </v-card>
    </div>
  </section>
</template>

<style scoped>
.article-parts {
  margin: 0;
  list-style: none;
}

.article-part + .article-part {
  margin-top: 0.25rem;
}
</style>
