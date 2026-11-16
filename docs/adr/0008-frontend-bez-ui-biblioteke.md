# 0008. Frontend bez UI biblioteke, sa semantičkim tokenima

**Kontekst.** Bankarska aplikacija treba dosledan vizuelni identitet, svetlu i tamnu temu, WCAG AA kontrast i mali paket.

**Odluka.** Vue 3 i TypeScript sa sopstvenim malim skupom komponenti (`AppButton`, `AppCard`, `AppModal`, `AppField`…). Boje se koriste isključivo kroz semantičke tokene (`--color-link`, `--color-emphasis-*`, `--color-sidebar-*`, `--color-contrast-*`, `--color-input-border`) koje svaka tema redefiniše. Raspored koristi zajedničke klase (`.grid-N` prema broju stavki, `.amount-fit` sa container query jedinicama za iznose). Kvalitet čuvaju Vitest testovi i Playwright E2E testovi rasporeda u CI-ju.

**Posledice.** Promena teme ili kontrasta je izmena tokena, a ne stotina fajlova. Paket ostaje mali. E2E testovi sprečavaju da se vrate greške poput praznih kolona ili bočnog pomeranja.

**Alternative.** Gotove UI biblioteke (Vuetify, PrimeVue) su odbačene: veći paket, tuđi vizuelni identitet i teže usklađivanje kontrasta.
