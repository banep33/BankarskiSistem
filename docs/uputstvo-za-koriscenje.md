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

Ako je uključena **dvostepena prijava**, posle lozinke se pojavljuje ekran **Potvrda prijave**. Unesi šestocifreni kod iz aplikacije za autentifikaciju na telefonu. Ako telefon nije pri ruci, klikni **Nemam telefon, koristiću rezervni kod** i unesi jedan od rezervnih kodova.

Posle 5 pogrešnih lozinki ili kodova nalog se zaključava na 30 minuta. Otključati ga može bankar ili administrator.

### Koliko traje prijava

- Osvežavanje stranice (F5) ne odjavljuje.
- Zatvaranje pregledača odjavljuje.
- Posle 5 minuta bez aktivnosti aplikacija odjavljuje sama, uz upozorenje minut ranije.
- Prijava najduže traje 8 sati, posle toga se traži nova prijava čak i uz aktivno korišćenje.

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

### Trajni nalozi

Trajni nalog je plaćanje koje banka sama izvršava u zadato vreme, na primer stanarina svakog 1. u mesecu.

- **Trajni nalozi** u meniju prikazuje aktivne i pauzirane naloge kao kartice (primalac, iznos, učestalost, datum sledećeg plaćanja), prosečan mesečni iznos svih aktivnih dinarskih naloga i, ispod, tabelu završenih i otkazanih.
- **Novi trajni nalog**:
  - račun platioca, primalac (može jednim klikom iz sačuvanih primalaca), iznos, šifra plaćanja, svrha, model i poziv na broj;
  - **Učestalost**: nedeljno, mesečno, kvartalno ili godišnje;
  - **Prvo plaćanje**: danas ili datum u narednih godinu dana;
  - **Kraj**: bez kraja, do određenog datuma ili određen broj plaćanja;
  - desno je **Pregled** sa prva četiri datuma plaćanja i ukupnim brojem plaćanja;
  - **Aktiviraj trajni nalog** traži ponovni unos lozinke, jer je trajni nalog trajno ovlašćenje banci.
- Kod sačuvanog primaoca dugme **Trajni nalog** otvara obrazac sa već popunjenim podacima primaoca.
- **Detalj trajnog naloga**: iznos i raspored, tri sledeća datuma (današnji je označen), podaci naloga i **Istorija izvršenja** sa referencom transakcije ili razlogom neuspeha.
  - **Izmeni** menja iznos, svrhu, poziv na broj i kraj (uz potvrdu lozinkom). Primalac, račun i učestalost se ne menjaju; za to otkaži nalog i napravi novi.
  - **Pauziraj** privremeno zaustavlja plaćanja, a **Nastavi** ih vraća. Datumi propušteni tokom pauze se ne plaćaju naknadno.
  - **Otkaži** trajno gasi nalog; izvršena plaćanja ostaju u istoriji.

Kako se izvršava:

- Banka proverava dospele naloge svakog minuta i plaća ih na dan dospeća. Svako plaćanje je običan platni nalog (vidi se u **Nalozi** i **Transakcije**) sa istim naknadama i dnevnim limitom kao ručno plaćanje.
- Ako je datum 29, 30. ili 31. a mesec je kraći, plaćanje ide poslednjeg dana tog meseca, a sledećeg meseca se vraća na izabrani dan.
- Ako na računu nema dovoljno sredstava ili je račun blokiran, plaćanje se ne izvršava, u istoriji se upisuje razlog i stiže obaveštenje. Nalog se ne pokušava ponovo istog dana, nego na sledeći datum. Posle **3 neuspeha zaredom** nalog se automatski pauzira.
- Isti datum se nikada ne plaća dva puta, čak ni ako se servis zaustavi i ponovo pokrene.

### Menjačnica

Konverzija između sopstvenih dinarskih i deviznih računa (EUR, USD), odmah i bez posebne naknade.

- Potrebni su dinarski i bar jedan devizni račun; devizni račun otvara bankar.
- **Sa računa** i **Na račun**: biraju se samo sopstveni aktivni računi. Jedan mora biti dinarski. Dugme sa strelicama menja smer.
- **Iznos koji menjam** ili **Iznos koji želim da dobijem**: možeš da uneseš koliko daješ ili koliko želiš da dobiješ (na primer tačno 100 EUR za put).
- Obračun se pojavljuje odmah dok kucaš: koliko daješ, koliko dobijaš, primenjeni kurs, srednji kurs za poređenje, razlika u odnosu na srednji kurs i stanje posle.
  - Kada kupuješ devize, važi **prodajni** kurs banke; kada ih prodaješ, važi **kupovni**.
  - Iznosi se zaokružuju na pare tako da nikad ne dobiješ više nego što si platio, niti platiš više nego što obračun prikazuje.
- **Potvrdi konverziju** izvršava zamenu. Ako je banka u međuvremenu objavila novu kursnu listu, konverzija se ne izvršava po starom kursu, nego se prikazuje novi obračun da ga proveriš.
- Konverzija ne troši dnevni limit za plaćanja, jer novac ostaje na tvojim računima.
- EUR u USD (i obrnuto) menja se u dva koraka, preko dinarskog računa.
- Desno je važeća **kursna lista**, a ispod **Istorija konverzija**. Svaka konverzija se vidi i u **Transakcije**, kao dve stavke „Menjačnica“ (odliv sa jednog i priliv na drugi račun).
- Na **Početnoj**, ako imaš račune u više valuta, ispod stanja piše ukupno u dinarskoj protivvrednosti po srednjem kursu.
- Kursna lista je javna i vidi se i na stranici **Pomoć i cenovnik**, i bez prijave.

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
- **Trajni nalozi klijenta**: pregled svih trajnih naloga sa statusom, sledećim datumom i razlogom poslednjeg neuspeha, da bi bankar mogao da pomogne klijentu. Izmene vrši samo klijent. Uvid se evidentira u klijentovom odeljku Privatnost.
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
- **Korisnici**: svi korisnici; pri kreiranju se bira uloga (klijent, bankar, administrator). Korisnici sa uključenom dvostepenom prijavom imaju oznaku **2FA** i dugme **Resetuj 2FA** (za izgubljen telefon, posle provere identiteta; sve sesije korisnika se odjavljuju). U svakom redu su i **Otključaj**, **Deaktiviraj** ili **Aktiviraj** i ikonica korpe za **trajno brisanje**. Brisanje radi samo za nalog bez računa, transakcija i prijava (na primer nalog napravljen greškom); za ostale se koristi deaktivacija, jer banka mora da čuva podatke propisani broj godina.
- **Zahtevi za lozinku**: korisnici koji su zaboravili lozinku. Za svaki zahtev vide se podnosilac, uloga, da li je nalog zaključan, IP adresa, uređaj i rok. **Odobri** traži da označiš da si proverio identitet nezavisnim kanalom, a **Odbij** traži razlog. O zahtevu za sopstvenu lozinku mora da odluči drugi administrator.
- **Računi**: svi računi sa vlasnicima, pretraga i promena statusa.
- **Kursna lista**: važeća lista, obrazac **Nova kursna lista** (kupovni, srednji i prodajni kurs za svaku valutu) i istorija kurseva za 90 dana. Pravila: kupovni < srednji < prodajni, razlika između kupovnog i prodajnog najviše 10% srednjeg kursa, a srednji kurs ne sme da se promeni za više od 20% od važećeg (zaštita od greške u kucanju). Nova lista važi odmah. Kursna lista u aplikaciji je simulirana i nije povezana sa kursnom listom NBS.
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
- **Bezbednost**: dvostepena prijava, aktivne sesije sa uređajem, IP adresom i vremenom aktivnosti; **Odjavi** za jedan uređaj ili **Odjavi sve druge uređaje**; istorija prijava i bezbednosnih događaja.
- **Dvostepena prijava (2FA)**: u odeljku Bezbednost klikni **Uključi** i potvrdi lozinku. Na telefonu otvori aplikaciju za autentifikaciju (Google Authenticator, Microsoft Authenticator, Authy), skeniraj QR kod ili ručno unesi ključ, pa upiši kod koji aplikacija prikazuje. Dobićeš 10 rezervnih kodova: preuzmi ih ili prepiši, jer se prikazuju samo jednom i svaki važi jednom. Isti kod iz aplikacije ne može se iskoristiti dva puta. **Novi rezervni kodovi** poništava stare, a **Isključi** traži lozinku i kod. Ako izgubiš i telefon i rezervne kodove, administrator može da isključi dvostepenu prijavu posle provere identiteta.
- **Profil**: izmena imena i prezimena, promena lozinke (odjavljuje sve druge uređaje), izbor teme, odjava.
- **Sakrivanje iznosa**: ikonica oka u gornjoj traci. Iznosi u obračunu plaćanja se ne sakrivaju, jer moraš da vidiš šta potvrđuješ.
- Banka nikada ne traži lozinku telefonom, porukom ili emailom.

## 6. Česta pitanja i rešavanje problema

| Problem | Rešenje |
|---|---|
| „Sesija je istekla ili je odjavljena sa drugog uređaja“ | prijavi se ponovo; ako nisi ti odjavio uređaj, promeni lozinku |
| Nalog je zaključan | sačekaj 30 minuta ili zamoli bankara da ga otključa |
| Konverzija javlja da je kurs promenjen | banka je objavila novu kursnu listu; proveri novi obračun i potvrdi ponovo |
| Ne mogu da menjam EUR u USD | menjaj u dva koraka preko dinarskog računa |
| Trajni nalog se nije izvršio | otvori nalog i pogledaj Istoriju izvršenja: razlog je upisan (najčešće nedovoljno sredstava ili dnevni limit) |
| Trajni nalog je pauziran sam od sebe | bila su 3 neuspeha zaredom; obezbedi sredstva i klikni Nastavi |
| Kod iz aplikacije nije prihvaćen | proveri da je vreme na telefonu podešeno automatski; sačekaj sledeći kod jer se isti kod ne može koristiti dva puta |
| Izgubio sam telefon | prijavi se rezervnim kodom i isključi pa ponovo uključi dvostepenu prijavu; bez rezervnih kodova obrati se banci |
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

### A3. Dvostepena prijava i trajanje prijave

Priprema:

- Potrebna je aplikacija koja pravi šestocifrene kodove: Google Authenticator, Microsoft Authenticator ili Authy na telefonu. Bez telefona radi i KeePassXC ili proširenje za pregledač „Authenticator“; tada se umesto skeniranja QR koda ručno unosi ključ prikazan ispod njega.
- Sat na uređaju mora biti podešen automatski, inače kodovi ne prolaze.
- Testiraj na klijentu, a ne na jedinom administratoru. Administrator ne može sam sebi da resetuje dvostepenu prijavu, pa bi izgubljen telefon bez rezervnih kodova značio da nalogu više niko ne može da pristupi.

Provere:

- [ ] Kao klijent osveži stranicu (F5): ostaješ prijavljen. Zatvori pregledač i otvori ponovo: traži se prijava.
- [ ] **Bezbednost**, **Uključi**: traži lozinku, zatim prikazuje QR kod i ključ.
- [ ] Skeniraj QR kod aplikacijom, upiši pogrešan kod (greška), pa ispravan: prikazuje se 10 rezervnih kodova; preuzmi ih.
- [ ] Odjavi se i prijavi ponovo: posle lozinke traži se kod. Pogrešan kod daje grešku, ispravan otvara aplikaciju.
- [ ] Odmah se odjavi i pokušaj ponovo sa istim kodom pre nego što se promeni: ne prolazi. Sledeći kod iz aplikacije prolazi.
- [ ] Prijava rezervnim kodom radi (može malim slovima i bez crtice); isti rezervni kod drugi put ne radi; u Bezbednosti broj preostalih kodova je 9.
- [ ] U obaveštenjima su bezbednosna obaveštenja o uključivanju i o iskorišćenom rezervnom kodu.
- [ ] Pet pogrešnih kodova zaredom zaključava nalog; bankar ga otključava u **Klijenti**, dugme **Otključaj**.
- [ ] Kao administrator u **Korisnici** klijent ima oznaku 2FA; **Resetuj 2FA**: klijent je odjavljen i sledeća prijava ne traži kod.
- [ ] Ponovo uključi dvostepenu prijavu (u aplikaciji prvo obriši stari nalog, jer je ključ nov). **Novi rezervni kodovi** uz kod iz aplikacije daje novih 10, a stari više ne važe.
- [ ] **Isključi** u Bezbednosti traži lozinku i kod iz aplikacije ili rezervni kod.

### A4. Upravljanje korisnicima

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

### C1. Trajni nalozi

Priprema: klijent sa najmanje 10.000 RSD na računu i drugi klijent kao primalac (ili sačuvani primalac).

- [ ] **Trajni nalozi** je prazan i nudi **Novi trajni nalog**.
- [ ] Novi nalog: primalac, 3.000 RSD, mesečno, prvo plaćanje danas, kraj posle 12 plaćanja. **Pregled** desno pokazuje današnji datum i naredna tri meseca i „Ukupno plaćanja: 12“.
- [ ] Pokušaj sa prvim plaćanjem 31. u mesecu i proveri u pregledu da se u kraćim mesecima pomera na poslednji dan meseca.
- [ ] **Aktiviraj trajni nalog** traži lozinku; posle potvrde otvara se detalj naloga.
- [ ] U roku od oko minut osveži detalj: u **Istoriji izvršenja** je današnje plaćanje sa referencom, sledeće plaćanje je za mesec dana, a stanje računa je umanjeno jednom.
- [ ] Plaćanje se vidi i u **Nalozi** i u **Transakcije**; u **Obaveštenjima** je „Trajni nalog je aktiviran“.
- [ ] **Izmeni**: promeni iznos na 3.500 i broj plaćanja na 6; potvrdi lozinkom; detalj prikazuje nove vrednosti.
- [ ] **Pauziraj**, pa **Nastavi**: status se menja, a lista sledećih datuma ostaje u budućnosti.
- [ ] Kao drugi klijent sa skoro praznim računom napravi trajni nalog za danas na iznos veći od stanja: posle oko minut u istoriji je „Nije izvršen“ sa razlogom, novac nije skinut, a stiže obaveštenje „Trajni nalog nije izvršen“.
- [ ] Kod sačuvanog primaoca klikni **Trajni nalog**: obrazac ima popunjenog primaoca.
- [ ] Kao bankar na detalju klijenta vidi tabelu **Trajni nalozi klijenta**; kao klijent u **Privatnost**, **Ko je video moje podatke** vidi taj uvid.
- [ ] **Otkaži** nalog: status je Otkazan, nalog prelazi u tabelu završenih i otkazanih i više nema dugmad za izmenu.

### C1b. Menjačnica i kursna lista

Priprema: klijent sa dinarskim računom (bar 20.000 RSD) i deviznim EUR računom (bankar ga otvara na detalju klijenta).

- [ ] Odjavljen, otvori **Pomoć i cenovnik** sa ekrana za prijavu: kursna lista je vidljiva.
- [ ] Kao klijent otvori **Menjačnica**: levo je obrazac, desno kursna lista.
- [ ] Sa dinarskog na EUR račun, **Iznos koji želim da dobijem** 100: obračun prikazuje prodajni kurs i da daješ 100 × prodajni kurs dinara.
- [ ] **Potvrdi konverziju**: EUR račun ima 100 EUR, dinarski je umanjen tačno za prikazani iznos; konverzija je u istoriji i u obaveštenjima.
- [ ] Zamenom smera (strelice) prodaj 50 EUR: primenjuje se kupovni kurs.
- [ ] U **Transakcije** filtriraj EUR račun: vide se stavke „Menjačnica“.
- [ ] Na **Početnoj** se pojavljuje ukupno u dinarskoj protivvrednosti.
- [ ] Pokušaj da promeniš više EUR nego što imaš: greška o nedovoljnim sredstvima.
- [ ] Ostavi obračun otvoren, pa kao administrator u **Kursna lista** objavi novu listu sa malo izmenjenim EUR kursom. Kao klijent klikni **Potvrdi konverziju**: stiže poruka da je kurs promenjen i prikazuje se novi obračun.
- [ ] Kao administrator pokušaj da objaviš kupovni kurs veći od srednjeg: greška. Istorija kurseva prikazuje i staru i novu listu.
- [ ] **Glavna knjiga**: bilans je i dalje uravnotežen, a na kontima se vidi devizna pozicija banke i prihod od razlike u kursu.

### C2. Klijent: profil, pomoć i privatnost

- [ ] **Profil**: promeni ime i sačuvaj; pozdrav u gornjoj traci i meni korisnika prikazuju novo ime.
- [ ] **Profil**, **Promena lozinke**: indikator jačine reaguje dok kucaš; posle promene je prijava u drugom pregledaču odjavljena, a stara lozinka više ne važi.
- [ ] **Privatnost**, uključi **Sakrij stanja pri prijavi** i sačuvaj; odjavi se i prijavi ponovo: iznosi su odmah sakriveni.
- [ ] **Pomoć i cenovnik**: pretraga „naknada“ sužava listu pitanja; klik na pitanje otvara odgovor; cenovnik prikazuje naknadu za plaćanje drugoj banci.

### D. Ponovo bankar

- [ ] **Pregled**: kartice zadataka prikazuju broj novih prigovora i klijenata koji čekaju proveru identiteta; klik vodi na odgovarajuću listu.
- [ ] **Transakcije**: pretraga po referenci iz priznanice nalazi plaćanje; klik na red otvara detalje.

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
- [ ] **Računi**: pretraga po imenu klijenta; **Blokiraj** jedan račun. Kao klijent proveri da se blokiran račun ne nudi pri plaćanju; kao administrator **Aktiviraj** ga ponovo.
- [ ] **Glavna knjiga**: posle svih radnji bilans je i dalje uravnotežen.

### F. Opšte

- [ ] Promena teme u tamnu i nazad.
- [ ] Suženje prozora ispod 960 px: pojavljuju se donja traka i meni preko ikonice.
- [ ] Navigacija samo tastaturom (Tab): fokus je vidljiv, modal se zatvara na Escape.
- [ ] Klijent koji ručno upiše adresu `/admin` vraća se na svoju početnu.
- [ ] Nepostojeća adresa prikazuje stranicu 404.
- [ ] Bez prijave: na ekranu za prijavu linkovi **Pomoć i cenovnik**, **Politika privatnosti** i **Uslovi korišćenja** otvaraju stranice bez prijave, a dugme **Prijava** vraća nazad.
