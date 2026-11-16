# 0007. Vreme se čuva u UTC-u, poslovni dan po beogradskom kalendaru

**Kontekst.** MySQL `datetime` ne čuva vremensku zonu. Bez dogovora, vreme bez oznake zone pregledač tumači kao lokalno, pa se svi prikazi pomeraju za razliku od UTC-a. Ta greška je postojala i ispravljena je.

**Odluka.** Sva vremena se upisuju u UTC. `UtcDateTimeConverter`, registrovan za svako `DateTime` svojstvo, vrednost pročitanu iz baze označava kao UTC, pa JSON uvek sadrži `Z`. Granice poslovnog dana (dnevni limiti, trajni nalozi, izvodi) računa `IBusinessCalendar` po zoni `Europe/Belgrade`. Datumi bez vremena su `DateOnly`.

**Posledice.** Prikaz je tačan u svakoj vremenskoj zoni korisnika, a ponoć za limite je uvek beogradska, uključujući promenu na letnje računanje vremena.

**Alternative.** Čuvanje lokalnog vremena je odbačeno: dvosmisleno je pri promeni sata.
