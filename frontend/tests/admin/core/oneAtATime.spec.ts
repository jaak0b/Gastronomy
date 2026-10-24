import { describe, expect, it } from 'vitest'
import { oneAtATime } from '../../../src/admin/core/oneAtATime'

function deferred(): { promise: Promise<string>; resolve: (value: string) => void; reject: (reason: Error) => void } {
  let resolve: (value: string) => void = () => undefined
  let reject: (reason: Error) => void = () => undefined
  const promise = new Promise<string>((onResolve, onReject) => {
    resolve = onResolve
    reject = onReject
  })
  return { promise, resolve, reject }
}

describe('actions run one at a time', () => {
  it('starts the second action only once the first has finished', async () => {
    const turns = oneAtATime()
    const first = deferred()
    const started: string[] = []

    const firstDone = turns.run(async () => {
      started.push('first')
      return await first.promise
    })
    const secondDone = turns.run(async () => {
      started.push('second')
      return 'second'
    })
    await Promise.resolve()
    const startedBeforeTheFirstFinished = [...started]
    first.resolve('first')

    expect([startedBeforeTheFirstFinished, await firstDone, await secondDone, started]).toEqual([
      ['first'],
      'first',
      'second',
      ['first', 'second'],
    ])
  })

  it('still runs the next action when the one before it failed', async () => {
    const turns = oneAtATime()
    const failing = deferred()

    const failed = turns.run(async () => await failing.promise)
    const next = turns.run(async () => 'next')
    failing.reject(new Error('lost'))

    await expect(failed).rejects.toThrow('lost')
    expect(await next).toBe('next')
  })
})
