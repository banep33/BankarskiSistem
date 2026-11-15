const LOCALE = 'sr-Latn-RS'

const moneyFormatters = new Map<string, Intl.NumberFormat>()

export function formatMoney(amount: number, currency = 'RSD'): string {
  let formatter = moneyFormatters.get(currency)
  if (!formatter) {
    formatter = new Intl.NumberFormat(LOCALE, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
    moneyFormatters.set(currency, formatter)
  }
  return `${formatter.format(amount)} ${currency}`
}

export function formatSignedMoney(amount: number, currency: string, isIncome: boolean): string {
  return `${isIncome ? '+' : '−'}${formatMoney(Math.abs(amount), currency)}`
}

export function formatNumber(value: number, fractionDigits = 0): string {
  return new Intl.NumberFormat(LOCALE, { maximumFractionDigits: fractionDigits }).format(value)
}

export function formatDate(value: string | Date | null | undefined): string {
  if (!value) return 'Nije navedeno'
  return new Intl.DateTimeFormat(LOCALE, { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value))
}

export function formatDateTime(value: string | Date | null | undefined): string {
  if (!value) return 'Nije navedeno'
  return new Intl.DateTimeFormat(LOCALE, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

export function formatRelative(value: string): string {
  const diffSeconds = Math.round((new Date(value).getTime() - Date.now()) / 1000)
  const formatter = new Intl.RelativeTimeFormat(LOCALE, { numeric: 'auto' })
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['day', 86400],
    ['hour', 3600],
    ['minute', 60],
  ]
  for (const [unit, seconds] of units) {
    if (Math.abs(diffSeconds) >= seconds) return formatter.format(Math.round(diffSeconds / seconds), unit)
  }
  return 'upravo sada'
}

export function maskAccountNumber(accountNumber: string): string {
  const digits = accountNumber.replaceAll('-', '')
  return `•••• ${digits.slice(-4)}`
}

export function formatAccountNumber(accountNumber: string): string {
  const [bank, middle, control] = accountNumber.split('-')
  if (!bank || !middle || !control) return accountNumber
  return `${bank}-${middle.replace(/^0+(?=\d)/, '')}-${control}`
}

export function pluralSr(count: number, one: string, few: string, many: string): string {
  const lastTwo = Math.abs(count) % 100
  const last = lastTwo % 10
  if (last === 1 && lastTwo !== 11) return one
  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return few
  return many
}

export function gridColumnsClass(count: number, maxColumns = 3): string {
  return `grid-${Math.max(1, Math.min(count, maxColumns))}`
}

export function toIsoDate(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 10)
}

export function initials(firstName?: string | null, lastName?: string | null): string {
  return `${firstName?.[0] ?? ''}${lastName?.[0] ?? ''}`.toUpperCase() || 'BS'
}

export function greetingForNow(): string {
  const hour = new Date().getHours()
  if (hour < 5) return 'Dobro veče'
  if (hour < 12) return 'Dobro jutro'
  if (hour < 18) return 'Dobar dan'
  return 'Dobro veče'
}
