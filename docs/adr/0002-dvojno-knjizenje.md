# 0002. Dvojno knjiženje kao izvor istine za novac

**Kontekst.** Stanje računa kao jedna kolona ne može da dokaže odakle je novac došao, niti da otkrije grešku.

**Odluka.** Svaka finansijska transakcija pravi nalog za knjiženje (`journal_entries`) sa stavkama (`postings`) čiji je zbir uvek nula: potražuje je pozitivno, a duguje negativno. Svaki račun klijenta ima svoje konto, a banka sistemska konta (blagajna, kliring, menjačnica, provizije). Okidači u bazi zabranjuju izmenu i brisanje knjiženja. Ispravka se radi isključivo storno transakcijom, uz princip četiri oka.

**Posledice.** Bruto bilans po valuti mora biti nula, a pogled `v_account_reconciliation` poredi stanje svakog računa sa zbirom njegovih knjiženja. Obe provere vidi administrator, a proverava ih i `verify-backup.ps1`. Izveštaji i izvodi se prave iz knjige, a ne iz tabele transakcija.

**Alternative.** Samo tabela transakcija sa stanjem posle transakcije je odbačena: nema kontrolu zbira i ne pokriva sistemske strane (naknade, kliring).
