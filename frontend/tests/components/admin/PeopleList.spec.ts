import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PeopleList from '../../../src/components/admin/people/PeopleList.vue'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

const PERSON_ID = '33333333-3333-3333-3333-333333333333'

const ONE_PERSON = {
  people: [
    {
      serverPersonId: PERSON_ID,
      name: 'Anna',
      isActive: true,
      hasDevice: true,
      lastSeenAtUtc: '2026-08-27T19:00:00Z',
      userAgent: 'Android',
      hasOutstandingInvitation: false,
    },
  ],
}

function stubFetch(revokeStatus = 200) {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      urls.push(url)
      if (url.endsWith('/revoke-device')) {
        return new Response(
          JSON.stringify({ code: 'NotFound', messageKey: 'admin.personHasNoPhone' }),
          { status: revokeStatus },
        )
      }
      return new Response(JSON.stringify(ONE_PERSON), { status: 200 })
    }),
  )
  return urls
}

function mountList() {
  return mount(PeopleList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

async function firstPerson(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('.person-row').exists()).toBe(true))
}

const OFF_THE_LIST = {
  people: [{ ...ONE_PERSON.people[0], isActive: false }],
}

function stubFetchWith(people: unknown) {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      return new Response(JSON.stringify(people), { status: 200 })
    }),
  )
  return urls
}

describe('taking a server person off the list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks before it happens', async () => {
    const urls = stubFetchWith(ONE_PERSON)

    const list = mountList()
    await firstPerson(list)
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('says that the orders already placed are kept', async () => {
    stubFetchWith(ONE_PERSON)

    const list = mountList()
    await firstPerson(list)
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(document.querySelector('.confirm-body')!.textContent).toContain('bleiben gespeichert')
  })

  it('takes them off the list once the question is answered with yes', async () => {
    const urls = stubFetchWith(ONE_PERSON)

    const list = mountList()
    await firstPerson(list)
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/server-people/${PERSON_ID}/deactivate`),
    )
  })
})

describe('a server person taken off the list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.show-deactivated').exists()).toBe(true))

    expect(list.find('.person-row').exists()).toBe(false)
  })

  it('says so on their row once it is shown', async () => {
    stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await firstPerson(list)

    expect(list.get('.deactivated').text()).toBe('Deaktiviert')
  })

  it('puts them back on the list without asking a question first', async () => {
    const urls = stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await list.get('.show-deactivated input').setValue(true)
    await firstPerson(list)
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/server-people/${PERSON_ID}/activate`),
    )
  })
})

describe('a server person in the admin list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is shown as having a phone when the laptop says a device is enrolled', async () => {
    stubFetch()

    const list = mountList()
    await firstPerson(list)

    expect(list.find('.no-phone').exists()).toBe(false)
  })

  it('offers the revoke button once a phone is enrolled', async () => {
    stubFetch()

    const list = mountList()
    await firstPerson(list)

    expect(list.find('.revoke').exists()).toBe(true)
  })

  it('is deactivated at their own address', async () => {
    const urls = stubFetch()

    const list = mountList()
    await firstPerson(list)
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/server-people/${PERSON_ID}/deactivate`),
    )
  })

  it('is not reported as revoked when the laptop refused the revocation', async () => {
    stubFetch(409)

    const list = mountList()
    await firstPerson(list)
    await list.get('.revoke').trigger('click')
    await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))

    expect(list.find('.revoked').exists()).toBe(false)
  })

  it('says out loud that the revocation failed', async () => {
    stubFetch(409)

    const list = mountList()
    await firstPerson(list)
    await list.get('.revoke').trigger('click')

    await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
  })
})
