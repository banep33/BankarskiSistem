# 0006. Ručno pisane SQL migracije sa kontrolnim sumama

**Kontekst.** Šema sadrži `CHECK` ograničenja, okidače i poglede koji štite novac. Automatski generisane migracije ih ne pokrivaju dobro i teško se pregledaju.

**Odluka.** Migracije su numerisani SQL fajlovi `V###__opis.sql`. Svaka upisuje svoj red u `schema_migrations`. `migrate.ps1` ih primenjuje redom i čuva SHA-256 sadržaja, pa se izmenjena primenjena migracija odbija. Primenjene migracije se nikada ne menjaju i ne spajaju; ispravka je uvek nova migracija. Pre primene na stvarnu bazu pravi se backup, a `verify-backup.ps1` dokazuje da se backup može vratiti.

**Posledice.** Istorija šeme je čitljiva i u CI-ju se proverava na praznoj bazi. DDL u MySQL-u nije transakcioni, pa je backup pre migracije obavezan.

**Alternative.** EF Core migracije su odbačene zbog potrebe za okidačima, `CHECK` ograničenjima i migracijama podataka, koje su čitljivije kao SQL.
