namespace BankarskiSistem.Core.Enums
{
    public enum UserRole
    {
        Administrator = 0,
        Korisnik = 1,
        Bankar = 2
    }

    public enum TransactionType
    {
        Uplata,
        Isplata,
        Transfer,
        EksterniTransfer
    }

    public enum PaymentOrderStatus
    {
        Zakazan,
        UObradi,
        Izvrsen,
        Odbijen,
        Otkazan
    }

    public enum LegalDocumentType
    {
        PolitikaPrivatnosti,
        UsloviKoriscenja
    }

    public enum TransactionStatus
    {
        Uspesna,
        Neuspesna,
        NaCekanju
    }

    public enum AccountStatus
    {
        Aktivan,
        Neaktivan,
        Blokiran
    }

    public enum NotificationType
    {
        Uplata,
        Isplata,
        PrimljenTransfer,
        PoslatTransfer,
        StatusRacuna,
        NoviRacun,
        Bezbednost,
        Prigovor,
        Privatnost,
        TrajniNalog
    }

    public enum KycStatus
    {
        NijePrimenljivo,
        NijeVerifikovan,
        Verifikovan,
        Odbijen
    }

    public enum RiskLevel
    {
        Nizak,
        Srednji,
        Visok
    }

    public enum FeeAppliesTo
    {
        InterniNalog,
        EksterniNalog,
        Isplata,
        Uplata
    }

    public enum FeeCalculationType
    {
        Fiksna,
        Procenat
    }

    public enum ComplaintCategory
    {
        PlatniPromet,
        Racun,
        Naknade,
        Kartice,
        ElektronskoBankarstvo,
        ZastitaPodataka,
        Ostalo
    }

    public enum ComplaintStatus
    {
        Podnet,
        UObradi,
        CekaKorisnika,
        Resen,
        Odbijen,
        Zatvoren
    }

    public enum ComplaintPriority
    {
        Normalan,
        Visok
    }

    public enum SurveyType
    {
        CSAT,
        NPS,
        CES
    }

    public enum PrivacyRequestType
    {
        Ispravka,
        Brisanje,
        Ogranicenje,
        Prigovor,
        Pristup
    }

    public enum PrivacyRequestStatus
    {
        Podnet,
        UObradi,
        Usvojen,
        DelimicnoUsvojen,
        Odbijen
    }

    public enum StandingOrderFrequency
    {
        Nedeljno,
        Mesecno,
        Kvartalno,
        Godisnje
    }

    public enum StandingOrderStatus
    {
        Aktivan,
        Pauziran,
        Zavrsen,
        Otkazan
    }

    public enum StandingOrderExecutionStatus
    {
        Pokrenut,
        Izvrsen,
        Odbijen
    }

    public enum PasswordResetStatus
    {
        Podnet,
        Odobren,
        Odbijen,
        Istekao
    }

    public enum ComplianceAlertStatus
    {
        Otvoren,
        UProveri,
        ZatvorenBezNalaza,
        EskaliranSimulacija
    }

    public enum LedgerAccountType
    {
        Klijent,
        Blagajna,
        Uskladjivanje,
        Menjacnica,
        Provizije,
        Kliring
    }

    public enum StatisticsPeriod
    {
        Week,
        Month,
        Year
    }
}
