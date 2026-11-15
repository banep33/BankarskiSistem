using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class LedgerAccount
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public LedgerAccountType Type { get; set; }
        public string Currency { get; set; } = "RSD";
        public int? AccountId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Account? Account { get; set; }

        public static LedgerAccount ForCustomer(Account account, DateTime now) => new()
        {
            Code = $"KLI-{account.AccountNumber}",
            Name = $"Racun klijenta {account.AccountNumber}",
            Type = LedgerAccountType.Klijent,
            Currency = account.Currency,
            Account = account,
            CreatedAt = now
        };

        public static LedgerAccount ForSystem(LedgerAccountType type, string currency, DateTime now) => new()
        {
            Code = $"{SystemCodePrefix(type)}-{currency}",
            Name = $"{SystemName(type)} {currency}",
            Type = type,
            Currency = currency,
            CreatedAt = now
        };

        public static string SystemCodePrefix(LedgerAccountType type) => type switch
        {
            LedgerAccountType.Blagajna => "SYS-BLAGAJNA",
            LedgerAccountType.Uskladjivanje => "SYS-USKLADJIVANJE",
            LedgerAccountType.Menjacnica => "SYS-MENJACNICA",
            LedgerAccountType.Provizije => "SYS-PROVIZIJE",
            LedgerAccountType.Kliring => "SYS-KLIRING",
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Klijentsko konto nije sistemsko.")
        };

        private static string SystemName(LedgerAccountType type) => type switch
        {
            LedgerAccountType.Blagajna => "Blagajna banke",
            LedgerAccountType.Uskladjivanje => "Neuskladjena pocetna stanja",
            LedgerAccountType.Menjacnica => "Devizna pozicija banke",
            LedgerAccountType.Provizije => "Prihodi od provizija",
            LedgerAccountType.Kliring => "Kliring prema drugim bankama",
            _ => type.ToString()
        };
    }

    public class JournalEntry
    {
        private readonly List<Posting> _postings = [];

        public int Id { get; set; }
        public int? TransactionId { get; set; }
        public string EntryType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPosted { get; private set; }
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PostedAt { get; private set; }

        public IReadOnlyCollection<Posting> Postings => _postings;

        public static JournalEntry Create(string entryType, string description, int? createdByUserId, DateTime now) => new()
        {
            EntryType = entryType,
            Description = description.Length > 255 ? description[..255] : description,
            CreatedByUserId = createdByUserId,
            CreatedAt = now
        };

        public JournalEntry Debit(int ledgerAccountId, decimal amount, string currency, decimal? balanceAfter = null) =>
            AddPosting(ledgerAccountId, -amount, currency, balanceAfter);

        public JournalEntry Credit(int ledgerAccountId, decimal amount, string currency, decimal? balanceAfter = null) =>
            AddPosting(ledgerAccountId, amount, currency, balanceAfter);

        public void MarkPosted(DateTime now)
        {
            EnsureBalanced();
            IsPosted = true;
            PostedAt = now;
        }

        public void EnsureBalanced()
        {
            if (_postings.Count < 2)
                throw new InvalidOperationException("Nalog za knjizenje mora imati najmanje dve stavke.");

            var unbalanced = _postings.GroupBy(p => p.Currency).FirstOrDefault(g => g.Sum(p => p.Amount) != 0);
            if (unbalanced != null)
                throw new InvalidOperationException($"Nalog za knjizenje nije uravnotezen u valuti {unbalanced.Key}.");
        }

        private JournalEntry AddPosting(int ledgerAccountId, decimal signedAmount, string currency, decimal? balanceAfter)
        {
            if (IsPosted)
                throw new InvalidOperationException("Proknjizenom nalogu se ne mogu dodavati stavke.");
            if (signedAmount == 0)
                throw new InvalidOperationException("Stavka knjiženja ne može imati iznos nula.");

            _postings.Add(new Posting
            {
                LedgerAccountId = ledgerAccountId,
                Amount = signedAmount,
                Currency = currency,
                BalanceAfter = balanceAfter,
                CreatedAt = CreatedAt
            });
            return this;
        }
    }

    public class Posting
    {
        public long Id { get; set; }
        public int JournalEntryId { get; set; }
        public int LedgerAccountId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RSD";
        public decimal? BalanceAfter { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public LedgerAccount? LedgerAccount { get; set; }
    }
}
