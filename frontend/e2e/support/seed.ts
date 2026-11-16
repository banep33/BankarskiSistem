import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

export interface SeedAccount {
  email: string
  password: string
}

export interface SeedData {
  admin: SeedAccount
  banker: SeedAccount
  customers: { one: SeedAccount; two: SeedAccount; three: SeedAccount }
}

const seedFile = join(dirname(fileURLToPath(import.meta.url)), '..', '.data', 'seed.json')

export function saveSeed(data: SeedData): void {
  mkdirSync(dirname(seedFile), { recursive: true })
  writeFileSync(seedFile, JSON.stringify(data, null, 2))
}

export function loadSeed(): SeedData {
  return JSON.parse(readFileSync(seedFile, 'utf-8')) as SeedData
}
