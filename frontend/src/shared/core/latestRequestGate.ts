export interface LatestRequestGate {
  startRequest(): number
  isNewestRequest(token: number): boolean
}

export function createLatestRequestGate(): LatestRequestGate {
  let latestToken = 0
  return {
    startRequest(): number {
      latestToken += 1
      return latestToken
    },
    isNewestRequest(token: number): boolean {
      return token === latestToken
    },
  }
}
