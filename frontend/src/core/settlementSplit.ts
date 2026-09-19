export interface SplitSettlementLine {
  paidPriceCents: number
  paymentNotice: string | null
}

function noticeFor(reason: string, paidPriceCents: number, unitPriceCents: number): string | null {
  const written = reason.trim()
  if (written === '' || paidPriceCents >= unitPriceCents) {
    return null
  }
  return written
}

function equalShares(amountPaidCents: number, lineCount: number): number[] {
  const base = Math.floor(amountPaidCents / lineCount)
  const extra = amountPaidCents - base * lineCount
  return Array.from({ length: lineCount }, (_, index) => (index < extra ? base + 1 : base))
}

function proportionalShares(
  amountPaidCents: number,
  lines: { unitPriceCents: number }[],
  totalUnitPriceCents: number,
): number[] {
  const shares = lines.map((line) =>
    Math.floor((amountPaidCents * line.unitPriceCents) / totalUnitPriceCents),
  )
  let remaining = amountPaidCents - shares.reduce((total, share) => total + share, 0)
  for (let index = 0; remaining > 0; index += 1) {
    shares[index % shares.length] += 1
    remaining -= 1
  }
  return shares
}

export function splitSettlement(
  amountPaidCents: number,
  lines: { unitPriceCents: number }[],
  reason: string,
): SplitSettlementLine[] {
  if (lines.length === 0) {
    return []
  }
  const totalUnitPriceCents = lines.reduce((total, line) => total + line.unitPriceCents, 0)
  const shares =
    totalUnitPriceCents === 0
      ? equalShares(amountPaidCents, lines.length)
      : proportionalShares(amountPaidCents, lines, totalUnitPriceCents)
  return shares.map((paidPriceCents, index) => ({
    paidPriceCents,
    paymentNotice: noticeFor(reason, paidPriceCents, lines[index].unitPriceCents),
  }))
}
