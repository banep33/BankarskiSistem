function parseAccountNumber(input: string): { bank: string; account: string; control: string } | null {
  const compact = input.trim().replaceAll(' ', '')
  const dashed = /^(\d{3})-(\d{1,13})-(\d{2})$/.exec(compact)
  if (dashed) return { bank: dashed[1]!, account: dashed[2]!.padStart(13, '0'), control: dashed[3]! }
  if (/^\d{18}$/.test(compact)) return { bank: compact.slice(0, 3), account: compact.slice(3, 16), control: compact.slice(16) }
  return null
}

function mod97(digits: string): number {
  let remainder = 0
  for (const digit of digits) remainder = (remainder * 10 + Number(digit)) % 97
  return remainder
}

export function accountControlDigits(bank: string, account: string): string {
  const remainder = (mod97(bank + account) * 100) % 97
  return String(98 - remainder).padStart(2, '0')
}

export function isValidAccountNumber(input: string): boolean {
  const parsed = parseAccountNumber(input)
  return parsed !== null && accountControlDigits(parsed.bank, parsed.account) === parsed.control
}

export function normalizeAccountNumber(input: string): string {
  const parsed = parseAccountNumber(input)
  return parsed ? `${parsed.bank}-${parsed.account}-${parsed.control}` : input.trim()
}

export function passwordIssues(password: string): string[] {
  const issues: string[] = []
  if (password.length < 8) issues.push('najmanje 8 karaktera')
  if (!/[A-Z]/.test(password)) issues.push('veliko slovo')
  if (!/[a-z]/.test(password)) issues.push('malo slovo')
  if (!/\d/.test(password)) issues.push('cifru')
  return issues
}
