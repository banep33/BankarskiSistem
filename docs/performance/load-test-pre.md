# Load test — 2026-11-14 17:24

Oznaka: **PRE optimizacija**, virtualnih korisnika: **200**, trajanje scenarija: **30 s**, adresa: `http://localhost:5199`

### Talas prijava (svi korisnici istovremeno) (200 virtuelnih korisnika, 5.8 s)

Ukupno zahteva: **200**, propusnost: **34.5 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| POST /auth/login | 200 | 0 | 0 | 4672.9 | 5345.9 | 5735.2 | 5767.3 |

### Mešano čitanje (dashboard, računi, transakcije, obaveštenja) (200 virtuelnih korisnika, 30.1 s)

Ukupno zahteva: **40702**, propusnost: **1353.3 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| GET /me/accounts | 10030 | 0 | 0 | 142.3 | 143.4 | 183.2 | 228.3 |
| GET /me/dashboard | 10335 | 0 | 0 | 154.7 | 150.0 | 213.8 | 277.3 |
| GET /me/notifications/unread-count | 10194 | 0 | 0 | 142.3 | 143.4 | 183.0 | 238.3 |
| GET /me/transactions | 10143 | 0 | 0 | 148.3 | 146.4 | 194.4 | 256.7 |

### Plaćanja između nasumičnih klijenata (200 virtuelnih korisnika, 30.5 s)

Ukupno zahteva: **7908**, propusnost: **259.4 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| POST /payments | 6287 | 0 | 0 | 868.8 | 834.2 | 1234.5 | 1456.2 |
| POST /payments/quote | 1621 | 0 | 0 | 367.0 | 358.6 | 511.1 | 647.9 |

### Vruć račun (svi plaćaju istom primaocu) (199 virtuelnih korisnika, 18.9 s)

Ukupno zahteva: **741**, propusnost: **39.2 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| POST /payments (isti primalac) | 741 | 0 | 0 | 4457.0 | 5026.9 | 5201.0 | 5252.5 |

## Ispravnost posle opterećenja

- Probni bilans jednak nuli: **DA**
- Računi bez razlike između stanja i glavne knjige: **DA**

