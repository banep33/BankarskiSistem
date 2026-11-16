# 0003. Pesimističko zaključavanje i idempotentni ključevi

**Kontekst.** Dva istovremena plaćanja sa istog računa ne smeju proći ako zajedno prelaze stanje. Ponovljen zahtev (dupli klik, ponovni pokušaj posle prekida mreže) ne sme dva puta skinuti novac.

**Odluka.**

- Redovi računa se zaključavaju sa `SELECT ... FOR UPDATE`, uvek po rastućem `Id`, da ne bi nastao deadlock.
- Pozadinski poslovi uzimaju posao sa `FOR UPDATE SKIP LOCKED`, pa dve instance ne obrađuju isti nalog.
- Svaka finansijska operacija prima zaglavlje `Idempotency-Key`. Ponovljen ključ vraća isti rezultat bez novog knjiženja. Trajni nalozi koriste deterministički ključ `TN-{id}-{datum}`.

**Posledice.** Ispravnost ne zavisi od optimističkih ponovnih pokušaja. Zaključavanje je kratko, jer obuhvata samo redove konkretnih računa.

**Alternative.** Optimističko zaključavanje (verzija reda) je odbačeno za stanja: pod opterećenjem na istom računu daje mnogo neuspešnih pokušaja.
