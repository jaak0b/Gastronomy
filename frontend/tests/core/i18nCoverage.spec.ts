import { readFileSync, readdirSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import de from '../../src/locales/de.json'
import en from '../../src/locales/en.json'

type LocaleTree = { [key: string]: string | LocaleTree }

const PLURAL_KEYS = [
  'header.attention',
  'header.stationWaiting',
  'catalog.basketSummary',
  'admin.overview.itemsWithoutLocation',
  'admin.overview.openTickets',
  'admin.overview.stationBlocked',
  'admin.locations.openTickets',
  'admin.printers.waiting',
  'admin.itemsWouldHaveNoStation',
]

function flatten(tree: LocaleTree, prefix = ''): Map<string, string> {
  const flat = new Map<string, string>()
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix === '' ? key : `${prefix}.${key}`
    if (typeof value === 'string') {
      flat.set(path, value)
    } else {
      for (const [nested, nestedValue] of flatten(value, path)) {
        flat.set(nested, nestedValue)
      }
    }
  }
  return flat
}

function placeholders(value: string): string[] {
  return [...value.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort()
}

const german = flatten(de as LocaleTree)
const english = flatten(en as LocaleTree)

describe('the two locale files', () => {
  it('carry the same keys, because a key in one language only is an unfinished change', () => {
    expect([...german.keys()].sort()).toEqual([...english.keys()].sort())
  })

  it('carry the same placeholders in every key', () => {
    const differing = [...german.entries()]
      .filter(([key, value]) => {
        const other = english.get(key)
        return other === undefined || placeholders(value).join() !== placeholders(other).join()
      })
      .map(([key]) => key)

    expect(differing).toEqual([])
  })

  it('leaves no string empty in either language', () => {
    const empty = [...german.entries(), ...english.entries()]
      .filter(([, value]) => value.trim().length === 0)
      .map(([key]) => key)

    expect(empty).toEqual([])
  })

  it('writes no em-dash anywhere in either language', () => {
    const withEmDash = [...german.entries(), ...english.entries()]
      .filter(([, value]) => value.includes(String.fromCharCode(0x2014)))
      .map(([key]) => key)

    expect(withEmDash).toEqual([])
  })
})

describe('the keys that carry a count', () => {
  it('exist in both languages', () => {
    const missing = PLURAL_KEYS.filter((key) => !german.has(key) || !english.has(key))

    expect(missing).toEqual([])
  })

  it('offer a singular form beside the general one in German', () => {
    const withoutSingular = PLURAL_KEYS.filter(
      (key) => (german.get(key) as string).split('|').length !== 2,
    )

    expect(withoutSingular).toEqual([])
  })

  it('offer a singular form beside the general one in English', () => {
    const withoutSingular = PLURAL_KEYS.filter(
      (key) => (english.get(key) as string).split('|').length !== 2,
    )

    expect(withoutSingular).toEqual([])
  })

  it('are the only keys written as plural forms', () => {
    const pipedKeys = [...german.entries()]
      .filter(([, value]) => value.includes('|'))
      .map(([key]) => key)
      .sort()

    expect(pipedKeys).toEqual([...PLURAL_KEYS].sort())
  })
})

const SOURCE_ROOT = `${process.cwd()}/src`

function sourceFiles(directory: string): string[] {
  const found: string[] = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const full = `${directory}/${entry.name}`
    if (entry.isDirectory()) {
      found.push(...sourceFiles(full))
    } else if (entry.name.endsWith('.ts') || entry.name.endsWith('.vue')) {
      found.push(full)
    }
  }
  return found
}

function keysUsedInSource(): Map<string, string> {
  const used = new Map<string, string>()
  for (const file of sourceFiles(SOURCE_ROOT)) {
    const source = readFileSync(file, 'utf8')
    for (const match of source.matchAll(/\$?\bt\(\s*'([a-zA-Z][\w.]*)'/g)) {
      used.set(match[1], file.slice(SOURCE_ROOT.length + 1))
    }
  }
  return used
}

describe('every key a screen asks for', () => {
  it('exists in German', () => {
    const missing = [...keysUsedInSource().entries()]
      .filter(([key]) => !german.has(key))
      .map(([key, file]) => `${key} (${file})`)

    expect(missing).toEqual([])
  })

  it('exists in English', () => {
    const missing = [...keysUsedInSource().entries()]
      .filter(([key]) => !english.has(key))
      .map(([key, file]) => `${key} (${file})`)

    expect(missing).toEqual([])
  })

  it('finds the keys the screens actually use, so the check cannot pass by finding nothing', () => {
    const used = keysUsedInSource()

    expect(used.has('review.send')).toBe(true)
    expect(used.size).toBeGreaterThan(100)
  })
})

const BACKEND_ROOT = `${process.cwd()}/../backend`

function backendSourceFiles(directory: string): string[] {
  const found: string[] = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.name === 'bin' || entry.name === 'obj' || entry.name.endsWith('.Tests')) {
      continue
    }
    const full = `${directory}/${entry.name}`
    if (entry.isDirectory()) {
      found.push(...backendSourceFiles(full))
    } else if (entry.name.endsWith('.cs')) {
      found.push(full)
    }
  }
  return found
}

function keysTheLaptopCanSend(): Map<string, string> {
  const sent = new Map<string, string>()
  for (const file of backendSourceFiles(BACKEND_ROOT)) {
    const source = readFileSync(file, 'utf8')
    for (const match of source.matchAll(
      /"((?:order|admin|ticket|enrolment|station|session|review)\.[a-zA-Z][\w.]*)"/g,
    )) {
      sent.set(match[1], file.slice(BACKEND_ROOT.length + 1))
    }
  }
  return sent
}

describe('every message the laptop can send back', () => {
  it('has German wording, because a raw key on a phone helps nobody', () => {
    const missing = [...keysTheLaptopCanSend().entries()]
      .filter(([key]) => !german.has(key))
      .map(([key, file]) => `${key} (${file})`)

    expect(missing).toEqual([])
  })

  it('has English wording too', () => {
    const missing = [...keysTheLaptopCanSend().entries()]
      .filter(([key]) => !english.has(key))
      .map(([key, file]) => `${key} (${file})`)

    expect(missing).toEqual([])
  })

  it('is actually found by this check, so it cannot pass by finding nothing', () => {
    const sent = keysTheLaptopCanSend()

    expect(sent.has('enrolment.codeUnknown')).toBe(true)
    expect(sent.size).toBeGreaterThan(30)
  })
})
