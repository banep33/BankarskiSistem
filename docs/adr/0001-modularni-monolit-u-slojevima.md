# 0001. Modularni monolit u slojevima

**Kontekst.** Sistem upravlja računima, plaćanjima, karticama, štednjom i usklađenošću. Tim je mali, a sve operacije nad novcem moraju biti atomične u jednoj bazi.

**Odluka.** Jedna aplikacija (monolit) podeljena u tri sloja:

- **Core**: entiteti, pravila (`MoneyRules`, `CardLimits`, `StatementCsv`, `AuthMethods`), interfejsi servisa i repozitorijuma. Ne zavisi ni od čega.
- **Infrastructure**: EF Core i MySQL, implementacije servisa, pozadinski poslovi, kriptografija.
- **API**: kontroleri, ugovori (DTO), validacija i bezbednosni filteri. Za Infrastructure zna samo kompozicioni koren (registracija servisa).

**Posledice.** Transakcija baze obuhvata ceo slučaj upotrebe (plaćanje, knjiženje, obaveštenje), bez distribuiranih transakcija. Pravila se testiraju bez baze. Moduli (plaćanja, kartice, štednja) su odvojeni servisima, pa se kasnije mogu izdvojiti ako za to postoji izmeren razlog.

**Alternative.** Mikroservisi su odbačeni: uveli bi mrežne pozive, eventualnu konzistentnost i Saga obrazac za tokove novca, a nijedan izmeren problem to ne opravdava.
