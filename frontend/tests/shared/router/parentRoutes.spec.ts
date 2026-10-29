import { describe, expect, it } from 'vitest'
import { isSameRoute, parentRouteOf, type AppRoute } from '../../../src/shared/router/route'

interface ParentRow {
  screen: string
  route: AppRoute
  parent: AppRoute | null
}

const FESTIVAL_LIST: AppRoute = { name: 'admin', section: 'festivals', festivalId: null }

describe('the screen one level up', () => {
  it.each<ParentRow>([
    { screen: 'the waiter home screen', route: { name: 'home' }, parent: null },
    { screen: 'the station screen', route: { name: 'stations' }, parent: null },
    { screen: 'an invitation', route: { name: 'enrolQr', code: 'abc123' }, parent: null },
    { screen: 'the order screen', route: { name: 'catalog' }, parent: { name: 'home' } },
    { screen: 'the settle view', route: { name: 'openItems' }, parent: { name: 'home' } },
    { screen: 'the orders of a table', route: { name: 'orderOverview' }, parent: { name: 'home' } },
    { screen: 'the summary', route: { name: 'review' }, parent: { name: 'catalog' } },
    { screen: 'the festival list', route: FESTIVAL_LIST, parent: null },
    { screen: 'the overview', route: { name: 'admin', section: 'overview', festivalId: null }, parent: FESTIVAL_LIST },
    { screen: 'the articles', route: { name: 'admin', section: 'items', festivalId: null }, parent: FESTIVAL_LIST },
    { screen: 'the waiters', route: { name: 'admin', section: 'staff', festivalId: null }, parent: FESTIVAL_LIST },
    {
      screen: 'the page of one festival',
      route: { name: 'admin', section: 'festivals', festivalId: 'fest-sommer' },
      parent: FESTIVAL_LIST,
    },
    {
      screen: 'the editor of one area',
      route: { name: 'admin', section: 'festivals', festivalId: 'fest-sommer', areaId: 'area-halle' },
      parent: { name: 'admin', section: 'festivals', festivalId: 'fest-sommer' },
    },
  ])('of $screen is the one the table names', ({ route, parent }) => {
    expect(parentRouteOf(route)).toEqual(parent)
  })
})

describe('two routes', () => {
  it('are the same screen when they name the same festival', () => {
    expect(
      isSameRoute(
        { name: 'admin', section: 'festivals', festivalId: 'fest-sommer' },
        { name: 'admin', section: 'festivals', festivalId: 'fest-sommer' },
      ),
    ).toBe(true)
  })

  it('are different screens when they name different festivals', () => {
    expect(
      isSameRoute(
        { name: 'admin', section: 'festivals', festivalId: 'fest-sommer' },
        { name: 'admin', section: 'festivals', festivalId: 'fest-herbst' },
      ),
    ).toBe(false)
  })
})
