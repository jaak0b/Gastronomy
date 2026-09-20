export interface LatestRequestGate {
  start(): number
  isCurrent(token: number): boolean
}

export function createLatestRequestGate(): LatestRequestGate {
  let latestToken = 0
  return {
    start(): number {
      latestToken += 1
      return latestToken
    },
    isCurrent(token: number): boolean {
      return token === latestToken
    },
  }
}
