import { computed, type ComputedRef } from 'vue'
import { useTheme } from 'vuetify'
import {
  DARK_CHART_COLOURS,
  DARK_CHART_THEME,
  LIGHT_CHART_COLOURS,
  LIGHT_CHART_THEME,
  type ChartColours,
} from '../core/chartTheme'

export interface ChartTheme {
  name: ComputedRef<string>
  colours: ComputedRef<ChartColours>
}

export function useChartTheme(): ChartTheme {
  const appTheme = useTheme()
  const isDark = computed(() => appTheme.current.value.dark)
  return {
    name: computed(() => (isDark.value ? DARK_CHART_THEME : LIGHT_CHART_THEME)),
    colours: computed(() => (isDark.value ? DARK_CHART_COLOURS : LIGHT_CHART_COLOURS)),
  }
}
