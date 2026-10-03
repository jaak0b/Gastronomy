import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineAdminList } from '../../../src/admin/stores/adminList'
import {
  AdminCategoryListView,
  AdminCategoryView,
  type SaveCategoryRequest,
} from '../../../src/shared/api/generatedSchemas'
import { aHold, answer, heldUntil, inTurn, refusal, stubLaptop, type StubbedLaptop } from '../../support/laptop'

interface CategoryDraft {
  name: string
  colourHex: string
}

const FOOD = { categoryId: 'category-essen', name: 'Essen', colourHex: '#AA0000', sortOrder: 1, isActive: true }
const DRINKS = { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#0000AA', sortOrder: 2, isActive: true }
const SWEETS = { categoryId: 'category-suesses', name: 'Süßes', colourHex: '#00AA00', sortOrder: 3, isActive: true }

const SWEETS_DRAFT: CategoryDraft = { name: 'Süßes', colourHex: '#00AA00' }

interface ListOptions {
  reloadAfterWriting?: () => Promise<void>
  currentView?: () => string | null
}

function categoryList(options: ListOptions = {}) {
  return defineAdminList({
    path: '/api/admin/categories',
    listSchema: AdminCategoryListView,
    entrySchema: AdminCategoryView,
    entriesOf: (response) => response.categories,
    idOf: (category) => category.categoryId,
    requestBodyOf: (draft: CategoryDraft): SaveCategoryRequest => ({
      name: draft.name,
      colourHex: draft.colourHex,
    }),
    reloadAfterWriting: options.reloadAfterWriting,
    currentView: options.currentView,
  })
}

function laptopListing(...lists: unknown[][]): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', /./, inTurn(...lists.map((categories) => answer({ categories }))))
}

async function nextTurn(): Promise<void> {
  await new Promise((resume) => setTimeout(resume, 0))
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('loading an admin list', () => {
  it('holds what the laptop listed, in its order', async () => {
    laptopListing([DRINKS, FOOD])
    const list = categoryList()

    await list.load()

    expect(list.entries.value).toEqual([DRINKS, FOOD])
    expect(list.loadFailed.value).toBe(false)
  })

  it('keeps the newer answer when an older load answers last', async () => {
    const olderAnswer = aHold()
    stubLaptop().answers(
      'GET',
      /./,
      inTurn(
        heldUntil(olderAnswer.released, answer({ categories: [FOOD] })),
        answer({ categories: [DRINKS] }),
      ),
    )
    const list = categoryList()

    const older = list.load()
    await nextTurn()
    await list.load()
    olderAnswer.release()
    await older

    expect(list.entries.value).toEqual([DRINKS])
  })

  it('says the load failed when the laptop refused it, and keeps the list it had', async () => {
    stubLaptop().answers('GET', /./, inTurn(answer({ categories: [FOOD] }), answer({}, 500)))
    const list = categoryList()
    await list.load()

    await list.load()

    expect(list.loadFailed.value).toBe(true)
    expect(list.entries.value).toEqual([FOOD])
  })
})

describe('creating an entry in an admin list', () => {
  it('sends the draft and shows the entry the laptop returned', async () => {
    const laptop = laptopListing([FOOD]).answers('POST', '/api/admin/categories', answer(SWEETS))
    const list = categoryList()
    await list.load()

    const result = await list.create(SWEETS_DRAFT)

    expect(result).toEqual({ kind: 'ok', value: SWEETS })
    expect(laptop.callsTo('POST', '/api/admin/categories')[0].body).toEqual(SWEETS_DRAFT)
    expect(list.entries.value).toEqual([FOOD, SWEETS])
  })

  it('keeps showing a new entry while the next load does not list it yet', async () => {
    laptopListing([FOOD], [FOOD]).answers('POST', '/api/admin/categories', answer(SWEETS))
    const list = categoryList()
    await list.load()
    await list.create(SWEETS_DRAFT)

    await list.load()

    expect(list.entries.value).toEqual([FOOD, SWEETS])
  })

  it('shows a new entry once when the next load lists it, then follows the laptop', async () => {
    laptopListing([FOOD], [FOOD, SWEETS], [FOOD]).answers(
      'POST',
      '/api/admin/categories',
      answer(SWEETS),
    )
    const list = categoryList()
    await list.load()
    await list.create(SWEETS_DRAFT)

    await list.load()
    const listedOnce = list.entries.value
    await list.load()

    expect(listedOnce).toEqual([FOOD, SWEETS])
    expect(list.entries.value).toEqual([FOOD])
  })

  it('leaves the list alone when the view changed while the answer was on its way', async () => {
    const theAnswer = aHold()
    laptopListing([FOOD]).answers(
      'POST',
      '/api/admin/categories',
      heldUntil(theAnswer.released, answer(SWEETS)),
    )
    let view = 'festival-sommer'
    const list = categoryList({ currentView: () => view })
    await list.load()

    const creating = list.create(SWEETS_DRAFT)
    await nextTurn()
    view = 'festival-winter'
    theAnswer.release()
    const result = await creating

    expect(result).toEqual({ kind: 'ok', value: SWEETS })
    expect(list.entries.value).toEqual([FOOD])
  })

  it('sends the draft and reads the list again when the laptop returns no entry', async () => {
    const laptop = laptopListing([FOOD], [FOOD, SWEETS])
    const list = categoryList()
    await list.load()

    const result = await list.createThenReload(SWEETS_DRAFT)

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.writtenBodies()).toEqual([SWEETS_DRAFT])
    expect(list.entries.value).toEqual([FOOD, SWEETS])
  })

  it('returns the refusal and does not read the list again when the laptop refuses a create that reloads', async () => {
    const laptop = laptopListing([FOOD]).answers(
      'POST',
      '/api/admin/categories',
      refusal('errors.categoryNameTaken'),
    )
    const list = categoryList()
    await list.load()
    laptop.forgetCalls()

    const result = await list.createThenReload(SWEETS_DRAFT)

    expect(result.kind).toBe('failed')
    expect(laptop.callsTo('GET', /./)).toEqual([])
  })
})

describe('changing an entry in an admin list', () => {
  it('puts the draft to the entry and shows the list read afterwards', async () => {
    const renamed = { ...FOOD, name: 'Speisen' }
    const laptop = laptopListing([FOOD], [renamed])
    const list = categoryList()
    await list.load()

    const result = await list.update('category-essen', { name: 'Speisen', colourHex: '#AA0000' })

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.writes().map((call) => [call.method, call.url, call.body])).toEqual([
      ['PUT', '/api/admin/categories/category-essen', { name: 'Speisen', colourHex: '#AA0000' }],
    ])
    expect(list.entries.value).toEqual([renamed])
  })

  it('deactivates an entry on its deactivate route without a body', async () => {
    const laptop = laptopListing([FOOD], [{ ...FOOD, isActive: false }])
    const list = categoryList()
    await list.load()

    await list.setActive('category-essen', false)

    expect(laptop.writes().map((call) => [call.method, call.url, call.body])).toEqual([
      ['POST', '/api/admin/categories/category-essen/deactivate', undefined],
    ])
    expect(list.entries.value).toEqual([{ ...FOOD, isActive: false }])
  })

  it('activates an entry on its activate route without a body', async () => {
    const laptop = laptopListing([FOOD])
    const list = categoryList()

    await list.setActive('category-essen', true)

    expect(laptop.writes().map((call) => [call.method, call.url, call.body])).toEqual([
      ['POST', '/api/admin/categories/category-essen/activate', undefined],
    ])
  })

  it('runs the reload the definition names instead of its own load after a write', async () => {
    const laptop = laptopListing([FOOD])
    const reloadAfterWriting = vi.fn(async () => undefined)
    const list = categoryList({ reloadAfterWriting })

    await list.update('category-essen', { name: 'Speisen', colourHex: '#AA0000' })

    expect(reloadAfterWriting).toHaveBeenCalledTimes(1)
    expect(laptop.callsTo('GET', /./)).toEqual([])
  })
})
