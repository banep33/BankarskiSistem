# Load test — 2026-11-14 17:30

Oznaka: **POSLE optimizacija**, virtualnih korisnika: **200**, trajanje scenarija: **30 s**, adresa: `http://localhost:5199`

### Talas prijava (svi korisnici istovremeno) (200 virtuelnih korisnika, 5.5 s)

Ukupno zahteva: **200**, propusnost: **36.5 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| POST /auth/login | 200 | 0 | 0 | 4321.4 | 5217.2 | 5255.0 | 5434.5 |

### Mešano čitanje (dashboard, računi, transakcije, obaveštenja) (200 virtuelnih korisnika, 30.1 s)

Ukupno zahteva: **47322**, propusnost: **1572.7 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| GET /me/accounts | 11627 | 0 | 0 | 119.4 | 122.1 | 170.1 | 214.3 |
| GET /me/dashboard | 12034 | 0 | 0 | 140.7 | 137.3 | 213.2 | 248.6 |
| GET /me/notifications/unread-count | 11895 | 0 | 0 | 119.1 | 122.0 | 168.8 | 207.7 |
| GET /me/transactions | 11766 | 0 | 0 | 128.3 | 129.1 | 185.1 | 235.3 |

### Plaćanja između nasumičnih klijenata (200 virtuelnih korisnika, 30.5 s)

Ukupno zahteva: **8058**, propusnost: **263.8 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| POST /payments | 6412 | 0 | 0 | 861.7 | 830.7 | 1254.0 | 1484.9 |
| POST /payments/quote | 1646 | 0 | 0 | 330.5 | 304.8 | 505.1 | 1109.9 |

### Vruć račun (svi plaćaju istom primaocu) (199 virtuelnih korisnika, 18.8 s)

Ukupno zahteva: **830**, propusnost: **44.1 zahteva/s**, greške: **0**

| Endpoint | Zahteva | Greške | Poslovno odbijeno (422) | Prosek ms | p50 ms | p95 ms | p99 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| POST /payments (isti primalac) | 830 | 0 | 0 | 4082.0 | 4488.5 | 4858.1 | 5144.5 |

## Ispravnost posle opterećenja

- Probni bilans jednak nuli: **DA**
- Računi bez razlike između stanja i glavne knjige: **DA**

