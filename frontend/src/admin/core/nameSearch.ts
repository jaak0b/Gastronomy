export function nameMatchesTheSearch(name: string, search: string): boolean {
  return name.toLowerCase().includes(search.trim().toLowerCase())
}
