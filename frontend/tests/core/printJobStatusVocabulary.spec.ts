import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

const BACKEND_ENUM = `${process.cwd()}/../backend/GastronomyApp.Core/Enums/PrintJobStatus.cs`
const CLIENT_TYPES = `${process.cwd()}/src/core/apiTypes.ts`

function statusesTheLaptopCanSend(): string[] {
  const source = readFileSync(BACKEND_ENUM, 'utf8')
  const body = source.slice(source.indexOf('{'), source.lastIndexOf('}'))
  return [...body.matchAll(/(\w+)\s*=\s*\d+/g)].map((match) => match[1]).sort()
}

function statusesThePhoneKnows(): string[] {
  const lines = readFileSync(CLIENT_TYPES, 'utf8').split('\n')
  const declaration = lines.findIndex((line) => line.startsWith('export type PrintJobStatus ='))
  const union: string[] = []
  for (const line of lines.slice(declaration + 1)) {
    const member = line.match(/\|\s*'(\w+)'/)
    if (member === null) {
      break
    }
    union.push(member[1])
  }
  return union.sort()
}

describe('the print job statuses the phone knows', () => {
  it('are exactly the ones the laptop can send, because an unknown one crashes the station screen', () => {
    expect(statusesThePhoneKnows()).toEqual(statusesTheLaptopCanSend())
  })

  it('are actually found by this check, so it cannot pass by finding nothing', () => {
    expect(statusesTheLaptopCanSend().length).toBeGreaterThan(4)
    expect(statusesThePhoneKnows().length).toBeGreaterThan(4)
  })
})
