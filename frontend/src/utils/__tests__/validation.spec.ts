import { describe, expect, it } from 'vitest'
import { accountControlDigits, isValidAccountNumber, normalizeAccountNumber, passwordIssues } from '@/utils/validation'

describe('broj računa (kontrolni broj po modulu 97)', () => {
  const bank = '205'
  const account = '0000001234567'
  const valid = `${bank}-${account}-${accountControlDigits(bank, account)}`

  it('prihvata ispravan broj u obliku sa crticama i bez njih', () => {
    expect(isValidAccountNumber(valid)).toBe(true)
    expect(isValidAccountNumber(valid.replaceAll('-', ''))).toBe(true)
  })

  it('odbija broj sa pogrešnim kontrolnim brojem', () => {
    const wrong = valid.slice(0, -2) + (valid.endsWith('00') ? '01' : '00')
    expect(isValidAccountNumber(wrong)).toBe(false)
  })

  it('odbija pogrešan format', () => {
    expect(isValidAccountNumber('20-123-45')).toBe(false)
    expect(isValidAccountNumber('')).toBe(false)
  })

  it('dopunjuje srednji deo nulama do 13 cifara', () => {
    const short = `${bank}-1234567-${accountControlDigits(bank, account)}`
    expect(normalizeAccountNumber(short)).toBe(valid)
  })
})

describe('pravila lozinke', () => {
  it('jaka lozinka nema nedostataka', () => {
    expect(passwordIssues('Ispravna123')).toEqual([])
  })

  it('navodi svaki nedostatak posebno', () => {
    expect(passwordIssues('abc')).toEqual(['najmanje 8 karaktera', 'veliko slovo', 'cifru'])
    expect(passwordIssues('SVEVELIKA1')).toEqual(['malo slovo'])
  })
})
