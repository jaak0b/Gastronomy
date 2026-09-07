import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '../../../src/components/header/AppHeader.vue'
import { currentRoute, navigate, openAStepInsideTheScreen } from '../../../src/router'
import { testPlugins } from '../../support/plugins'

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
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
    navigate('/stations')
  })

  it('offers the ordering screen in the row of buttons', async () => {
    const header = mountHeader()

    await header.get('.catalog-link').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

describe('the settings control', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('is named for a screen reader even though it carries an icon', () => {
    const header = mountHeader()

    expect(header.get('.settings').attributes('aria-label')).toBe('Einstellungen')
  })
})

describe('the row of destinations', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('names every destination in words, under an icon that fits a phone', () => {
    const header = mountHeader()

    expect(header.get('.catalog-link .label').text()).toBe('Bestellung aufnehmen')
    expect(header.get('.open-items-link .label').text()).toBe('Offene Posten')
  })

  it('carries an icon on every destination, so the row fits without hiding a word', () => {
    const header = mountHeader()

    expect(header.get('.catalog-link .v-icon').exists()).toBe(true)
    expect(header.get('.open-items-link .v-icon').exists()).toBe(true)
  })
})

describe('the way to the ordering screen while a category is open on it', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
    navigate('/')
  })

  it('closes the open category, so the tap is answered instead of doing nothing', async () => {
    let timesClosed = 0
    openAStepInsideTheScreen(() => {
      timesClosed += 1
    })
    const header = mountHeader()

    await header.get('.catalog-link').trigger('click')

    expect(timesClosed).toBe(1)
    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})
