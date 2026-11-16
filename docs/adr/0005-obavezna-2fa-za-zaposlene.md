# 0005. Obavezna dvostepena prijava za zaposlene preko nivoa sesije

**Kontekst.** Bankar i administrator vide podatke svih klijenata. NIST SP 800-63B za takve naloge traži nivo AAL2.

**Odluka.** Nivo provere pripada sesiji: `user_sessions.MfaVerified`. Access token nosi claim `amr` (`pwd` ili `mfa`, prema RFC 8176). Politike za zaposlene imaju zahtev `TwoFactorAssuranceRequirement`, koji traži `mfa`. Odbijanje vraća 403 sa tipom `two-factor-required`. Zaposleni bez 2FA može samo da je uključi, posle čega se tekuća sesija nadograđuje. Zaposleni ne mogu sami da isključe 2FA. Pravilo se uključuje podešavanjem `SecuritySettings:RequireTwoFactorForStaff`.

**Posledice.** Isti obrazac kasnije prima i passkey (`amr=hwk`) bez promene politika. Klijenti nisu prinuđeni, ali im je 2FA preporučena.

**Alternative.** Provera zastavice „korisnik ima 2FA“ je odbačena: ne dokazuje da je drugi faktor korišćen u toj sesiji.
