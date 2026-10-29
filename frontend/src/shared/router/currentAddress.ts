export function currentAddress(): string {
  return window.location.pathname + window.location.search + window.location.hash
}
