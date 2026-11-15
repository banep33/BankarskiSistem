# Uputstvo za korišćenje

Uputstvo prati aplikaciju ekran po ekran, za svaku ulogu. Na kraju je lista za proveru kojom se sve funkcionalnosti mogu isprobati u pregledaču.

## Sadržaj

1. [Prvi koraci](#1-prvi-koraci)
2. [Klijent](#2-klijent)
3. [Bankar](#3-bankar)
4. [Administrator](#4-administrator)
5. [Bezbednost i privatnost za sve uloge](#5-bezbednost-i-privatnost-za-sve-uloge)
6. [Česta pitanja i rešavanje problema](#6-česta-pitanja-i-rešavanje-problema)
7. [Lista za testiranje u pregledaču](#7-lista-za-testiranje-u-pregledaču)

## 1. Prvi koraci

### Uloge

| Uloga | Ko je | Šta radi |
|---|---|---|
| Klijent | korisnik banke | plaća, prati račune, podnosi prigovore i upravlja privatnošću |
| Bankar | zaposleni na šalteru ili u podršci | otvara račune, proverava identitet, uplaćuje gotovinu, rešava prigovore |
| Administrator | upravlja sistemom | korisnici, glavna knjiga, usklađenost, audit log |

Klijent ne može sam da se registruje. Nalog mu pravi bankar, kao kada se u stvarnoj banci otvara račun na šalteru. Prvog administratora sistem napravi sam pri pokretanju (vidi README).

### Prijava

1. Otvori `http://localhost:5173`.
2. Unesi email i lozinku i klikni **Prijavi se**.
3. Pri prvoj prijavi, a i kada banka objavi novu verziju dokumenta, pojaviće se prozor **Pre nastavka** sa Politikom privatnosti i Uslovima korišćenja. Otvori svaki dokument, označi da ga prihvataš i klikni **Prihvatam i nastavljam**. Bez prihvatanja aplikacija ne može da se koristi; jedina druga opcija je odjava.

Posle 5 pogrešnih lozinki nalog se zaključava na 30 minuta. Otključati ga može bankar ili administrator.

### Zaboravljena lozinka

1. Na ekranu za prijavu klikni **Zaboravili ste lozinku?**.
2. Unesi email adresu naloga i novu lozinku dva puta (najmanje 8 karaktera, veliko i malo slovo i cifra) i klikni **Pošalji zahtev**.
3. Poruka je uvek ista, bez obzira na to da li nalog postoji, da niko ne bi mogao da proverava koje adrese su klijenti banke.
4. Do odobrenja važi stara lozinka. Banka proverava identitet (na primer povratnim pozivom na broj iz evidencije), a administrator zatim odobrava zahtev.
5. Posle odobrenja nova lozinka je aktivna, nalog je otključan, a sve ranije prijave na drugim uređajima su odjavljene.

Zahtev važi 72 sata. Novi zahtev poništava prethodni, a dnevno se mogu poslati najviše 3 zahteva. Vlasnik naloga u aplikaciji dobija bezbednosno obaveštenje o svakom zahtevu, pa odmah vidi ako ga je poslao neko drugi.

### Raspored ekrana

- **Bočni meni** levo sadrži sve odeljke dostupne tvojoj ulozi. Na telefonu se otvara ikonicom menija, a na dnu ekrana je traka sa 5 najvažnijih stavki.
- **Gornja traka**: pozdrav, ikonica oka za sakrivanje iznosa, promena teme (svetla ili tamna), zvonce sa brojem nepročitanih obaveštenja i meni korisnika sa odjavom.

## 2. Klijent

### Početna

- Velika tamna kartica prikazuje **ukupno raspoloživo** u dinarima i ostale valute ispod. Dugme **Sakrij** zamenjuje iznose tačkicama.
- **Brze radnje**: Plati, Primaoci, Istorija, Cenovnik.
- Kartice **Ovaj mesec** pokazuju priliv i odliv po valuti.
- **Moji računi** prikazuje do tri računa; klik otvara detalj računa.
- **Poslednje transakcije**: klik na transakciju otvara detalje (referenca, računi, šifra plaćanja, poziv na broj, naknada) i link za prigovor.
- Kartica za ocenu aplikacije pojavljuje se jednom mesečno i nije obavezna.

### Računi i detalj računa

- **Računi** prikazuje kartice svih računa, ukupno po valuti i tabelu sa statusom i datumom otvaranja.
- **Detalj računa** sadrži:
  - stanje i status računa;
  - priliv i odliv u izabranom periodu i grafikon prometa (7 dana, 30 dana, 12 meseci);
  - **Kopiraj broj** za kopiranje broja računa;
  - **Isplata gotovine** (simulacija bankomata) sa iznosom i opisom;
  - **Plati sa ovog računa** otvara nalog sa unapred izabranim računom;
  - listu transakcija sa dugmetom **Učitaj još**.

### Novo plaćanje

Plaćanje ima tri koraka.

**1. Podaci**
- izaberi račun sa kog plaćaš (ispod piše raspoloživo stanje);
- klikni na sačuvanog primaoca ili unesi naziv i račun primaoca. Račun ima 18 cifara, crtice nisu obavezne. Aplikacija odmah proverava kontrolni broj i javlja grešku ako je broj pogrešno prepisan;
- unesi iznos (decimale zarezom ili tačkom), šifru plaćanja, svrhu i po potrebi model i poziv na broj;
- **Zakaži plaćanje**: izaberi datum u budućnosti; do tog dana nalog možeš da otkažeš;
- **Sačuvaj primaoca**: daj mu naziv (npr. Struja) da bi ga sledeći put izabrao jednim klikom.

**2. Provera**
- prikazuje sve podatke naloga, vrstu plaćanja (u okviru banke ili drugoj banci) i vreme izvršenja;
- **Obračun**: iznos, naknada sa nazivom iz cenovnika, **ukupno sa računa**, stanje posle plaćanja i preostali dnevni limit;
- upozorenja: prvo plaćanje ovom primaocu, plaćanje veće od pola stanja, prekoračenje dnevnog limita;
- **Izmeni podatke** vraća na prvi korak bez gubitka unosa.

**3. Potvrda**
- za veće iznose novom primaocu ili iznose preko tvog ličnog praga traži se **ponovni unos lozinke**;
- priznanica prikazuje status (Izvršen, Poslat u kliring, Zakazan), referencu i iznose; **Štampaj potvrdu** otvara štampu.

Plaćanje u okviru banke izvršava se odmah i besplatno je. Plaćanje drugoj banci ide u simulirani kliring i naplaćuje se naknada iz cenovnika. Dnevni limit je 100.000 RSD, odnosno 1.000 EUR ili USD.

### Nalozi

Lista svih naloga sa filterima: Svi, Zakazani, U kliringu, Izvršeni, Odbijeni, Otkazani. Odbijeni nalog prikazuje razlog. Zakazani nalog se otkazuje dugmetom **Otkaži**, uz potvrdu.

### Transakcije

Filteri: pretraga po opisu, primaocu ili referenci, račun, vrsta, period od i do. **Primeni** pokreće pretragu, **Poništi** briše filtere. Klik na transakciju otvara detalje i link **Nešto nije u redu? Podnesite prigovor**, koji otvara prigovor već povezan sa tom transakcijom.

### Primaoci

Dodavanje, izmena i brisanje sačuvanih primalaca. Dugme **Plati** na kartici otvara nalog sa popunjenim podacima. Brisanje primaoca ne briše izvršena plaćanja.

### Prigovori

- **Novi prigovor**: oblast, naslov i opis od najmanje 20 karaktera. Posle slanja dobijaš broj predmeta i rok za odgovor (15 dana).
- U detalju prigovora vidiš prepisku, rok i istoriju statusa i možeš da dopišeš poruku.
- Ako banka produži rok, videćeš novi datum i razlog.
- Kada je prigovor rešen, odgovor banke je istaknut zelenom bojom i možeš da oceniš uslugu.
- Ako nisi zadovoljan odgovorom, možeš se obratiti Narodnoj banci Srbije.

### Privatnost

| Kartica | Šta se tu radi |
|---|---|
| Podešavanja i saglasnosti | vrste obaveštenja, marketinške poruke (podrazumevano isključene), sakrivanje stanja pri prijavi, lični prag za dodatnu potvrdu lozinkom, istorija saglasnosti |
| Dokumenti | verzije Politike privatnosti i Uslova koje si prihvatio i kada |
| Ko je video moje podatke | svaki put kada zaposleni otvori tvoje podatke: ko, kada, šta i zašto |
| Zahtevi po ZZPL | zahtev za pristup, ispravku, brisanje, ograničenje obrade ili prigovor; rok za odgovor je 30 dana |
| Preuzmi moje podatke | preuzimanje svih podataka u JSON fajlu, uz ponovni unos lozinke |

Bezbednosna obaveštenja ne mogu da se isključe. Podaci o transakcijama čuvaju se propisani broj godina zbog propisa o sprečavanju pranja novca, pa zahtev za brisanje može biti delimično usvojen.

### Pomoć i cenovnik

Česta pitanja sa pretragom, kontakt banke, informacija o Narodnoj banci Srbije i kompletan cenovnik. Ova stranica je dostupna i bez prijave, preko linka na ekranu za prijavu.

## 3. Bankar

### Pregled

Tri kartice sa zadacima: prigovori sa prekoračenim rokom (crveno), novi prigovori i klijenti koji čekaju proveru identiteta. Ispod su statistike za danas i poslednjih 7 dana.

### Klijenti

- pretraga po imenu ili emailu i filter po statusu;
- u svakom redu su dugmad **Detalji**, **Izmeni**, **Otključaj** (ako je nalog zaključan) i **Deaktiviraj** ili **Aktiviraj**;
- **Novi klijent**: ime, prezime, email i početna lozinka (najmanje 8 karaktera, veliko i malo slovo i cifra). Lozinku predaj klijentu lično;
- posle kreiranja otvara se detalj klijenta.

### Detalj klijenta

Svako otvaranje ove stranice beleži se i klijent to vidi u svom odeljku Privatnost.

- **Zaglavlje**: status KYC, procena rizika, status naloga; dugmad **Otključaj**, **Deaktiviraj** i **Aktiviraj**. Deaktivacija prekida sve sesije klijenta.
- **Provera identiteta (KYC)**: izaberi status (Verifikovan, Nije verifikovan, Odbijen) i rizik (nizak, srednji, visok) i sačuvaj.
- **Otvaranje računa**: moguće je tek kada je identitet verifikovan i nalog aktivan. Izaberi valutu i klikni **Otvori račun**.
- **Računi klijenta**:
  - **Uplata** proknjižava gotovinsku uplatu na šalteru. Uplate iznad praga automatski se prijavljuju AML proveri;
  - **Blokiraj** i **Aktiviraj** menjaju status računa;
  - klik na broj računa prikazuje poslednje transakcije.

### Transakcije

Sve transakcije u sistemu, sa pretragom i filterima po vrsti, statusu i periodu. Klik na red otvara detalje.

### Prigovori

- filteri: status, samo prekoračen rok, samo nedodeljeni;
- u detalju:
  - **Preuzmi predmet** dodeljuje prigovor tebi;
  - poruka klijentu ili **interna beleška**, koju klijent ne vidi;
  - **Promena statusa**: kod statusa Rešen ili Odbijen obrazloženje je obavezno i klijent ga dobija kao odgovor;
  - **Produženje roka**: moguće jednom, za još 15 dana, uz obrazloženje.

### Zadovoljstvo

Rezultati anketa za 30 dana, 90 dana ili godinu dana:
- **CSAT**: udeo ocena 4 i 5;
- **NPS**: procenat promotera minus procenat kritičara;
- **CES**: prosečan napor, gde je niže bolje.

Ispod su poslednji odgovori sa komentarima.

## 4. Administrator

Administrator ima sve što i bankar, plus sledeće.

- **Pregled sistema**: zdravlje glavne knjige, broj otvorenih AML upozorenja i ZZPL zahteva, statistike, dugme **Pokreni čišćenje sada** (briše pročitana obaveštenja i istekle sesije po rokovima čuvanja i prikazuje koliko je obrisano).
- **Korisnici**: svi korisnici; pri kreiranju se bira uloga (klijent, bankar, administrator). U svakom redu su i **Otključaj**, **Deaktiviraj** ili **Aktiviraj** i ikonica korpe za **trajno brisanje**. Brisanje radi samo za nalog bez računa, transakcija i prijava (na primer nalog napravljen greškom); za ostale se koristi deaktivacija, jer banka mora da čuva podatke propisani broj godina.
- **Zahtevi za lozinku**: korisnici koji su zaboravili lozinku. Za svaki zahtev vide se podnosilac, uloga, da li je nalog zaključan, IP adresa, uređaj i rok. **Odobri** traži da označiš da si proverio identitet nezavisnim kanalom, a **Odbij** traži razlog. O zahtevu za sopstvenu lozinku mora da odluči drugi administrator.
- **Računi**: svi računi sa vlasnicima, pretraga i promena statusa.
- **Glavna knjiga**:
  - **Bruto bilans** po valuti mora biti uravnotežen (zbir svih knjiženja je nula);
  - **Usklađenost** poredi stanje svakog računa sa zbirom njegovih knjiženja. Ako postoji razlika, prikazuje se u tabeli;
  - **Konta** prikazuje sistemska konta (blagajna, provizije, kliring, usklađivanje), a po izboru i klijentska.
- **AML upozorenja**: filter po statusu, **Obradi** otvara odluku (U proveri, Zatvoren bez nalaza, Eskaliran) sa obaveznim obrazloženjem.
- **Zahtevi po ZZPL**: odgovor podnosiocu sa odlukom; kod odbijanja treba navesti pravni osnov.
- **Audit log**: pretraga, period i samo neuspešne radnje; klik na red prikazuje vrednosti pre i posle izmene.
- **Centar za pomoć**: dodavanje, izmena, skrivanje i brisanje čestih pitanja.

## 5. Bezbednost i privatnost za sve uloge

- **Automatska odjava**: posle 5 minuta bez aktivnosti. Minut ranije pojavljuje se prozor sa odbrojavanjem i dugmetom **Ostajem prijavljen/a**.
- **Bezbednost**: aktivne sesije sa uređajem, IP adresom i vremenom aktivnosti; **Odjavi** za jedan uređaj ili **Odjavi sve druge uređaje**; istorija prijava i bezbednosnih događaja.
- **Profil**: izmena imena i prezimena, promena lozinke (odjavljuje sve druge uređaje), izbor teme, odjava.
- **Sakrivanje iznosa**: ikonica oka u gornjoj traci. Iznosi u obračunu plaćanja se ne sakrivaju, jer moraš da vidiš šta potvrđuješ.
- Banka nikada ne traži lozinku telefonom, porukom ili emailom.

## 6. Česta pitanja i rešavanje problema

| Problem | Rešenje |
|---|---|
| „Sesija je istekla ili je odjavljena sa drugog uređaja“ | prijavi se ponovo; ako nisi ti odjavio uređaj, promeni lozinku |
| Nalog je zaključan | sačekaj 30 minuta ili zamoli bankara da ga otključa |
| Zaboravio sam lozinku | **Zaboravili ste lozinku?** na ekranu za prijavu; nova lozinka važi posle odobrenja banke |
| Administrator je zaboravio lozinku | zahtev mora da odobri drugi administrator; ako postoji samo jedan, lozinku resetuje tehnička podrška direktno u bazi |
| Ne mogu da otvorim račun klijentu | identitet klijenta mora biti verifikovan, a nalog aktivan |
| Plaćanje je odbijeno zbog limita | dnevni limit se računa po valuti i obnavlja se u ponoć po beogradskom vremenu |
| Traži se lozinka pri plaćanju | iznos je veći od praga za novog primaoca ili od tvog ličnog praga |
| Stranica prikazuje „Podaci nisu učitani“ | proveri da li backend radi i klikni **Pokušaj ponovo** |

## 7. Lista za testiranje u pregledaču

Pokreni backend i frontend kako piše u README-u i prolazi redom. Svaka stavka navodi šta treba da se vidi.

### A. Administrator i priprema

- [ ] Prijava administratorom, prihvatanje dokumenata u prozoru **Pre nastavka**.
- [ ] **Korisnici**, **Novi korisnik**, uloga Bankar: korisnik se pojavljuje u listi.
- [ ] **Glavna knjiga**: bruto bilans je uravnotežen, tabela razlika je prazna.
- [ ] Odjava iz menija korisnika.

### A2. Zaboravljena lozinka

- [ ] Na prijavi **Zaboravili ste lozinku?**, unesi email klijenta i novu lozinku: prikazuje se poruka da je zahtev prosleđen.
- [ ] Isti postupak sa nepostojećom adresom: poruka je ista.
- [ ] Prijava klijenta novom lozinkom ne uspeva, starom uspeva; u obaveštenjima je bezbednosno upozorenje o zahtevu.
- [ ] Kao administrator otvori **Zahtevi za lozinku**: zahtev ima IP adresu i uređaj. **Odobri** bez štikliranja provere identiteta ne prolazi; sa štikliranjem prolazi.
- [ ] Klijent se sada prijavljuje novom lozinkom, a stara više ne važi.
- [ ] Pošalji zahtev za lozinku administratora: u redu piše „Odlučuje drugi administrator“.
- [ ] Na **Pregledu sistema** kartica „zahteva za lozinku“ prikazuje broj zahteva koji čekaju.

### A3. Upravljanje korisnicima

- [ ] **Korisnici**: napravi probnog korisnika i obriši ga ikonicom korpe.
- [ ] Pokušaj brisanja klijenta koji ima račune: poruka predlaže deaktivaciju.
- [ ] **Deaktiviraj** klijenta: prijava tim nalogom ne uspeva; **Aktiviraj** vraća pristup.

### B. Bankar

- [ ] Prijava bankarom, prihvatanje dokumenata.
- [ ] **Klijenti**, **Novi klijent**: otvara se detalj klijenta, KYC je „Nije verifikovan“, otvaranje računa nije dozvoljeno.
- [ ] KYC na Verifikovan, rizik Nizak, **Sačuvaj KYC**: pojavljuje se izbor valute.
- [ ] Otvori RSD i EUR račun.
- [ ] **Uplata** od 300.000 RSD na RSD račun: poruka sa referencom, stanje se menja.
- [ ] Napravi još jednog klijenta sa verifikovanim KYC i RSD računom (biće primalac). Zapiši broj računa.

### C. Klijent

- [ ] Prijava klijentom: početna prikazuje stanje, mesečni priliv i transakciju uplate.
- [ ] Ikonica oka: iznosi postaju tačkice; ponovni klik ih vraća.
- [ ] **Računi**, detalj RSD računa: grafikon, promena perioda, **Kopiraj broj**.
- [ ] **Isplata gotovine** od 2.000 RSD: stanje se smanjuje, transakcija se pojavljuje.
- [ ] **Novo plaćanje** drugom klijentu, 1.500 RSD:
  - [ ] pogrešno prepisan broj računa (promeni poslednju cifru) daje grešku o kontrolnom broju;
  - [ ] na proveri je naknada 0, prikazano je upozorenje da je primalac nov;
  - [ ] priznanica ima status Izvršen.
- [ ] Plaćanje istom primaocu od 60.000 RSD: ne traži lozinku jer primalac više nije nov.
- [ ] Plaćanje trećem, novom primaocu od 60.000 RSD: na proveri piše da će se tražiti lozinka; posle potvrde otvara se prozor za lozinku. Pogrešna lozinka daje grešku, ispravna izvršava plaćanje. Pripazi na dnevni limit od 100.000 RSD.
- [ ] Plaćanje na račun druge banke, npr. `160-0000000123456-54` (ispravan kontrolni broj, banka 160): naknada je veća od nule, status Poslat u kliring.
- [ ] Zakazano plaćanje: status Zakazan, u **Nalozima** filter Zakazani, **Otkaži**.
- [ ] **Primaoci**: dodaj, izmeni, **Plati** popunjava formu, obriši.
- [ ] **Transakcije**: filter po vrsti Uplate, **Učitaj još**, detalj transakcije, link za prigovor.
- [ ] **Prigovori**, novi prigovor povezan sa transakcijom: dobija broj i rok; u detalju dopiši poruku.
- [ ] **Obaveštenja**: brojač na zvoncu, **Označi sve kao pročitano**, brojevi računa su skraćeni.
- [ ] **Privatnost**:
  - [ ] uključi marketing i lični prag od 10.000: istorija saglasnosti beleži promenu;
  - [ ] plaćanje od 15.000 sada traži lozinku i kod poznatog primaoca;
  - [ ] **Ko je video moje podatke** prikazuje uvid bankara;
  - [ ] **Zahtevi po ZZPL**: podnesi zahtev za ispravku;
  - [ ] **Preuzmi moje podatke**: traži lozinku i preuzima JSON.
- [ ] **Bezbednost**: aktivna sesija sa oznakom „Ovaj uređaj“. Prijavi se istim nalogom u drugom pregledaču, pa ga ovde odjavi: u drugom pregledaču sledeća radnja vodi na prijavu.
- [ ] Ostavi aplikaciju 4 minuta bez aktivnosti: pojavljuje se odbrojavanje; posle još minut sledi odjava i poruka na ekranu za prijavu.

### D. Ponovo bankar

- [ ] **Prigovori**: novi prigovor je u listi; **Preuzmi predmet**, interna beleška, odgovor klijentu, produženje roka, status Rešen sa obrazloženjem.
- [ ] Kao klijent proveri da interna beleška nije vidljiva, a odgovor jeste.
- [ ] **Zadovoljstvo**: ocena sa početne stranice je u listi.

### E. Ponovo administrator

- [ ] Kao bankar uplati 1.800.000 RSD na račun klijenta (iznad praga za prijavu gotovine od 1.750.000 RSD).
- [ ] **AML upozorenja**: pojavljuje se „Gotovina preko praga za izveštavanje“; **Obradi** sa obrazloženjem.
- [ ] **Zahtevi po ZZPL**: odgovori na zahtev; klijent vidi odgovor.
- [ ] **Audit log**: pretraga „prijava“, klik na red prikazuje detalje.
- [ ] **Centar za pomoć**: dodaj pitanje, proveri ga na stranici Pomoć, obriši ga.
- [ ] **Pregled sistema**: **Pokreni čišćenje sada** prikazuje broj obrisanih zapisa.
- [ ] **Glavna knjiga**: posle svih radnji bilans je i dalje uravnotežen.

### F. Opšte

- [ ] Promena teme u tamnu i nazad.
- [ ] Suženje prozora ispod 960 px: pojavljuju se donja traka i meni preko ikonice.
- [ ] Navigacija samo tastaturom (Tab): fokus je vidljiv, modal se zatvara na Escape.
- [ ] Klijent koji ručno upiše adresu `/admin` vraća se na svoju početnu.
- [ ] Nepostojeća adresa prikazuje stranicu 404.
