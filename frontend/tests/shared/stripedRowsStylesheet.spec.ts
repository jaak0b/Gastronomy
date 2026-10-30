import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { stripeClassesOf } from '../../src/shared/core/stripedRows'

const STRIPED_ROWS_STYLESHEET = `${process.cwd()}/src/shared/styles/stripedRows.css`

interface Rule {
  selector: string
  declarations: string
}

function rulesOfTheStylesheet(): Rule[] {
  const stylesheet = readFileSync(STRIPED_ROWS_STYLESHEET, 'utf8')
  return [...stylesheet.matchAll(/([^{}]+)\{([^}]*)\}/g)].map(([, selector, body]) => ({
    selector: selector.trim(),
    declarations: body.replace(/\s+/g, ' ').trim(),
  }))
}

function declarationsOf(selector: string): string[] {
  return rulesOfTheStylesheet()
    .filter((rule) => rule.selector === selector)
    .map((rule) => rule.declarations)
}

describe('the stylesheet of the striped rows', () => {
  it('holds two rules, one for each stripe, and no other', () => {
    const rules = rulesOfTheStylesheet()

    expect(rules).toHaveLength(2)
    expect(rules.map((rule) => rule.selector)).toEqual(['.striped-row', '.striped-row.striped-row-strong'])
  })

  it('paints a striped row with the light tint of the theme and nothing else', () => {
    expect(declarationsOf('.striped-row')).toEqual([
      'background-color: rgba(var(--v-theme-on-surface), var(--v-stripe-opacity));',
    ])
  })

  it('paints a row that is also strong with the stronger tint of the theme and nothing else', () => {
    expect(declarationsOf('.striped-row.striped-row-strong')).toEqual([
      'background-color: rgba(var(--v-theme-on-surface), var(--v-stripe-strong-opacity));',
    ])
  })

  it.each(Object.keys(stripeClassesOf(1)))('styles the class %s that a row carries, spelled exactly', (className) => {
    const classesInTheSelectors = rulesOfTheStylesheet().flatMap((rule) =>
      rule.selector.split('.').filter((part) => part !== ''),
    )

    expect(classesInTheSelectors).toContain(className)
  })
})
