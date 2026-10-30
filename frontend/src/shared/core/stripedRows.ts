export function stripeClassesOf(position: number): Record<string, boolean> {
  return { 'striped-row': true, 'striped-row-strong': position % 2 === 1 }
}
