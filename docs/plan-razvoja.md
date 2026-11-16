# Plan razvoja za stvarnu upotrebu

Sistem je danas potpun za lokalni rad i demonstraciju: dvojno knjiženje, plaćanja, trajni nalozi, menjačnica, kartice, štednja, budžeti, prigovori, privatnost, obavezna dvostepena prijava za zaposlene, automatski testovi i CI. Ovaj dokument opisuje šta bi se uradilo pre i posle puštanja u stvarnu upotrebu u banci, kojim redom i na osnovu kojih standarda. Arhitektonske odluke na koje se plan oslanja su u [`docs/adr`](adr/README.md).

Prioriteti:

- **Faza 1:** obavezno pre produkcije;
- **Faza 2:** u prvih šest meseci rada;
- **Faza 3:** kada obim ili poslovni zahtevi to traže.

## 1. Bezbednost i zaštita podataka

| Faza | Stavka | Zašto i po kom standardu |
|---|---|---|
| 1 | **Passkey (WebAuthn/FIDO2) za zaposlene, zatim i za klijente.** Detalji su u odeljku 1.1. | Otporno na phishing; NIST SP 800-63B AAL2/AAL3, PSD2 SCA |
| 1 | Šifrovanje baze u mirovanju (InnoDB tablespace encryption sa keyring komponentom) i šifrovane rezervne kopije | PCI DSS 3.5, ZZPL čl. 50, ISO 27001 A.8.24 |
| 1 | Ključevi (JWT, Data Protection, ključ baze) u HSM-u ili KMS-u, sa planiranom rotacijom | NIST SP 800-57, PCI DSS 3.6 |
| 1 | Aplikacija radi sa MySQL korisnikom bez DDL prava (`database/create-app-user.ps1` već postoji) | princip najmanjih privilegija, OWASP ASVS V1.4 |
| 1 | Model pretnji (STRIDE) za prijavu, plaćanje, kartice i administraciju | OWASP Threat Modeling, ISO 27001 A.5.7 |
| 1 | SAST (CodeQL) i provera tajni sa zaštitom pri push-u u CI-ju | OWASP SAMM, GitHub Advanced Security |
| 1 | DAST (OWASP ZAP) nad testnim okruženjem i penetracioni test treće strane | OWASP WSTG, propisi NBS o upravljanju rizicima informacionog sistema |
| 1 | WAF i zaštita od DDoS-a ispred API-ja | OWASP API Security Top 10 (API4) |
| 2 | Otkrivanje prevara: velocity pravila (broj i iznos po primaocu i vremenu), novi primalac uz veliki iznos, otisak uređaja, procena rizika pri prijavi | PSD2 RTS čl. 18 (izuzeća na osnovu analize rizika), EBA smernice |
| 2 | CAPTCHA posle više neuspešnih prijava, umesto samo zaključavanja | OWASP Authentication Cheat Sheet |
| 2 | SIEM: centralno prikupljanje bezbednosnih događaja sa alarmima | ISO 27001 A.8.16, NIST SP 800-92 |
| 3 | Povezivanje sesije sa uređajem (DPoP ili mTLS za mobilnu aplikaciju) | RFC 9449, FAPI 2.0 |

### 1.1 Passkey: plan implementacije

- Registracija u odeljku Bezbednost, sa više uređaja po nalogu, nazivom uređaja i poslednjom upotrebom.
- Prijava je email i lozinka, pa dodir prstom, prepoznavanje lica ili PIN uređaja. Kasnije je moguća i prijava bez lozinke.
- Brisanje izgubljenog uređaja i reset uz princip četiri oka (kao za TOTP). TOTP i rezervni kodovi ostaju kao rezerva.
- Claim `amr` dobija vrednost `hwk`, a politika za zaposlene je prihvata kao jaču proveru (ADR 0005).
- Frontend koristi ugrađeni `navigator.credentials`, a backend proverenu biblioteku Fido2NetLib. Migracija dodaje tabelu `user_passkeys` (credential ID, javni ključ, brojač potpisa, AAGUID).
- Testiranje: Windows Hello, telefon preko QR koda i virtuelni autentifikator u Chrome DevTools (radi i u Playwright testovima).
- Posle passkey-a, opciono za klijente: „zapamti ovaj uređaj“ na 30 dana, uz obaveštenje o svakoj prijavi sa novog uređaja. Za zaposlene se to ne uvodi.

## 2. Skalabilnost i pouzdanost

Aplikacija je već bez stanja na nivou API-ja (sesije i ključevi idempotentnosti su u bazi), a pozadinski poslovi koriste `FOR UPDATE SKIP LOCKED`. Zato može da radi u više instanci uz sledeće dopune.

| Faza | Stavka | Zašto |
|---|---|---|
| 1 | Data Protection ključevi u zajedničkom skladištu | da sve instance čitaju iste šifrovane podatke |
| 1 | Plan oporavka: RPO 5 minuta (binarni log, point-in-time recovery), RTO 1 sat; redovno vežbanje vraćanja (`verify-backup.ps1` je osnova) | ISO 22301, DORA čl. 12 |
| 2 | Deljeni rate limiter (Redis) umesto memorije jedne instance | ista ograničenja na svim instancama |
| 2 | Outbox obrazac za obaveštenja i događaje (upis u istoj transakciji, slanje iz pozadinskog posla) | garantovana isporuka bez gubitka posle pada |
| 2 | Replika baze samo za čitanje, za izveštaje i izvode | rasterećenje glavne baze |
| 3 | Particionisanje tabela `transactions` i `postings` po mesecu i arhiviranje starijih godina | zadržava brzinu upita posle više godina rada |
| 3 | Red poruka za kliring i spoljne integracije (ponovni pokušaji, dead-letter red) | pouzdana razmena sa spoljnim sistemima |
| 3 | Blue-green ili rolling puštanje novih verzija uz migracije kompatibilne unazad | rad bez prekida |

## 3. Nadzor i rad u produkciji

| Faza | Stavka |
|---|---|
| 1 | Centralni logovi (JSON logovi i `X-Request-Id` već postoje) u Seq ili ELK, sa čuvanjem po propisima |
| 1 | Alarmi: greške 5xx, neuravnotežen bruto bilans, razlika u usaglašenosti računa, zastoj pozadinskih poslova, nagli rast neuspešnih prijava |
| 2 | OpenTelemetry metrike i tragovi (trajanje plaćanja, broj zaključavanja, red čekanja trajnih naloga) |
| 2 | Ciljevi nivoa usluge (SLO): na primer 99,9% dostupnosti i 95% plaćanja ispod 500 ms |
| 2 | Javna stranica statusa sistema i procedura za incidente |

## 4. Integracije sa platnim sistemom i spoljnim servisima

| Faza | Stavka | Napomena |
|---|---|---|
| 1 | Zamena simuliranog kliringa stvarnim: NBS RTGS i kliring, NBS IPS za instant plaćanja | poruke po ISO 20022, idempotentno slanje i usaglašavanje dnevnih izveštaja |
| 1 | Kursna lista sa NBS servisa umesto ručnog unosa | srednji kurs NBS kao osnova, kupovni i prodajni prema politici banke |
| 1 | Slanje emaila i SMS-a preko provajdera (obaveštenja, nikada kodovi za prijavu putem SMS-a) | |
| 2 | Plaćanje skeniranjem NBS IPS QR koda (računi za struju, telefon…) | standard NBS za IPS QR |
| 2 | Kartice preko stvarnog procesora, uz tokenizaciju | PCI DSS: banka ni dalje ne čuva pun broj kartice |
| 2 | Elektronska identifikacija klijenta (video identifikacija, provera lične karte) | Zakon o sprečavanju pranja novca, odluke NBS o video identifikaciji |
| 3 | Otvoreni API za treće strane (pristup računima i iniciranje plaćanja) | PSD2 AISP/PISP, OAuth 2.0 sa FAPI 2.0 profilom |

## 5. Regulativa i usklađenost

Sistem već ima: KYC status i procenu rizika, AML upozorenja sa pragom za gotovinu, prava po ZZPL (pristup, izvoz, zahtevi sa rokovima), evidenciju svakog uvida zaposlenog u podatke klijenta, nepromenljiv audit log i nepromenljivu knjigu. Pre produkcije je potrebno sledeće.

- Pravna revizija usklađenosti sa Zakonom o platnim uslugama, Zakonom o sprečavanju pranja novca i finansiranja terorizma, ZZPL-om i odlukama NBS (upravljanje rizicima informacionog sistema, zaštita korisnika finansijskih usluga).
- Procena uticaja na zaštitu podataka (DPIA) i imenovano lice za zaštitu podataka.
- Prijave Upravi za sprečavanje pranja novca kroz propisani kanal umesto internih upozorenja.
- Rokovi čuvanja: transakcije i dokumentacija o identifikaciji najmanje 10 godina; bezbednosni logovi prema politici banke.
- Za rad u EU: DORA (digitalna operativna otpornost) i PSD2 RTS o pouzdanoj autentifikaciji.

## 6. Kvalitet koda i proizvoda

| Faza | Stavka |
|---|---|
| 1 | Podela najvećih komponenti na manje: `NewPaymentView` (koraci plaćanja), `PrivacyView` (po tabovima), `CardsView` (podešavanja, simulator, istorija) |
| 1 | Podela `PaymentOrderService` po slučajevima upotrebe (priprema, izvršenje, zakazivanje, kliring) |
| 2 | TypeScript tipovi generisani iz OpenAPI specifikacije i ugovorni test koji pada ako se frontend i backend razilaze |
| 2 | Automatska provera pristupačnosti (axe) u E2E testovima i povremena provera čitačem ekrana (NVDA) |
| 2 | Lighthouse CI sa budžetom performansi (LCP < 2,5 s, CLS < 0,1) |
| 2 | Vizuelni regresioni testovi sa referentnim snimcima u stalnom okruženju |
| 3 | Mobilna aplikacija sa push potvrdom plaćanja (uparivanje broja protiv „MFA fatigue“ napada) |
| 3 | Krediti, dozvoljeno prekoračenje, e-računi, PDF izvodi generisani na serveru, više jezika |

## 7. Šta je već urađeno u pravcu ovog plana

- Bezbednost: obavezna 2FA za zaposlene, rotirajući refresh tokeni, ponovna potvrda lozinkom, princip četiri oka, CSP i bezbednosna zaglavlja, nepromenljiv audit log, bez otkrivanja postojanja naloga.
- Pouzdanost: pesimističko zaključavanje, idempotentni ključevi, `SKIP LOCKED` za pozadinske poslove, backup i proveren restore, migracije sa kontrolnim sumama.
- Kvalitet: 166 backend testova na pravom MySQL-u, frontend testovi (Vitest), E2E testovi rasporeda i tokova (Playwright) na računaru i telefonu, CI na svaki push, WCAG 2.1 AA kontrast, ADR zapisi.
