import { describe, expect, it } from 'vitest'
import { counterpartyLabel, isIncoming } from '@/utils/labels'
import type { Transaction } from '@/types/api'

function transaction(overrides: Partial<Transaction>): Transaction {
  return {
    id: 1,
    amount: 100,
    currency: 'RSD',
    type: 'Transfer',
    status: 'Uspesna',
    description: null,
    referenceNumber: 'TRX-1',
    fromAccountId: null,
    toAccountId: null,
    fromAccountNumber: null,
    toAccountNumber: null,
    counterpartyName: null,
    counterpartyAccountNumber: null,
    createdAt: '2026-11-15T10:00:00Z',
    feeAmount: 0,
    ...overrides,
  } as Transaction
}

describe('smer transakcije', () => {
  const own = new Set([10, 11])

  it('uplata je uvek priliv, a isplata, plaćanje drugoj banci i kartica odliv', () => {
    expect(isIncoming(transaction({ type: 'Uplata', toAccountId: 10 }), own)).toBe(true)
    expect(isIncoming(transaction({ type: 'Isplata', fromAccountId: 10 }), own)).toBe(false)
    expect(isIncoming(transaction({ type: 'EksterniTransfer', fromAccountId: 10 }), own)).toBe(false)
    expect(isIncoming(transaction({ type: 'Kartica', fromAccountId: 10 }), own)).toBe(false)
  })

  it('prenos sa tuđeg na sopstveni račun je priliv', () => {
    expect(isIncoming(transaction({ fromAccountId: 99, toAccountId: 10 }), own)).toBe(true)
  })

  it('prenos između sopstvenih računa nije priliv', () => {
    expect(isIncoming(transaction({ fromAccountId: 10, toAccountId: 11 }), own)).toBe(false)
  })
})

describe('naziv druge strane', () => {
  it('za karticu prikazuje opis trgovca', () => {
    expect(counterpartyLabel(transaction({ type: 'Kartica', description: 'Kartica ...1234: Maxi' }), false)).toBe('Kartica ...1234: Maxi')
  })

  it('za storno razlikuje povraćaj i povlačenje', () => {
    expect(counterpartyLabel(transaction({ type: 'Storno' }), true)).toBe('Storno, povraćaj na račun')
    expect(counterpartyLabel(transaction({ type: 'Storno' }), false)).toBe('Storno, povlačenje sa računa')
  })

  it('za odlazno plaćanje prikazuje ime primaoca', () => {
    expect(counterpartyLabel(transaction({ counterpartyName: 'Elektrodistribucija' }), false)).toBe('Elektrodistribucija')
  })
})
