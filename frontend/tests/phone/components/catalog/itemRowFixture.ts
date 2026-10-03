import { vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { testPlugins } from '../../../support/plugins'
import ItemRow from '../../../../src/phone/components/catalog/ItemRow.vue'
import { useKeyboardInset } from '../../../../src/phone/composables/useKeyboardInset'
import { CatalogItemView } from '../../../../src/shared/api/generatedSchemas'
import type { EstimateRange } from '../../../../src/phone/core/estimates'
import type { ItemPosition } from '../../../../src/phone/core/itemPositions'
import { inputOf, typeInto, clickOn } from '../../../support/dom'

export function item(isAvailable: boolean): CatalogItemView {
  return {
    id: 'item-wasser',
    name: 'Wasser',
    categoryId: 'category-getraenke',
    priceCents: 200,
    sortOrder: 1,
    isAvailable,
    stationIds: ['station-bar'],
    productionMinutes: null,
    isQueueIndependent: false,
  }
}

export function plain(index: number): ItemPosition {
  return {
    index,
    note: null,
    hasAStationChoice: false,
    stationId: 'station-bar',
    stationName: null,
  }
}

export function noted(index: number, note: string): ItemPosition {
  return {
    index,
    note,
    hasAStationChoice: false,
    stationId: 'station-bar',
    stationName: null,
  }
}

export function stationNameFor(stationId: string): string {
  return stationId === 'station-bar' ? 'Theke' : stationId
}

export function mountRow(isAvailable: boolean, positions: ItemPosition[]) {
  return mount(ItemRow, {
    props: {
      item: item(isAvailable),
      positions,
      language: 'de' as const,
      estimateRange: null,
      stationNameFor,
    },
    global: { plugins: testPlugins('de') },
    attachTo: document.body,
  })
}

export function mountRowForAnItemAtSeveralStations(isAvailable: boolean, positions: ItemPosition[]) {
  return mount(ItemRow, {
    props: {
      item: { ...item(isAvailable), stationIds: ['station-kueche', 'station-bar'] },
      positions,
      language: 'de' as const,
      estimateRange: null,
      stationNameFor,
    },
    global: { plugins: testPlugins('de') },
    attachTo: document.body,
  })
}

export function dialogField(): HTMLInputElement {
  return inputOf('[data-test="note-dialog"] [data-test="note-input"]')
}

export async function enterTheNote(text: string): Promise<void> {
  typeInto(dialogField(), text)
  await flushPromises()
}

export async function confirmDialog(): Promise<void> {
  await clickOn('[data-test="note-dialog"] [data-test="note-confirm"]')
}

export function theKeyboard(height: number, scale = 1) {
  return {
    height,
    scale,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  }
}

export const InsetProbe = defineComponent({
  setup() {
    const inset = useKeyboardInset()
    return () => h('div', { 'data-test': 'inset-probe' }, String(inset.value))
  },
})

export function mountTwoProbes() {
  const firstIsThere = ref(true)
  const secondIsThere = ref(true)
  mount(
    defineComponent({
      setup() {
        return () =>
          h('div', [
            firstIsThere.value ? h(InsetProbe) : null,
            secondIsThere.value ? h(InsetProbe) : null,
          ])
      },
    }),
    { attachTo: document.body },
  )
  return { firstIsThere, secondIsThere }
}

export function mountRowWithEstimate(
  range: EstimateRange | null,
  locale: 'de' | 'en' = 'de',
  isAvailable = true,
  positions: ItemPosition[] = [],
) {
  return mount(ItemRow, {
    props: {
      item: item(isAvailable),
      positions,
      language: locale,
      estimateRange: range,
      stationNameFor,
    },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

export function schnitzel(): CatalogItemView {
  return {
    id: 'item-schnitzel',
    name: 'Schnitzel',
    categoryId: 'category-essen',
    priceCents: 300,
    sortOrder: 1,
    isAvailable: true,
    stationIds: ['station-schank', 'station-kueche'],
    productionMinutes: null,
    isQueueIndependent: false,
  }
}

export function at(index: number, stationId: string, stationName: string, note: string | null = null): ItemPosition {
  return { index, note, hasAStationChoice: true, stationId, stationName }
}
