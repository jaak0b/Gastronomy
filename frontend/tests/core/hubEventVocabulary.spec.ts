import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { sourceFilesUnder } from '../support/sourceFiles'

const BACKEND_HUB = `${process.cwd()}/../backend/GastronomyApp.Api/Hub/GastronomyHub.cs`
const SOURCE_ROOT = `${process.cwd()}/src`

function eventsTheLaptopCanPush(): string[] {
  const source = readFileSync(BACKEND_HUB, 'utf8')
  const declaration = source.indexOf('public sealed record HubEventNames')
  const body = source.slice(declaration, source.indexOf('\n}', declaration))
  return [...body.matchAll(/=\s*"(\w+)";/g)].map((match) => match[1]).sort()
}

function eventsThePhoneWaitsFor(): Map<string, string> {
  const waited = new Map<string, string>()
  for (const file of sourceFilesUnder(SOURCE_ROOT)) {
    const source = readFileSync(file, 'utf8')
    for (const match of source.matchAll(/onEvent(?:<[\s\S]*?>)?\s*\(\s*'(\w+)'/g)) {
      waited.set(match[1], file.slice(SOURCE_ROOT.length + 1))
    }
  }
  return waited
}

describe('every event the phone waits for', () => {
  it('is one the laptop can push, because a name nobody sends never arrives', () => {
    const pushed = eventsTheLaptopCanPush()

    const unknown = [...eventsThePhoneWaitsFor().entries()]
      .filter(([name]) => !pushed.includes(name))
      .map(([name, file]) => `${name} (${file})`)

    expect(unknown).toEqual([])
  })

  it('is found by this check, so it cannot pass by finding nothing', () => {
    const waited = eventsThePhoneWaitsFor()
    const pushed = eventsTheLaptopCanPush()

    expect(waited.has('CatalogChanged')).toBe(true)
    expect(pushed).toContain('CatalogChanged')
    expect(waited.size).toBeGreaterThan(5)
    expect(pushed.length).toBeGreaterThan(7)
  })
})
