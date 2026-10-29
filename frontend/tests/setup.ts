import { config } from '@vue/test-utils'
import { createVuetify } from 'vuetify'
import { APP_VUETIFY_OPTIONS } from '../src/shared/styles/appVuetify'

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = globalThis.ResizeObserver ?? (ResizeObserverStub as never)

if (Element.prototype.scrollIntoView === undefined) {
  Element.prototype.scrollIntoView = () => undefined
}

if (window.visualViewport === undefined) {
  Object.defineProperty(window, 'visualViewport', {
    writable: true,
    configurable: true,
    value: {
      width: 1024,
      height: 768,
      offsetLeft: 0,
      offsetTop: 0,
      scale: 1,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    },
  })
}

if (window.matchMedia === undefined) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => false,
    }),
  })
}

config.global.plugins = [...(config.global.plugins ?? []), createVuetify(APP_VUETIFY_OPTIONS)]
