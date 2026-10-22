<script setup lang="ts">
import VChart from 'vue-echarts'
import type { EChartsCoreOption, EChartsInitOpts } from 'echarts/core'
import { useChartTheme } from '../composables/useChartTheme'

const DRAWN_AS_SVG: EChartsInitOpts = { renderer: 'svg' }

defineProps<{ option: EChartsCoreOption }>()

const { name: chartThemeName } = useChartTheme()
</script>

<template>
  <VChart
    class="base-chart"
    :option="option"
    :theme="chartThemeName"
    :init-options="DRAWN_AS_SVG"
    autoresize
  />
</template>

<style scoped>
.base-chart {
  width: 100%;
  height: 100%;
}
</style>
