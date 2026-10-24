import type { ComputedRef } from 'vue'
import { useThemedValue } from '../../shared/composables/useThemedValue'
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
  return {
    name: useThemedValue(LIGHT_CHART_THEME, DARK_CHART_THEME),
    colours: useThemedValue(LIGHT_CHART_COLOURS, DARK_CHART_COLOURS),
  }
}
