<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AdminFestivalIngredientView,
  AdminFestivalView,
} from '../../../shared/api/generatedSchemas'
import { formatTimeOfDay, formatTimeTick, spansMoreThanOneDay } from '../../../shared/core/moments'
import { stockChartOptionOf } from '../../core/stockChartOption'
import {
  useAmountTextInEntryUnit,
  useIngredientAmountText,
} from '../../composables/useIngredientAmountText'
import BaseChart from '../BaseChart.vue'
import { useChartTheme } from '../../composables/useChartTheme'

const props = defineProps<{
  ingredient: AdminFestivalIngredientView
  festival: AdminFestivalView
}>()

const { t, locale } = useI18n()
const amountText = useIngredientAmountText()
const amountTextIn = useAmountTextInEntryUnit()
const { colours: chartColours } = useChartTheme()

const option = computed(() => {
  const showsWeekday = spansMoreThanOneDay(props.festival.startsAtUtc, props.festival.endsAtUtc)
  return stockChartOptionOf(
    props.ingredient,
    props.festival,
    {
      used: t('admin.ingredients.history.used'),
      available: t('admin.ingredients.labels.available'),
      forecastFromLastTwoHours: t('admin.ingredients.history.forecastFromLastTwoHours'),
      forecastSinceStart: t('admin.ingredients.history.forecastSinceStart'),
      now: t('admin.ingredients.history.now'),
      timeTickOf: (moment, form) => formatTimeTick(moment, locale.value, form),
      crossingOf: (moment) => formatTimeOfDay(moment, locale.value, showsWeekday),
      crossingBeyondTheChartOf: (moment) =>
        t('admin.ingredients.history.runsOutBeyondTheChart', {
          moment: formatTimeOfDay(moment, locale.value, true),
        }),
      axisAmountOf: (amount, entryUnit) => amountTextIn(amount, entryUnit),
      readoutOf: (moment, usedAmount) =>
        t('admin.ingredients.history.usedBy', {
          moment: formatTimeOfDay(moment, locale.value, showsWeekday),
          amount: amountText(usedAmount, props.ingredient.unit),
        }),
    },
    chartColours.value,
  )
})
</script>

<template>
  <div class="festival-stock-chart" data-test="stock-chart">
    <BaseChart :option="option" />
  </div>
</template>

<style scoped>
.festival-stock-chart {
  height: 18rem;
}
</style>
