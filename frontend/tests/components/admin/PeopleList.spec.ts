import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import PeopleList from '../../../src/components/admin/people/PeopleList.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

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
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(PeopleList, { global: { plugins: [i18n] } })
}

async function firstPerson(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
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

describe('a server person taken off the list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says so on their row', async () => {
    stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await firstPerson(list)

    expect(list.get('.off-the-list').text()).toBe('Nicht in der Liste')
  })

  it('offers to put them back on the list', async () => {
    stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await firstPerson(list)

    expect(list.get('.toggle-active').text()).toBe('Bedienung wieder in die Liste')
  })

  it('puts them back on the list at their own address', async () => {
    const urls = stubFetchWith(OFF_THE_LIST)

    const list = mountList()
    await firstPerson(list)
    await list.get('.toggle-active').trigger('click')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/server-people/${PERSON_ID}/activate`),
    )
  })

  it('offers to take an active person off the list', async () => {
    stubFetchWith(ONE_PERSON)

    const list = mountList()
    await firstPerson(list)

    expect(list.get('.toggle-active').text()).toBe('Bedienung aus der Liste nehmen')
  })
})

describe('a server person in the admin list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
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
    await list.get('.toggle-active').trigger('click')

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
