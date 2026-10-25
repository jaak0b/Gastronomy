const UPPER_A = 65
const UPPER_Z = 90
const CASE_OFFSET = 32

function foldedCodeUnit(text: string, index: number): number {
  const codeUnit = text.charCodeAt(index)
  return codeUnit >= UPPER_A && codeUnit <= UPPER_Z ? codeUnit + CASE_OFFSET : codeUnit
}

export function compareNames(left: string, right: string): number {
  const shared = Math.min(left.length, right.length)
  for (let index = 0; index < shared; index++) {
    const difference = foldedCodeUnit(left, index) - foldedCodeUnit(right, index)
    if (difference !== 0) {
      return difference
    }
  }
  return left.length - right.length
}
