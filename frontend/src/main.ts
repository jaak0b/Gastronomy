import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import 'vuetify/styles'
import './shared/styles/controls.css'
import './shared/styles/cancelledItems.css'
import './shared/styles/surfaceText.css'
import '@mdi/font/css/materialdesignicons.css'
import { createVuetify } from 'vuetify'
import { registerTheme, use } from 'echarts/core'
import { LineChart } from 'echarts/charts'
import {
  GridComponent,
  LegendComponent,
  MarkLineComponent,
  MarkPointComponent,
  TooltipComponent,
} from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'
import App from './App.vue'
import { CHART_THEMES } from './admin/core/chartTheme'
import de from './shared/i18n/de.json'
import en from './shared/i18n/en.json'
import { startRouter } from './shared/router/router'
import { appTheme } from './shared/theme'
import { APP_VUETIFY_OPTIONS } from './shared/styles/appVuetify'
import { initialLanguage } from './shared/core/deviceLanguage'

export const i18n = createI18n({
  legacy: false,
  locale: initialLanguage(),
  fallbackLocale: 'de',
  messages: { de, en },
})

export const vuetify = createVuetify({ ...APP_VUETIFY_OPTIONS, theme: appTheme })

use([
  LineChart,
  GridComponent,
  LegendComponent,
  MarkLineComponent,
  MarkPointComponent,
  TooltipComponent,
  SVGRenderer,
])

for (const chartTheme of CHART_THEMES) {
  registerTheme(chartTheme.name, chartTheme.theme)
}

startRouter()

createApp(App).use(createPinia()).use(i18n).use(vuetify).mount('#app')
