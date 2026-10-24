import type { AdminAreaView } from '../../shared/api/generatedSchemas'

export function sortOrderForANewArea(areas: readonly AdminAreaView[]): number {
  return Math.max(0, ...areas.map((area) => area.sortOrder)) + 1
}
