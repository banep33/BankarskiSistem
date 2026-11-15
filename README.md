# Bankarski Sistem

Edukativni sistem elektronskog bankarstva prilagođen platnom prometu u Republici Srbiji: računi u RSD, EUR i USD, platni nalozi sa šifrom plaćanja i pozivom na broj (model 97), dvojno knjigovodstvo, zaštita korisnika finansijskih usluga, privatnost po ZZPL i simulacija KYC/AML provera.

> Projekat služi za učenje. Kliring sa drugim bankama, AML izveštavanje i KYC su simulirani i nisu povezani sa stvarnim institucijama.

## Mogućnosti

**Klijent**
- pregled računa, stanja po valutama, prometa i grafikona prihoda i rashoda;
- platni nalog u tri koraka: unos, provera sa naknadom i dnevnim limitom, potvrda sa priznanicom;
- interna plaćanja (odmah, besplatno), plaćanja drugoj banci (kliring, naknada iz cenovnika), zakazana plaćanja i otkazivanje;
- trajni nalozi (nedeljno, mesečno, kvartalno, godišnje) sa automatskim izvršenjem, istorijom izvršenja, pauzom posle uzastopnih neuspeha i izmenom uz potvrdu lozinkom;
- sačuvani primaoci, istorija transakcija sa filterima i kursor paginacijom;
- prigovori sa zakonskim rokom od 15 dana i prepiskom sa bankom;
- centar za privatnost: saglasnosti, uvid u to ko je pristupao podacima, zahtevi po ZZPL, izvoz podataka u JSON;
- bezbednost: dvostepena prijava (TOTP) sa rezervnim kodovima, aktivne sesije i odjava uređaja, istorija prijava, automatska odjava zbog neaktivnosti, sakrivanje stanja.

**Bankar**
- klijenti, provera identiteta (KYC) i procena rizika, otvaranje računa, uplata na šalteru, blokada računa;
- red prigovora sa dodelom, internim beleškama, produženjem roka i odgovorom;
- pregled svih transakcija i ocene zadovoljstva korisnika (CSAT, NPS, CES).

**Administrator**
- korisnici svih uloga (kreiranje, izmena, aktivacija i deaktivacija, otključavanje, brisanje naloga bez istorije), svi računi, audit log;
- zahtevi za zaboravljenu lozinku: korisnik predlaže novu lozinku, a administrator je aktivira posle provere identiteta;
- glavna knjiga: bruto bilans, usklađenost stanja računa sa knjiženjima, konta;
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
  migrations/                          V001 do V009
  migrate.ps1, backup.ps1, restore.ps1
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
npm run build
npm run lint
```

## Bezbednost u kratkim crtama

- lozinke su heširane BCrypt-om, nalog se zaključava posle 5 neuspešnih pokušaja;
- zaboravljena lozinka bez otkrivanja postojanja naloga: nova lozinka se odmah hešira, aktivira je administrator po principu četiri oka, a zahtev ističe za 72 sata;
- kratkotrajni JWT (15 minuta) samo u memoriji pregledača i refresh token u HttpOnly, SameSite=Strict kolačiću koji se menja pri svakoj upotrebi; ponovna upotreba starog tokena gasi sesiju;
- dvostepena prijava po RFC 6238 (TOTP): tajna šifrovana preko ASP.NET Data Protection, zaštita od ponovne upotrebe koda, jednokratni rezervni kodovi čuvani kao heš;
- sesije se čuvaju na serveru i mogu se opozvati, uz apsolutni rok od 8 sati;
- ponovna potvrda lozinkom za rizične radnje (veliko plaćanje novom primaocu, lični prag, izvoz podataka);
- RBAC i provera vlasništva nad svakim resursom na backendu;
- pesimističko zaključavanje redova i idempotentni ključevi za sve finansijske operacije;
- rate limiting po vrsti zahteva, greške u formatu RFC 9457 bez internih detalja;
- audit log osetljivih radnji i evidencija svakog uvida zaposlenog u podatke klijenta;
- obaveštenja sa maskiranim brojevima računa i brisanje podataka po rokovima čuvanja.

Ako pronađeš bezbednosni propust, prijavi ga privatno vlasniku repozitorijuma umesto javnog issue-a.

## Ograničenja

Ovo nije produkcioni sistem. Nedostaju stvarni kliring (NBS RTGS/IPS), integracija sa registrima za KYC, prijava Upravi za sprečavanje pranja novca, HSM za ključeve i revizija od strane treće strane.
