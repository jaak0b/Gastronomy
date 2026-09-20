const LOOKUP_ID_SEPARATOR = '.'

export function deviceTokenIsWellFormed(token: string): boolean {
  const separatorIndex = token.indexOf(LOOKUP_ID_SEPARATOR)
  return separatorIndex > 0 && separatorIndex < token.length - 1
}
