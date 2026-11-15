ALTER TABLE accounts DROP CHECK chk_accounts_number_format;

UPDATE accounts
SET AccountNumber = CONCAT(
  SUBSTRING_INDEX(AccountNumber, '-', 1), '-',
  LPAD(SUBSTRING_INDEX(SUBSTRING_INDEX(AccountNumber, '-', 2), '-', -1), 13, '0'), '-',
  SUBSTRING_INDEX(AccountNumber, '-', -1))
WHERE AccountNumber REGEXP '^[0-9]{3}-[0-9]{1,12}-[0-9]{2}$';

ALTER TABLE accounts
  ADD CONSTRAINT chk_accounts_number_format CHECK (AccountNumber REGEXP '^[0-9]{3}-[0-9]{13}-[0-9]{2}$');

UPDATE ledger_accounts la
JOIN accounts a ON a.Id = la.AccountId
SET la.Code = CONCAT('KLI-', a.AccountNumber), la.Name = CONCAT('Racun klijenta ', a.AccountNumber);

ALTER TABLE transactions DROP CHECK chk_transactions_type_accounts;

ALTER TABLE transactions
  MODIFY Type enum('Uplata','Isplata','Transfer','EksterniTransfer') NOT NULL,
  ADD CONSTRAINT chk_transactions_type_accounts CHECK (
    (Type = 'Uplata' AND FromAccountId IS NULL AND ToAccountId IS NOT NULL)
    OR (Type = 'Isplata' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Transfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NOT NULL AND FromAccountId <> ToAccountId)
    OR (Type = 'EksterniTransfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
  );

CREATE TABLE payment_orders (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  FromAccountId int NOT NULL,
  ToAccountId int NULL,
  RecipientName varchar(100) NOT NULL,
  RecipientAccountNumber varchar(20) NOT NULL,
  RecipientAddress varchar(150) NULL,
  Amount decimal(18,2) NOT NULL,
  Currency char(3) NOT NULL,
  PaymentCode char(3) NOT NULL,
  Purpose varchar(105) NOT NULL,
  ReferenceModel char(2) NULL,
  ReferenceNumber varchar(25) NULL,
  Status enum('Zakazan','UObradi','Izvrsen','Odbijen','Otkazan') NOT NULL,
  ScheduledFor date NULL,
  ExecutedAt datetime NULL,
  RejectionReason varchar(255) NULL,
  TransactionId int NULL,
  IdempotencyKey varchar(64) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_payment_orders_transaction (TransactionId),
  UNIQUE KEY ux_payment_orders_idempotency (UserId, IdempotencyKey),
  KEY idx_payment_orders_user_created (UserId, CreatedAt),
  KEY idx_payment_orders_status_scheduled (Status, ScheduledFor),
  KEY idx_payment_orders_from_account (FromAccountId),
  KEY idx_payment_orders_to_account (ToAccountId),
  CONSTRAINT fk_payment_orders_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_payment_orders_from_account FOREIGN KEY (FromAccountId) REFERENCES accounts (Id),
  CONSTRAINT fk_payment_orders_to_account FOREIGN KEY (ToAccountId) REFERENCES accounts (Id),
  CONSTRAINT fk_payment_orders_transaction FOREIGN KEY (TransactionId) REFERENCES transactions (Id),
  CONSTRAINT chk_payment_orders_amount_positive CHECK (Amount > 0),
  CONSTRAINT chk_payment_orders_currency CHECK (Currency REGEXP '^[A-Z]{3}$'),
  CONSTRAINT chk_payment_orders_payment_code CHECK (PaymentCode REGEXP '^[12][0-9]{2}$'),
  CONSTRAINT chk_payment_orders_recipient_account CHECK (RecipientAccountNumber REGEXP '^[0-9]{3}-[0-9]{13}-[0-9]{2}$'),
  CONSTRAINT chk_payment_orders_model CHECK (ReferenceModel IS NULL OR ReferenceModel IN ('00', '11', '97')),
  CONSTRAINT chk_payment_orders_reference CHECK (ReferenceNumber IS NULL OR ReferenceNumber REGEXP '^[0-9A-Za-z-]{1,25}$'),
  CONSTRAINT chk_payment_orders_executed CHECK ((Status IN ('Izvrsen', 'UObradi')) = (TransactionId IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE beneficiaries (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  Nickname varchar(60) NOT NULL,
  RecipientName varchar(100) NOT NULL,
  AccountNumber varchar(20) NOT NULL,
  RecipientAddress varchar(150) NULL,
  PaymentCode char(3) NULL,
  Purpose varchar(105) NULL,
  ReferenceModel char(2) NULL,
  ReferenceNumber varchar(25) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  LastUsedAt datetime NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_beneficiaries_user_nickname (UserId, Nickname),
  KEY idx_beneficiaries_user_last_used (UserId, LastUsedAt),
  CONSTRAINT fk_beneficiaries_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT chk_beneficiaries_account CHECK (AccountNumber REGEXP '^[0-9]{3}-[0-9]{13}-[0-9]{2}$'),
  CONSTRAINT chk_beneficiaries_payment_code CHECK (PaymentCode IS NULL OR PaymentCode REGEXP '^[12][0-9]{2}$'),
  CONSTRAINT chk_beneficiaries_model CHECK (ReferenceModel IS NULL OR ReferenceModel IN ('00', '11', '97'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE legal_documents (
  Id int NOT NULL AUTO_INCREMENT,
  DocumentType enum('PolitikaPrivatnosti','UsloviKoriscenja') NOT NULL,
  Version varchar(20) NOT NULL,
  Title varchar(150) NOT NULL,
  Content mediumtext NOT NULL,
  PublishedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PublishedByUserId int NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_legal_documents_type_version (DocumentType, Version),
  KEY idx_legal_documents_type_published (DocumentType, PublishedAt),
  CONSTRAINT fk_legal_documents_user FOREIGN KEY (PublishedByUserId) REFERENCES users (Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE user_consents (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  LegalDocumentId int NOT NULL,
  AcceptedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  IpAddress varchar(45) NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_user_consents_user_document (UserId, LegalDocumentId),
  KEY idx_user_consents_document (LegalDocumentId),
  CONSTRAINT fk_user_consents_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT fk_user_consents_document FOREIGN KEY (LegalDocumentId) REFERENCES legal_documents (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO legal_documents (DocumentType, Version, Title, Content) VALUES
('PolitikaPrivatnosti', '1.0', 'Politika privatnosti',
'Ova politika objašnjava koje podatke o ličnosti banka obrađuje kada koristite aplikaciju Bankarski Sistem, u koje svrhe, na osnovu kog pravnog osnova i koja prava imate. Obrada se vrši u skladu sa Zakonom o zaštiti podataka o ličnosti Republike Srbije.

1. Rukovalac podacima
Rukovalac je banka koja pruža uslugu. Kontakt lica za zaštitu podataka o ličnosti objavljen je na sajtu banke.

2. Koje podatke obrađujemo
Identifikacioni i kontakt podaci: ime, prezime, email adresa.
Podaci o računima i platnim transakcijama: brojevi računa, stanja, nalozi, primaoci, svrha plaćanja.
Bezbednosni podaci: istorija prijava, IP adresa, neuspešni pokušaji prijave, evidencija izmena (audit log).
Podešavanja i obaveštenja u aplikaciji.

3. Svrhe i pravni osnov
Izvršenje ugovora o otvaranju i vođenju računa i pružanju platnih usluga.
Poštovanje zakonskih obaveza banke, uključujući propise o platnim uslugama i o sprečavanju pranja novca i finansiranja terorizma.
Legitimni interes banke za zaštitu bezbednosti sistema i sprečavanje prevara.

4. Rok čuvanja
Podaci o transakcijama i identifikaciji čuvaju se onoliko koliko propisi nalažu (po pravilu najmanje deset godina od okončanja poslovnog odnosa). Bezbednosni zapisi se čuvaju u roku potrebnom za zaštitu sistema i dokazivanje.

5. Primaoci podataka
Podaci se ne prodaju niti ustupaju u marketinske svrhe. Mogu biti dostavljeni nadležnim organima kada to zakon nalaže, kao i platnim sistemima neophodnim za izvršenje naloga.

6. Vaša prava
Pravo na pristup i kopiju podataka (izvoz podataka dostupan je u aplikaciji, u odeljku Privatnost).
Pravo na ispravku netačnih podataka.
Pravo na brisanje i ograničenje obrade, u meri u kojoj to zakonske obaveze čuvanja dozvoljavaju.
Pravo na prigovor i pravo na prenosivost podataka.
Pravo na pritužbu Povereniku za informacije od javnog značaja i zaštitu podataka o ličnosti.

7. Bezbednost
Lozinke se čuvaju isključivo u obliku jednosmernog kriptografskog otiska. Pristup podacima je ograničen po ulogama, sve važne radnje se evidentiraju, a sesija se automatski prekida posle perioda neaktivnosti.

8. Izmene politike
O izmenama ćete biti obavešteni u aplikaciji. Nova verzija zahteva vaše ponovno potvrđivanje pre daljeg korišćenja.'),
('UsloviKoriscenja', '1.0', 'Uslovi korišćenja mobilnog i elektronskog bankarstva',
'1. Predmet
Ovi uslovi uređuju korišćenje aplikacije Bankarski Sistem za pregled računa i izvršenje platnih naloga.

2. Pristup i bezbednost
Korisnik je dužan da čuva tajnost lozinke i da je ne odaje nikome, uključujući i zaposlene u banci. Banka nikada neće tražiti lozinku putem emaila, poruke ili telefona.
Posle pet uzastopnih neuspešnih pokušaja prijave nalog se privremeno zaključava radi zaštite.
Sesija se automatski završava posle perioda neaktivnosti.

3. Platni nalozi
Korisnik je odgovoran za tačnost podataka u nalogu: broj računa primaoca, iznos, šifru plaćanja, model i poziv na broj.
Interni transferi izvršavaju se odmah. Nalozi ka drugim bankama šalju se u kliring i mogu biti proknjiženi kod primaoca naknadno.
Zakazani nalog može se otkazati do trenutka izvršenja. Nalog neće biti izvršen ako na računu nema dovoljno sredstava.
Banka primenjuje dnevne limite i ograničenje broja transakcija radi zaštite korisnika.

4. Obaveštenja
Banka obaveštava korisnika o svakoj promeni na računu kroz obaveštenja u aplikaciji.

5. Prijava zloupotrebe
U slučaju sumnje na neovlašćeno korišćenje korisnik je dužan da odmah promeni lozinku i obavesti banku.

6. Izmene uslova
O izmenama uslova korisnik se obaveštava u aplikaciji, a nastavak korišćenja zahteva prihvatanje nove verzije.');

INSERT INTO schema_migrations (Version, Description) VALUES ('V005', 'payment orders, beneficiaries, legal documents and consents, standard account numbers');
