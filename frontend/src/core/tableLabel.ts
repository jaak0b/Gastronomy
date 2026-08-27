export function isTableLabelValid(label: string): boolean {
  return label.trim().length > 0
}

export function suggestionMatches(query: string, suggestions: string[]): string[] {
  const needle = query.trim().toLowerCase()
  if (needle.length === 0) {
    return [...suggestions]
  }
  return suggestions.filter((suggestion) => suggestion.toLowerCase().includes(needle))
}
