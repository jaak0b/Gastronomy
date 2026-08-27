export function assertNever(value: never): never {
  throw new Error(`Unhandled union member: ${String(value)}`)
}
