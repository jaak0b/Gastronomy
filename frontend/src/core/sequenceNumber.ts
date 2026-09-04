export function formatSequenceNumber(stationOrderNumber: number): string {
  return stationOrderNumber.toString().padStart(3, '0')
}
