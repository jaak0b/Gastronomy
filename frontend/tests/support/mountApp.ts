import { mount, type VueWrapper } from '@vue/test-utils'
import { testPlugins } from './plugins'

export async function mountApp(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  const App = (await import('../../src/App.vue')).default
  return mount(App, { global: { plugins: testPlugins(locale) } })
}
