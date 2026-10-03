import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '../../../../src/phone/components/header/AppHeader.vue'
import { currentRoute, navigate, registerOpenStepCloser } from '../../../../src/shared/router/router'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer } from '../../../support/laptop'
import { nextTick } from 'vue'

const AppBarStub = {
  template: '<div class="app-header"><slot /></div>',
}

function mountHeader() {
  return mount(AppHeader, {
    global: {
      plugins: testPlugins(),
      stubs: { VAppBar: AppBarStub },
    },
    attachTo: document.body,
  })
}

describe('finding the way back to the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubLaptop().answersEverythingElse(answer({}))
    navigate('/stations')
  })

  it('offers the ordering screen in the row of buttons', async () => {
    const header = mountHeader()

    await header.get('[data-test="catalog-link"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

describe('the row of destinations', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('names every destination in words, under an icon that fits a phone', () => {
    const header = mountHeader()

    expect(header.get('[data-test="catalog-link"] [data-test="label"]').text()).toBe('Bestellung aufnehmen')
    expect(header.get('[data-test="open-items-link"] [data-test="label"]').text()).toBe('Offene Posten')
  })

  it('carries an icon on every destination, so the row fits without hiding a word', () => {
    const header = mountHeader()

    expect(header.get('[data-test="catalog-link"] .v-icon').exists()).toBe(true)
    expect(header.get('[data-test="open-items-link"] .v-icon').exists()).toBe(true)
  })
})

describe('the destination the waiter is on', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('marks the ordering screen with the action colour and leaves the other destination quiet', () => {
    navigate('/')
    const header = mountHeader()

    expect(header.get('[data-test="catalog-link"]').classes()).toContain('text-primary')
    expect(header.get('[data-test="open-items-link"]').classes()).toContain('text-medium-emphasis')
  })

  it('moves the mark to the open items once the waiter goes there', async () => {
    navigate('/')
    const header = mountHeader()

    navigate('/open-items')
    await nextTick()

    expect(header.get('[data-test="open-items-link"]').classes()).toContain('text-primary')
    expect(header.get('[data-test="catalog-link"]').classes()).toContain('text-medium-emphasis')
  })

  it('counts the review screen as the ordering destination', () => {
    navigate('/review')
    const header = mountHeader()

    expect(header.get('[data-test="catalog-link"]').classes()).toContain('text-primary')
    expect(header.get('[data-test="open-items-link"]').classes()).toContain('text-medium-emphasis')
  })
})

describe('the way to the ordering screen while a category is open on it', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubLaptop().answersEverythingElse(answer({}))
    navigate('/')
  })

  it('closes the open category, so the tap is answered instead of doing nothing', async () => {
    let timesClosed = 0
    registerOpenStepCloser(() => {
      timesClosed += 1
    })
    const header = mountHeader()

    await header.get('[data-test="catalog-link"]').trigger('click')

    expect(timesClosed).toBe(1)
    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})
