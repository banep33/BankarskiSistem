import { describe, expect, it } from 'vitest'
import { formatAccountNumber, formatMoney, formatSignedMoney, gridColumnsClass, maskAccountNumber, pluralSr, toIsoDate } from '@/utils/format'

describe('formatMoney', () => {
  it('koristi srpski format sa dve decimale i oznakom valute', () => {
    expect(formatMoney(1234567.5, 'RSD')).toBe('1.234.567,50 RSD')
    expect(formatMoney(0, 'EUR')).toBe('0,00 EUR')
  })

  it('zaokružuje na pare', () => {
    expect(formatMoney(10.005, 'RSD')).toMatch(/^10,0[01] RSD$/)
  })

  it('označava priliv i odliv znakom', () => {
    expect(formatSignedMoney(-250, 'RSD', true)).toBe('+250,00 RSD')
    expect(formatSignedMoney(250, 'RSD', false)).toBe('−250,00 RSD')
  })
})

describe('pluralSr', () => {
  const forms = (n: number) => pluralSr(n, 'aktivan', 'aktivna', 'aktivnih')

  it('koristi jedninu za 1, 21, 101', () => {
    expect([1, 21, 101].map(forms)).toEqual(['aktivan', 'aktivan', 'aktivan'])
  })

  it('koristi paukal za 2 do 4, 22 do 24', () => {
    expect([2, 3, 4, 22, 34].map(forms)).toEqual(['aktivna', 'aktivna', 'aktivna', 'aktivna', 'aktivna'])
  })

  it('koristi množinu za 0, 5 do 20 i izuzetke 11 do 14', () => {
    expect([0, 5, 11, 12, 13, 14, 20, 111].map(forms)).toEqual(Array(8).fill('aktivnih'))
  })
})

describe('gridColumnsClass', () => {
  it('ne ostavlja prazne kolone kada stavki ima manje od maksimuma', () => {
    expect(gridColumnsClass(1)).toBe('grid-1')
    expect(gridColumnsClass(2)).toBe('grid-2')
    expect(gridColumnsClass(3)).toBe('grid-3')
  })

  it('ograničava broj kolona na zadati maksimum', () => {
    expect(gridColumnsClass(7)).toBe('grid-3')
    expect(gridColumnsClass(5, 2)).toBe('grid-2')
  })

  it('za praznu listu vraća jednu kolonu', () => {
    expect(gridColumnsClass(0)).toBe('grid-1')
  })
})

describe('brojevi računa i datumi', () => {
  it('prikazuje broj računa bez vodećih nula u srednjem delu', () => {
    expect(formatAccountNumber('205-0000001234567-89')).toBe('205-1234567-89')
    expect(formatAccountNumber('nije-broj')).toBe('nije-broj')
  })

  it('maskira broj računa tako da ostanu vidljive samo poslednje četiri cifre', () => {
    expect(maskAccountNumber('205-0000001234567-89')).toBe('•••• 6789')
  })

  it('pravi ISO datum po lokalnom kalendaru', () => {
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05')
  })
})
