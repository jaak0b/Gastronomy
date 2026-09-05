import { readFileSync, readdirSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { sourceFilesUnder } from '../support/sourceFiles'

const BACKEND_ROOT = `${process.cwd()}/../backend/GastronomyApp.Api`
const SOURCE_ROOT = `${process.cwd()}/src`
const ANY_SEGMENT = '*'

interface Route {
  method: string
  segments: string[]
}

interface CalledPath {
  path: string
  segments: string[]
  file: string
}

interface CalledRoute extends CalledPath {
  method: string
}

function segmentsOf(path: string): string[] {
  return path
    .split('/')
    .filter((segment) => segment.length > 0)
    .map((segment) => (segment.startsWith('{') || segment.includes('${') ? ANY_SEGMENT : segment))
}

function backendSourceFiles(directory: string): string[] {
  const found: string[] = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (entry.name === 'bin' || entry.name === 'obj' || entry.name === 'wwwroot') {
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

function routesTheLaptopServes(): Route[] {
  const routes: Route[] = []
  for (const file of backendSourceFiles(BACKEND_ROOT)) {
    const source = readFileSync(file, 'utf8')
    const group = source.match(/var group = [\w.]+\.MapGroup\("([^"]*)"\)/)
    const prefix = group === null ? '' : group[1]
    for (const match of source.matchAll(
      /(\w+)\.Map(Get|Post|Put|Delete)\(\s*(?:string\.Empty|"([^"]*)")/g,
    )) {
      const relative = match[3] ?? ''
      const full = match[1] === 'group' ? `${prefix}${relative}` : relative
      routes.push({ method: match[2].toUpperCase(), segments: segmentsOf(full) })
    }
  }
  return routes
}

function parenthesisedRanges(source: string): { start: number; end: number }[] {
  const ranges: { start: number; end: number }[] = []
  const open: number[] = []
  for (let index = 0; index < source.length; index++) {
    if (source[index] === '(') {
      open.push(index)
    } else if (source[index] === ')' && open.length > 0) {
      ranges.push({ start: open.pop() as number, end: index })
    }
  }
  return ranges
}

function methodsIn(text: string): string[] {
  return [...text.matchAll(/'(GET|POST|PUT|DELETE)'/g)].map((match) => match[1])
}

function pathsThePhoneCalls(): CalledPath[] {
  const called: CalledPath[] = []
  for (const file of sourceFilesUnder(SOURCE_ROOT)) {
    const source = readFileSync(file, 'utf8')
    for (const match of source.matchAll(/['`](\/api\/[^'`]*)['`]/g)) {
      called.push({
        path: match[1],
        segments: segmentsOf(match[1]),
        file: file.slice(SOURCE_ROOT.length + 1),
      })
    }
  }
  return called
}

function routesThePhoneCalls(): CalledRoute[] {
  const called: CalledRoute[] = []
  for (const file of sourceFilesUnder(SOURCE_ROOT)) {
    const source = readFileSync(file, 'utf8')
    const ranges = parenthesisedRanges(source)
    for (const match of source.matchAll(/['`](\/api\/[^'`]*)['`]/g)) {
      const at = match.index as number
      const enclosing = ranges
        .filter((range) => range.start < at && at < range.end)
        .sort((left, right) => left.end - left.start - (right.end - right.start))[0]
      if (enclosing === undefined) {
        continue
      }
      const callText = source.slice(enclosing.start, enclosing.end)
      const methods = methodsIn(callText)
      if (methods.length > 1 || (methods.length === 0 && callText.includes('method'))) {
        continue
      }
      called.push({
        path: match[1],
        segments: segmentsOf(match[1]),
        method: methods[0] ?? 'GET',
        file: file.slice(SOURCE_ROOT.length + 1),
      })
    }
  }
  return called
}

function matches(called: string[], served: string[]): boolean {
  return (
    called.length === served.length
    && called.every(
      (segment, position) =>
        segment === ANY_SEGMENT || served[position] === ANY_SEGMENT || segment === served[position],
    )
  )
}

describe('every address the phone asks the laptop for', () => {
  it('is one the laptop answers on, because an unknown address is a lost order', () => {
    const served = routesTheLaptopServes()

    const unanswered = pathsThePhoneCalls()
      .filter((call) => !served.some((route) => matches(call.segments, route.segments)))
      .map((call) => `${call.path} (${call.file})`)

    expect(unanswered).toEqual([])
  })

  it('is asked for with a method the laptop accepts there', () => {
    const served = routesTheLaptopServes()

    const refused = routesThePhoneCalls()
      .filter(
        (call) =>
          !served.some(
            (route) => route.method === call.method && matches(call.segments, route.segments),
          ),
      )
      .map((call) => `${call.method} ${call.path} (${call.file})`)

    expect(refused).toEqual([])
  })

  it('is found by this check, so it cannot pass by finding nothing', () => {
    const served = routesTheLaptopServes()
    const calls = routesThePhoneCalls()

    expect(served.length).toBeGreaterThan(25)
    expect(pathsThePhoneCalls().length).toBeGreaterThan(15)
    expect(calls.length).toBeGreaterThan(10)
    expect(
      calls.some((call) => call.method === 'POST' && call.path === '/api/orders'),
    ).toBe(true)
    expect(
      served.some(
        (route) => route.method === 'POST' && matches(['api', 'orders'], route.segments),
      ),
    ).toBe(true)
  })
})
