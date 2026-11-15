import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StationOpenBoard from '../../../src/station/components/StationOpenBoard.vue'
import { testPlugins } from '../../support/plugins'
import type { ArticleTotal, ItemLine } from '../../../src/shared/core/stationBoard'
import { nextTick } from 'vue'

function part(itemName: string, note: string | null, units: number): ItemLine {
  return { key: `${itemName}|${note ?? ''}`, itemName, note, isCancelled: false, units }
}

function article(itemName: string, units: number, partsByNote: ItemLine[] = []): ArticleTotal {
  return { key: itemName, itemName, units, partsByNote }
}

const HOTDOG_WITH_A_NOTE = article('Hotdog', 23, [part('Hotdog', null, 20), part('Hotdog', 'Ohne Ketchup', 3)])

function textsOf(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '')
}

function box(itemName: string): string {
  return `[data-test="station-open-board"] [data-test="open-article"][data-test-id="${itemName}"]`
}

function partOf(itemName: string, partText: string): Element | undefined {
  return [...document.querySelectorAll(`${box(itemName)} [data-test="article-part"]`)].find(
    (element) => element.textContent?.trim() === partText,
  )
}

function board(articles: ArticleTotal[], failureText: string | null = null, showsEmptyListText = false, locale: 'de' | 'en' = 'de') {
  return mount(StationOpenBoard, {
    props: { togetherCount: 2, asItComesCount: 3, articles, showsEmptyListText, failureText },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

describe('the overview board at a station', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('names the heading and the two mode counts', () => {
    board([])

    expect(document.querySelector('[data-test="station-open-board"] [data-test="board-heading"]')?.textContent?.trim()).toBe(
      'Offene Artikel',
    )
    expect(textsOf('[data-test="station-open-board"] [data-test="stat-together"]')).toEqual(['Gemeinsam 2'])
    expect(textsOf('[data-test="station-open-board"] [data-test="stat-as-it-comes"]')).toEqual(['Einzeln 3'])
  })

  it('heads one box per open article with its total, in the order it is given', () => {
    board([HOTDOG_WITH_A_NOTE, article('Bier', 3)])

    expect(textsOf('[data-test="station-open-board"] [data-test="open-article"] [data-test="article-total"]')).toEqual([
      '23 x Hotdog',
      '3 x Bier',
    ])
  })

  it('lists every part of a noted article beneath its total in the same box', () => {
    board([HOTDOG_WITH_A_NOTE])

    expect(textsOf(`${box('Hotdog')} [data-test="article-part"]`)).toEqual([
      '20 x ohne Hinweis',
      '3 x Hinweis: Ohne Ketchup',
    ])
  })

  it('shows no parts beneath an article that carries no note', () => {
    board([article('Bier', 3)])

    expect(document.querySelector(`${box('Bier')} [data-test="article-parts"]`)).toBeNull()
  })

  it('sets a noted part in bold and leaves the part without a note plain', () => {
    board([HOTDOG_WITH_A_NOTE])

    expect({
      withoutNote: partOf('Hotdog', '20 x ohne Hinweis')?.classList.contains('font-weight-bold'),
      noted: partOf('Hotdog', '3 x Hinweis: Ohne Ketchup')?.classList.contains('font-weight-bold'),
    }).toEqual({ withoutNote: false, noted: true })
  })

  it('writes the parts in English on an English tablet', () => {
    board([article('Hotdog', 23, [part('Hotdog', null, 20), part('Hotdog', 'No ketchup', 3)])], null, false, 'en')

    expect(textsOf(`${box('Hotdog')} [data-test="article-part"]`)).toEqual(['20 x without note', '3 x Note: No ketchup'])
  })

  it('shows the shared text of an empty list when the page says no article is open', () => {
    board([], null, true)

    expect(textsOf('[data-test="station-open-board"] [data-test="empty-list-text"]')).toEqual(['Keine Einträge'])
  })

  it('shows no text of an empty list while the page does not say no article is open', () => {
    board([article('Bier', 1)])

    expect(textsOf('[data-test="station-open-board"] [data-test="empty-list-text"]')).toEqual([])
  })

  it('stays live while it is open', async () => {
    const page = board([article('Bratwurst', 1)])

    await page.setProps({
      articles: [article('Bier', 2), article('Bratwurst', 1)],
    })

    expect(textsOf('[data-test="station-open-board"] [data-test="article-total"]')).toEqual(['2 x Bier', '1 x Bratwurst'])
  })

  it('goes back to the orders on the back button', async () => {
    const page = board([])

    expect(textsOf('[data-test="station-open-board"] [data-test="back-to-orders"]')).toEqual(['Zurück zu den Bestellungen'])

    ;(document.querySelector('[data-test="station-open-board"] [data-test="back-to-orders"]') as HTMLElement).click()
    await nextTick()

    expect(page.emitted('close')).toEqual([[]])
  })

  it('shows the warning the page passed in, above the counts', () => {
    board(
      [],
      'Der Rechner ist nicht erreichbar, die Liste ist möglicherweise veraltet. Laden Sie die Seite neu.',
    )

    const warning = document.querySelector('[data-test="station-open-board"] [data-test="board-failed"]')
    const counts = document.querySelector('[data-test="station-open-board"] [data-test="mode-counts"]')

    expect(warning?.textContent?.trim()).toBe(
      'Der Rechner ist nicht erreichbar, die Liste ist möglicherweise veraltet. Laden Sie die Seite neu.',
    )
    expect(
      (warning as Node).compareDocumentPosition(counts as Node) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
  })

  it('shows no warning when the page behind it has nothing to report', () => {
    board([])

    expect(document.querySelector('[data-test="station-open-board"] [data-test="board-failed"]')).toBeNull()
  })
})
