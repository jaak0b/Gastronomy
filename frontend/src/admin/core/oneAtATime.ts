interface OneAtATime {
  run: <TResult>(action: () => Promise<TResult>) => Promise<TResult>
}

export function oneAtATime(): OneAtATime {
  let latest: Promise<unknown> = Promise.resolve()

  function run<TResult>(action: () => Promise<TResult>): Promise<TResult> {
    const next = latest.then(action, action)
    latest = next
    return next
  }

  return { run }
}
