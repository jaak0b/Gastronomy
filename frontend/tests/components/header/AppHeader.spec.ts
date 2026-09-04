import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '../../../src/components/header/AppHeader.vue'
import { currentRoute, navigate } from '../../../src/router'
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
    expect(header.get('.stations-link .label').text()).toBe('Ausgabestellen')
  })

  it('carries an icon on every destination, so the row fits without hiding a word', () => {
    const header = mountHeader()

    expect(header.get('.catalog-link .v-icon').exists()).toBe(true)
    expect(header.get('.stations-link .v-icon').exists()).toBe(true)
  })
})
