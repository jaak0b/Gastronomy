import { z } from 'zod'

export interface ApiErrorBody {
  code: string
  messageKey: string
  parameters: Record<string, string | number>
  details: string | null
}

const apiErrorBodySchema: z.ZodType<ApiErrorBody> = z.object({
  code: z.string(),
  messageKey: z.string(),
  parameters: z.record(z.string(), z.union([z.string(), z.number()])),
  details: z.string().nullable(),
})

export function isApiErrorBody(value: unknown): value is ApiErrorBody {
  return apiErrorBodySchema.safeParse(value).success
}
