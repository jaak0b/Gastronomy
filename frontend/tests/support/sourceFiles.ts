import { readdirSync } from 'node:fs'

export function sourceFilesUnder(directory: string): string[] {
  const found: string[] = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const full = `${directory}/${entry.name}`
    if (entry.isDirectory()) {
      found.push(...sourceFilesUnder(full))
    } else if (entry.name.endsWith('.ts') || entry.name.endsWith('.vue')) {
      found.push(full)
    }
  }
  return found
}
