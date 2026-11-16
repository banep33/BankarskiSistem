# Bankarski Sistem

Edukativni sistem elektronskog bankarstva prilagođen platnom prometu u Republici Srbiji: računi u RSD, EUR i USD, platni nalozi sa šifrom plaćanja i pozivom na broj (model 97), dvojno knjigovodstvo, zaštita korisnika finansijskih usluga, privatnost po ZZPL i simulacija KYC/AML provera.

> Projekat služi za učenje. Kliring sa drugim bankama, AML izveštavanje i KYC su simulirani i nisu povezani sa stvarnim institucijama.

## Mogućnosti

**Klijent**
- pregled računa, stanja po valutama, prometa i grafikona prihoda i rashoda;
- platni nalog u tri koraka: unos, provera sa naknadom i dnevnim limitom, potvrda sa priznanicom;
- interna plaćanja (odmah, besplatno), plaćanja drugoj banci (kliring, naknada iz cenovnika), zakazana plaćanja i otkazivanje;
- trajni nalozi (nedeljno, mesečno, kvartalno, godišnje) sa automatskim izvršenjem, istorijom izvršenja, pauzom posle uzastopnih neuspeha i izmenom uz potvrdu lozinkom;
- menjačnica između sopstvenih dinarskih i deviznih računa po kursnoj listi banke (kupovni, srednji, prodajni kurs), sa zaštitom od promene kursa između obračuna i potvrde;
- sačuvani primaoci, istorija transakcija sa filterima i kursor paginacijom;
- debitne kartice (simulacija): izdavanje, dozvole za internet, beskontaktno i inostranstvo, dnevni limiti, privremena i bankarska blokada, prijava gubitka sa zamenskom karticom, simulator plaćanja sa stvarnim proverama i knjiženjem;
- ciljevi štednje sa posebnim štednim računom, napretkom, potrebnim mesečnim iznosom i automatskom štednjom kroz trajni nalog;
- potrošnja po kategorijama sa automatskim predlogom i pravilima po primaocu, mesečni budžeti sa upozorenjima na 80% i 100%;
- izvod po računu za izabrani period iz glavne knjige, preuzimanje u CSV (srpski Excel format) i štampa ili PDF iz pregledača;
- prigovori sa zakonskim rokom od 15 dana i prepiskom sa bankom;
- centar za privatnost: saglasnosti, uvid u to ko je pristupao podacima, zahtevi po ZZPL, izvoz podataka u JSON;
- bezbednost: dvostepena prijava (TOTP) sa rezervnim kodovima, aktivne sesije i odjava uređaja, istorija prijava, automatska odjava zbog neaktivnosti, sakrivanje stanja.

**Bankar**
- klijenti, provera identiteta (KYC) i procena rizika, otvaranje računa, uplata na šalteru, blokada računa;
- red prigovora sa dodelom, internim beleškama, produženjem roka i odgovorom;
- storniranje pogrešnih transakcija po principu četiri oka (bankar predlaže, administrator odobrava), sa protivstavkom u glavnoj knjizi;
- pregled svih transakcija i ocene zadovoljstva korisnika (CSAT, NPS, CES).

**Administrator**
- korisnici svih uloga (kreiranje, izmena, aktivacija i deaktivacija, otključavanje, brisanje naloga bez istorije), svi računi, audit log;
- zahtevi za zaboravljenu lozinku: korisnik predlaže novu lozinku, a administrator je aktivira posle provere identiteta;
- glavna knjiga: bruto bilans, usklađenost stanja računa sa knjiženjima, konta;
- objava kursne liste sa istorijom i proverama ispravnosti;
- AML upozorenja, zahtevi po ZZPL, centar za pomoć (FAQ), brisanje po rokovima čuvanja.

Detaljno uputstvo za svaki ekran: [docs/uputstvo-za-koriscenje.md](docs/uputstvo-za-koriscenje.md).

## Tehnologije

| Sloj | Tehnologija |
|---|---|
| Backend | ASP.NET Core 8 Web API, EF Core 8 (Pomelo MySQL), FluentValidation, BCrypt, JWT |
| Baza | MySQL 8, verzionisane SQL migracije, okidači za nepromenljivost knjiženja |
| Frontend | Vue 3, TypeScript, Vue Router, Pinia, Axios, Vite |
| Testovi | xUnit (unit i integracioni nad pravim MySQL-om), konzolni load test |

## Struktura

```
backend/
  src/BankarskiSistem.Core             entiteti, interfejsi, bankarska pravila (račun, poziv na broj, novac)
  src/BankarskiSistem.Infrastructure   EF Core, repozitorijumi, servisi, poslovi u pozadini
  src/BankarskiSistem.API              kontroleri (API v1), ugovori, validatori, middleware
  tests/BankarskiSistem.Tests          unit i integracioni testovi
  tests/BankarskiSistem.LoadTests      load test
database/
  migrations/                          V001 do V015
  migrate.ps1, backup.ps1, restore.ps1, verify-backup.ps1, create-app-user.ps1
docs/                                  uputstvo, bezbednosni koncepti, rezultati load testa
frontend/                              Vue aplikacija
```

## Pokretanje lokalno

### Preduslovi

- .NET SDK 8
- Node.js 20.19+ ili 22.12+
- MySQL 8

### 1. Baza

```powershell
cd database
$env:MYSQL_PWD = "<lozinka MySQL korisnika>"
.\migrate.ps1 -User root -Database bankarskisistemdb
```

Skripta primenjuje samo migracije koje još nisu primenjene i beleži ih u tabeli `schema_migrations`.

### 2. Backend

Tajne se nikada ne upisuju u `appsettings.json`. Za lokalni rad koristi se .NET User Secrets:

```powershell
cd backend/src/BankarskiSistem.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=bankarskisistemdb;User=<korisnik>;Password=<lozinka>;"
dotnet user-secrets set "Jwt:SecretKey" "<nasumičan ključ od najmanje 64 karaktera>"
dotnet user-secrets set "Seed:Administrator:Email" "admin@primer.local"
dotnet user-secrets set "Seed:Administrator:Password" "<početna lozinka administratora>"
dotnet run --launch-profile http
```

Ključ se može generisati ovako:

```powershell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
```

Umesto User Secrets mogu se koristiti promenljive okruženja; spisak je u [.env.example](.env.example). Administrator se kreira pri prvom pokretanju ako ne postoji. API radi na `http://localhost:5039`, Swagger na `/swagger`.

### 3. Frontend

```powershell
cd frontend
npm install
npm run dev
```

Aplikacija je na `http://localhost:5173`. Vite prosleđuje `/api` zahteve backendu, pa CORS nije potreban u razvoju. Opcione promenljive su u [frontend/.env.example](frontend/.env.example).

### 4. Testovi

```powershell
cd backend
dotnet test
```

Integracioni testovi prave privremenu bazu i pokreću se kada je postavljena promenljiva `BANKARSKI_TEST_MYSQL` (na primer `Server=localhost;User=root;Password=<lozinka>;`). Bez nje se preskaču.

```powershell
cd frontend
npm run test
npm run build
npm run lint
```

E2E testovi (Playwright) proveravaju raspored svih stranica na računaru i telefonu: bez bočnog pomeranja, kartice iste širine, centrirane kolone tabela, meniji bez prelamanja, centrirani modali i bez grešaka u konzoli. Proveravaju i glavne tokove (potvrda blokiranja računa, odvojeno čuvanje podešavanja, odjava samo iz menija). Pokreću se nad aplikacijom koja radi; lokalno koriste instalirani Chrome, a podrazumevani administrator se zadaje promenljivama `E2E_ADMIN_EMAIL` i `E2E_ADMIN_PASSWORD`. Backend za ove testove treba da ima `SecuritySettings:RequireTwoFactorForStaff=false`.

```powershell
cd frontend
$env:E2E_BASE_URL = "http://localhost:5173"
npm run test:e2e
```

Provera rezervne kopije vraća poslednji backup u privremenu bazu, proverava knjigu, usaglašenost i verziju šeme, pa briše privremenu bazu:

```powershell
cd database
powershell -ExecutionPolicy Bypass -File .\verify-backup.ps1
```

Na svaki push i pull request GitHub Actions (`.github/workflows/ci.yml`) pokreće: build backenda bez upozorenja, sve backend testove na MySQL-u, proveru ranjivih NuGet paketa, frontend testove, proveru tipova, oba lintera, build, `npm audit` za produkcione zavisnosti, primenu svih migracija na praznoj bazi sa proverom kontrolnih suma i E2E testove interfejsa nad pravim API-jem i bazom.

## Arhitektura i plan razvoja

- [`docs/adr`](docs/adr/README.md): zapisi arhitektonskih odluka (slojevi, dvojno knjiženje, zaključavanje, tokeni, obavezna 2FA, migracije, UTC, frontend).
- [`docs/plan-razvoja.md`](docs/plan-razvoja.md): plan za stvarnu upotrebu (passkey, šifrovanje, skaliranje, integracija sa NBS IPS i kliringom, regulativa), sa fazama i standardima na koje se oslanja.

## Bezbednost u kratkim crtama

- lozinke su heširane BCrypt-om, nalog se zaključava posle 5 neuspešnih pokušaja, a odgovor za zaključan nalog sa pogrešnom lozinkom je isti kao za nepostojeći (bez otkrivanja postojanja naloga);
- **obavezna dvostepena prijava za zaposlene**: sesija pamti nivo provere, access token nosi `amr` (`pwd` ili `mfa`, RFC 8176), a politike za bankara i administratora traže `mfa`; zaposleni bez 2FA mogu samo da je uključe;
- zaboravljena lozinka bez otkrivanja postojanja naloga: nova lozinka se odmah hešira, aktivira je administrator po principu četiri oka, a zahtev ističe za 72 sata;
- kratkotrajni JWT (15 minuta) samo u memoriji pregledača i refresh token u HttpOnly, SameSite=Strict kolačiću koji se menja pri svakoj upotrebi; ponovna upotreba starog tokena gasi sesiju;
- dvostepena prijava po RFC 6238 (TOTP): tajna šifrovana preko ASP.NET Data Protection, zaštita od ponovne upotrebe koda, jednokratni rezervni kodovi čuvani kao heš;
- sesije se čuvaju na serveru i mogu se opozvati, uz apsolutni rok od 8 sati;
- ponovna potvrda lozinkom za rizične radnje (veliko plaćanje novom primaocu, lični prag, izvoz podataka);
- RBAC i provera vlasništva nad svakim resursom na backendu;
- pesimističko zaključavanje redova i idempotentni ključevi za sve finansijske operacije;
- rate limiting po vrsti zahteva, greške u formatu RFC 9457 bez internih detalja;
- audit log osetljivih radnji, nepromenljiv na nivou baze (okidači zabranjuju izmenu i brisanje), i evidencija svakog uvida zaposlenog u podatke klijenta;
- Content Security Policy i bezbednosna zaglavlja i za frontend (Vite dev i preview server, `<meta>` u buildu), `X-Request-Id` za praćenje zahteva kroz logove, JSON logovi van razvoja, limit veličine zahteva od 1 MB;
- migracije sa kontrolnim sumama: izmena već primenjene migracije se odbija;
- `database/create-app-user.ps1` pravi MySQL korisnika samo sa SELECT, INSERT, UPDATE i DELETE pravima za rad aplikacije (princip najmanjih privilegija), dok se migracije pokreću administratorskim korisnikom;
- obaveštenja sa maskiranim brojevima računa i brisanje podataka po rokovima čuvanja.

Ako pronađeš bezbednosni propust, prijavi ga privatno vlasniku repozitorijuma umesto javnog issue-a.

## Produkciona postavka frontenda

Statički build (`frontend/dist`) treba servirati preko HTTPS-a sa istim zaglavljima koja koristi `vite preview` (videti `vite.config.ts`): `Content-Security-Policy` sa `frame-ancestors 'none'`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` i `Permissions-Policy`. Ako API nije na istom domenu, njegov origin se automatski dodaje u `connect-src` iz `VITE_API_BASE_URL`.

## Ograničenja

Ovo nije produkcioni sistem. Nedostaju stvarni kliring (NBS RTGS/IPS), integracija sa registrima za KYC, prijava Upravi za sprečavanje pranja novca, HSM za ključeve i revizija od strane treće strane.
