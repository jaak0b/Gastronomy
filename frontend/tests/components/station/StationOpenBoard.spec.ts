import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StationOpenBoard from '../../../src/components/station/StationOpenBoard.vue'
import { testPlugins } from '../../support/plugins'

function textsOf(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '')
}

function board(
  lines: { itemName: string; note: string | null; units: number }[],
  failureText: string | null = null,
) {
  return mount(StationOpenBoard, {
    props: { togetherCount: 2, asItComesCount: 3, lines, failureText },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

describe('the overview board at a station', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('names the heading and the two mode counts', () => {
    board([])

    expect(document.querySelector('.station-open-board .board-heading')?.textContent?.trim()).toBe(
      'Offene Artikel',
    )
    expect(textsOf('.station-open-board .stat-together')).toEqual(['Gemeinsam 2'])
    expect(textsOf('.station-open-board .stat-as-it-comes')).toEqual(['Einzeln 3'])
  })

  it('lists every open article once per note, largest count first and ties by name', () => {
    board([
      { itemName: 'Wasser', note: null, units: 1 },
      { itemName: 'Hotdog', note: null, units: 20 },
      { itemName: 'Hotdog', note: 'Ohne Ketchup', units: 3 },
      { itemName: 'Bier', note: null, units: 3 },
      { itemName: 'Bratwurst', note: null, units: 2 },
    ])

    expect(textsOf('.station-open-board .unit')).toEqual([
      '20 x Hotdog',
      '3 x Bier',
      '3 x Hotdog · Hinweis: Ohne Ketchup',
      '2 x Bratwurst',
      '1 x Wasser',
    ])
  })

  it('stays live while it is open', async () => {
    const page = board([{ itemName: 'Bratwurst', note: null, units: 1 }])

    await page.setProps({
      lines: [
        { itemName: 'Bratwurst', note: null, units: 1 },
        { itemName: 'Bier', note: null, units: 2 },
      ],
    })

    expect(textsOf('.station-open-board .unit')).toEqual(['2 x Bier', '1 x Bratwurst'])
  })

  it('goes back to the orders on the back button', async () => {
    const page = board([])

    expect(textsOf('.station-open-board .back-to-orders')).toEqual(['Zurück zu den Bestellungen'])

    ;(document.querySelector('.station-open-board .back-to-orders') as HTMLElement).click()
    await page.vm.$nextTick()

    expect(page.emitted('close')).toEqual([[]])
  })

  it('shows the warning the page passed in, above the counts', () => {
    board(
      [],
      'Laden Sie die Seite neu. Der Laptop war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )

    const warning = document.querySelector('.station-open-board .board-failed')
    const counts = document.querySelector('.station-open-board .mode-counts')

    expect(warning?.textContent?.trim()).toBe(
      'Laden Sie die Seite neu. Der Laptop war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )
    expect(
      (warning as Node).compareDocumentPosition(counts as Node) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
  })

  it('shows no warning when the page behind it has nothing to report', () => {
    board([])

    expect(document.querySelector('.station-open-board .board-failed')).toBeNull()
  })
})
