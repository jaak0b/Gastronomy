import { describe, expect, it } from 'vitest'
import { articleTotalText, articlePartText, stationStats } from '../../../src/shared/core/stationBoard'
import type { ArticleTotal, ItemLine } from '../../../src/shared/core/stationBoard'
import type { StationQueueItemView } from '../../../src/shared/api/generatedSchemas'
import { cancelledItem, item, stationOrder } from '../../support/station'

function notedItem(orderItemId: string, itemName: string, note: string): StationQueueItemView {
  return { ...item(orderItemId, itemName), note }
}

function boxes(articles: readonly ArticleTotal[]) {
  return articles.map(({ itemName, units, partsByNote }) => ({
    itemName,
    units,
    partsByNote: partsByNote.map(({ note, units: partUnits }) => ({ note, units: partUnits })),
  }))
}

function part(note: string | null, units: number): ItemLine {
  return { key: `Hotdog|${note ?? ''}`, itemName: 'Hotdog', note, isCancelled: false, units }
}

describe('the open articles of the overview board, one box per article', () => {
  it('adds up the units of one article across every order and note', () => {
    const orders = [
      stationOrder({
        stationOrderId: 'station-order-1',
        items: [item('a', 'Schnitzel Semmel'), item('b', 'Schnitzel Semmel'), notedItem('c', 'Schnitzel Semmel', 'Mit Salat')],
      }),
      stationOrder({ stationOrderId: 'station-order-2', items: [item('d', 'Schnitzel Semmel')] }),
    ]

    const articles = stationStats(orders).openArticles

    expect(articles.map(({ itemName, units }) => ({ itemName, units }))).toEqual([
      { itemName: 'Schnitzel Semmel', units: 4 },
    ])
  })

  it('lists the part without a note first and then the noted part, each with its own count', () => {
    const orders = [
      stationOrder({
        items: [item('a', 'Schnitzel Semmel'), notedItem('b', 'Schnitzel Semmel', 'Mit Salat'), item('c', 'Schnitzel Semmel')],
      }),
    ]

    expect(boxes(stationStats(orders).openArticles)).toEqual([
      {
        itemName: 'Schnitzel Semmel',
        units: 3,
        partsByNote: [
          { note: null, units: 2 },
          { note: 'Mit Salat', units: 1 },
        ],
      },
    ])
  })

  it('lists two different notes of one article apart, the larger count first', () => {
    const orders = [
      stationOrder({
        items: [
          notedItem('a', 'Hotdog', 'Ohne Ketchup'),
          notedItem('b', 'Hotdog', 'Mit Zwiebeln'),
          notedItem('c', 'Hotdog', 'Mit Zwiebeln'),
          item('d', 'Hotdog'),
        ],
      }),
    ]

    expect(boxes(stationStats(orders).openArticles)).toEqual([
      {
        itemName: 'Hotdog',
        units: 4,
        partsByNote: [
          { note: null, units: 1 },
          { note: 'Mit Zwiebeln', units: 2 },
          { note: 'Ohne Ketchup', units: 1 },
        ],
      },
    ])
  })

  it('lists only the noted parts when every unit carries a note', () => {
    const orders = [
      stationOrder({ items: [notedItem('a', 'Hotdog', 'Ohne Ketchup'), notedItem('b', 'Hotdog', 'Mit Zwiebeln')] }),
    ]

    expect(boxes(stationStats(orders).openArticles)[0].partsByNote).toEqual([
      { note: 'Mit Zwiebeln', units: 1 },
      { note: 'Ohne Ketchup', units: 1 },
    ])
  })

  it('gives an article without any note no parts beneath its total', () => {
    const orders = [stationOrder({ items: [item('a', 'Bier'), item('b', 'Bier')] })]

    expect(boxes(stationStats(orders).openArticles)).toEqual([{ itemName: 'Bier', units: 2, partsByNote: [] }])
  })

  it('puts the largest total first and breaks ties by name', () => {
    const orders = [
      stationOrder({
        items: [
          item('a', 'Wasser'),
          item('b', 'Bratwurst'),
          notedItem('c', 'Bratwurst', 'Ohne Senf'),
          item('d', 'Bier'),
          item('e', 'Bier'),
          item('f', 'Pommes'),
          item('g', 'Pommes'),
          item('h', 'Pommes'),
        ],
      }),
    ]

    expect(stationStats(orders).openArticles.map(({ itemName, units }) => `${units} ${itemName}`)).toEqual([
      '3 Pommes',
      '2 Bier',
      '2 Bratwurst',
      '1 Wasser',
    ])
  })

  it('leaves handed-out and cancelled units out of the total', () => {
    const orders = [
      stationOrder({
        items: [
          item('a', 'Pommes'),
          item('b', 'Pommes', '2026-09-05T18:30:00Z'),
          cancelledItem('c', 'Pommes'),
        ],
      }),
    ]

    expect(boxes(stationStats(orders).openArticles)).toEqual([{ itemName: 'Pommes', units: 1, partsByNote: [] }])
  })
})

describe('the words in one box of the overview board', () => {
  const words = (key: string, values?: Record<string, string | number>): string =>
    values === undefined ? `[${key}]` : `[${key} ${Object.values(values).join(' ')}]`

  it('names the total with the article', () => {
    const article: ArticleTotal = { key: 'Hotdog', itemName: 'Hotdog', units: 20, partsByNote: [] }

    expect(articleTotalText(article, words)).toBe('[common.labels.countTimesItem 20 Hotdog]')
  })

  it('names the part without a note by its count', () => {
    expect(articlePartText(part(null, 17), words)).toBe('[common.labels.countTimesItem 17 [station.board.labels.withoutNote]]')
  })

  it('names a noted part by its count and its note', () => {
    expect(articlePartText(part('Ohne Ketchup', 3), words)).toBe(
      '[common.labels.countTimesItem 3 [common.labels.note Ohne Ketchup]]',
    )
  })
})
