import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { inputOf, typeInto } from '../../../support/dom'
import { nextTick } from 'vue'
import { FOOD_ID, BRATWURST, mountDialog, pressSave, pressEnterInTheMinutes, knownCategories, categoriesLaptop } from './itemDialogFixture'

describe('the item dialog', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    categoriesLaptop()
    knownCategories()
  })

  it('is headed as a new item when there is none yet', async () => {
    mountDialog()

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toContain('Neuer Artikel'),
    )
  })

  it('is headed as an edit when an item is being changed', async () => {
    mountDialog(BRATWURST)

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toContain(
        'Artikel bearbeiten',
      ),
    )
  })

  it('starts with the name the item already has', async () => {
    mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="item-name-field"]')).not.toBeNull())

    expect(inputOf('[data-test="item-name-field"]').value).toBe('Bratwurst')
  })

  it('sends the whole item as the admin sees it', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-save"]')).not.toBeNull())
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({
      itemId: 'item-1',
      name: 'Bratwurst',
      categoryId: FOOD_ID,
      sortOrder: 1,
      productionMinutes: 15,
      isQueueIndependent: false,
    })
  })

  it('leaves the item alone when the admin cancels', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-cancel"]')).not.toBeNull())
    ;(document.querySelector('[data-test="form-cancel"]') as HTMLElement).click()
    await nextTick()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('save')).toBeUndefined()
  })
})

describe('the preparation time field', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    categoriesLaptop()
    knownCategories()
  })

  it('names the field for the preparation time', async () => {
    mountDialog()

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="production-minutes-field"] label')).not.toBeNull(),
    )

    expect(document.querySelector('[data-test="production-minutes-field"] label')?.textContent).toBe(
      'Zubereitungszeit in Minuten',
    )
  })

  it('shows the minutes of an item that already has them', async () => {
    mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())

    expect(inputOf('[data-test="production-minutes-field"]').value).toBe('15')
  })

  it('shows half a minute the way German writes it', async () => {
    mountDialog({ ...BRATWURST, productionMinutes: 1.5 })

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())

    expect(inputOf('[data-test="production-minutes-field"]').value).toBe('1,5')
  })

  it('shows half a minute the way English writes it', async () => {
    mountDialog({ ...BRATWURST, productionMinutes: 1.5 }, 'en')

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())

    expect(inputOf('[data-test="production-minutes-field"]').value).toBe('1.5')
  })

  it('stays empty for an item that is handed over right away', async () => {
    mountDialog({ ...BRATWURST, productionMinutes: null })

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())

    expect(inputOf('[data-test="production-minutes-field"]').value).toBe('')
  })

  it('sends the minutes that were typed', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())
    typeInto('[data-test="production-minutes-field"]', '20')
    await nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 20 })
  })

  it('sends half a minute written with a comma, the way German writes it', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())
    typeInto('[data-test="production-minutes-field"]', '1,5')
    await nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 1.5 })
  })

  it('sends half a minute written with a dot, the way English writes it', async () => {
    const dialog = mountDialog(BRATWURST, 'en')

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())
    typeInto('[data-test="production-minutes-field"]', '1.5')
    await nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 1.5 })
  })

  it('sends no preparation time when the field is left empty', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())
    typeInto('[data-test="production-minutes-field"]', '')
    await nextTick()
    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: null })
  })

  it('saves the highest allowed time when more is typed and Enter is pressed', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())
    const input = inputOf('[data-test="production-minutes-field"]')
    input.focus()
    typeInto('[data-test="production-minutes-field"]', '601')
    await nextTick()
    pressEnterInTheMinutes(dialog)
    await nextTick()

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ productionMinutes: 600 })
  })

  it('does not save on Enter while the name is missing', async () => {
    const dialog = mountDialog(BRATWURST)

    await vi.waitFor(() => expect(document.querySelector('[data-test="production-minutes-field"]')).not.toBeNull())
    inputOf('[data-test="production-minutes-field"]').focus()

    typeInto('[data-test="item-name-field"]', '')
    await nextTick()
    typeInto('[data-test="production-minutes-field"]', '10')
    await nextTick()
    pressEnterInTheMinutes(dialog)
    await nextTick()

    expect(dialog.emitted('save')).toBeUndefined()
  })
})

describe('the independent preparation choice', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    categoriesLaptop()
    knownCategories()
  })

  it('shows the choice of an item and saves it', async () => {
    const dialog = mountDialog({ ...BRATWURST, isQueueIndependent: true })

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="queue-independent-checkbox"]')).not.toBeNull(),
    )

    expect(inputOf('[data-test="queue-independent-checkbox"]').checked).toBe(true)

    await pressSave(dialog)

    expect(dialog.emitted('save')?.[0]?.[0]).toMatchObject({ isQueueIndependent: true })
  })
})

describe('the length of an article name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    categoriesLaptop()
    knownCategories()
  })

  it('stops where the laptop stops storing it', async () => {
    mountDialog()

    await vi.waitFor(() => expect(document.querySelector('[data-test="item-name-field"]')).not.toBeNull())

    expect(inputOf('[data-test="item-name-field"]').getAttribute('maxlength')).toBe('200')
  })
})
