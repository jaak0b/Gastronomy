import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { sourceFilesUnder } from '../support/sourceFiles'

const BACKEND_HUB = `${process.cwd()}/../backend/GastronomyApp.Api/Names.cs`
const SOURCE_ROOT = `${process.cwd()}/src`

function eventsTheLaptopCanPush(): string[] {
  const source = readFileSync(BACKEND_HUB, 'utf8')
  const declaration = source.indexOf('public static class HubEvents')
  const nextClass = source.indexOf('public static class', declaration + 1)
  const body = nextClass < 0 ? source.substring(declaration) : source.substring(declaration, nextClass)
  return [...body.matchAll(/=\s*"(\w+)";/g)].map((match) => match[1]).sort()
}

function eventsThePhoneWaitsFor(): Map<string, string> {
  const waited = new Map<string, string>()
  for (const file of sourceFilesUnder(SOURCE_ROOT)) {
    const source = readFileSync(file, 'utf8')
    for (const match of source.matchAll(/onEvent(?:<[\s\S]*?>)?\s*\(\s*'(\w+)'/g)) {
      waited.set(match[1], file.substring(SOURCE_ROOT.length + 1))
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

  it('covers every event the laptop pushes, so a store that stops listening is caught', () => {
    const pushed = eventsTheLaptopCanPush()

    expect(pushed).toEqual(['ConfigurationChanged', 'DeviceRevoked', 'EnrolmentCompleted', 'OrdersChanged'])
    expect(new Set(eventsThePhoneWaitsFor().keys())).toEqual(new Set(pushed))
  })
})
