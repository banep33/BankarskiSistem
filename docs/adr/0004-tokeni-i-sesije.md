# 0004. Kratkotrajni token u memoriji i rotirajući refresh u HttpOnly kolačiću

**Kontekst.** Token u `localStorage` može da ukrade bilo koji XSS. Dugotrajan token se ne može opozvati.

**Odluka.**

- Access JWT važi 15 minuta i čuva se samo u memoriji pregledača.
- Refresh token je nasumičan i u bazi se čuva samo njegov heš. Nalazi se u kolačiću `HttpOnly`, `SameSite=Strict`, ograničenom na putanju `/api/v1/auth`, uz obavezno zaglavlje `X-BS-Refresh` kao zaštitu od CSRF-a.
- Pri svakoj upotrebi refresh token se menja (rotacija). Ponovna upotreba starog tokena gasi celu sesiju.
- Sesije su na serveru, sa apsolutnim rokom od 8 sati i odjavom posle neaktivnosti, i mogu se opozvati sa liste uređaja.

**Posledice.** Osvežavanje stranice ne odjavljuje korisnika, a ukradeni access token kratko traje. U `localStorage` se čuva samo oznaka da je korisnik prijavljen, bez tokena, da se izbegne nepotreban zahtev za obnovu.

**Alternative.** Klasična serverska sesija u kolačiću je razmatrana. Odbačena je radi jasnog razdvajanja API-ja od frontenda i opoziva po sesiji uz kratko važenje tokena.
