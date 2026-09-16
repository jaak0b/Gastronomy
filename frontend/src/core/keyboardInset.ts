export function keyboardInsetPx(layoutViewportHeight: number, visualViewportHeight: number): number {
  return Math.max(0, Math.round(layoutViewportHeight - visualViewportHeight))
}
