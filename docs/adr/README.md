# Zapisi arhitektonskih odluka (ADR)

Svaki zapis opisuje jednu važnu odluku: kontekst, odluku, posledice i razmotrene alternative. Zapisi se ne menjaju naknadno. Ako se odluka promeni, piše se novi zapis koji zamenjuje stari.

| Broj | Odluka | Status |
|---|---|---|
| [0001](0001-modularni-monolit-u-slojevima.md) | Modularni monolit u slojevima (Core, Infrastructure, API) | prihvaćeno |
| [0002](0002-dvojno-knjizenje.md) | Dvojno knjiženje kao izvor istine za novac | prihvaćeno |
| [0003](0003-zakljucavanje-i-idempotentnost.md) | Pesimističko zaključavanje i idempotentni ključevi | prihvaćeno |
| [0004](0004-tokeni-i-sesije.md) | Kratkotrajni token u memoriji i rotirajući refresh u HttpOnly kolačiću | prihvaćeno |
| [0005](0005-obavezna-2fa-za-zaposlene.md) | Obavezna dvostepena prijava za zaposlene preko nivoa sesije (`amr`) | prihvaćeno |
| [0006](0006-sql-migracije.md) | Ručno pisane SQL migracije sa kontrolnim sumama | prihvaćeno |
| [0007](0007-vreme-u-utc.md) | Vreme se čuva u UTC-u, poslovni dan po beogradskom kalendaru | prihvaćeno |
| [0008](0008-frontend-bez-ui-biblioteke.md) | Frontend bez UI biblioteke, sa semantičkim tokenima | prihvaćeno |
