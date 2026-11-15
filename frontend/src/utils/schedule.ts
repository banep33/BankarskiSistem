import type { StandingOrderFrequency } from '@/types/api'

function fromIso(value: string): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(Date.UTC(year!, month! - 1, day!))
}

function toIso(date: Date): string {
  return date.toISOString().slice(0, 10)
}

function addMonthsClamped(start: Date, months: number): Date {
  const target = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + months, 1))
  const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate()
  target.setUTCDate(Math.min(start.getUTCDate(), lastDay))
  return target
}

export function occurrence(startDate: string, frequency: StandingOrderFrequency, index: number): string {
  const start = fromIso(startDate)
  if (frequency === 'Nedeljno') return toIso(new Date(start.getTime() + index * 7 * 86_400_000))
  const months = frequency === 'Mesecno' ? index : frequency === 'Kvartalno' ? index * 3 : index * 12
  return toIso(addMonthsClamped(start, months))
}

export function previewDates(
  startDate: string,
  frequency: StandingOrderFrequency,
  endDate: string | null,
  maxExecutions: number | null,
  count: number,
): string[] {
  const result: string[] = []
  for (let index = 0; result.length < count; index++) {
    const date = occurrence(startDate, frequency, index)
    if ((endDate && date > endDate) || (maxExecutions !== null && index >= maxExecutions)) break
    result.push(date)
  }
  return result
}

export function totalPlannedExecutions(startDate: string, frequency: StandingOrderFrequency, endDate: string | null, maxExecutions: number | null): number | null {
  if (maxExecutions !== null) return maxExecutions
  if (!endDate) return null
  return previewDates(startDate, frequency, endDate, null, 601).length
}
