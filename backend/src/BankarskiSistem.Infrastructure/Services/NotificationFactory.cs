using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Infrastructure.Services.Payments;

namespace BankarskiSistem.Infrastructure.Services
{
    internal static class NotificationFactory
    {
        private static string Mask(string accountNumber) =>
            accountNumber.Length >= 4 ? $"***{accountNumber.Replace("-", string.Empty)[^4..]}" : accountNumber;

        public static Notification Deposit(Account account, Transaction transaction, DateTime now) => new()
        {
            UserId = account.UserId,
            Type = NotificationType.Uplata,
            Title = "Uplata na račun",
            Message = $"Na račun {Mask(account.AccountNumber)} uplaćeno je {MoneyRules.Format(transaction.Amount, transaction.Currency)}. " +
                      $"Novo stanje: {MoneyRules.Format(account.Balance, account.Currency)}.",
            TransactionId = transaction.Id,
            CreatedAt = now
        };

        public static Notification Withdrawal(Account account, Transaction transaction, DateTime now) => new()
        {
            UserId = account.UserId,
            Type = NotificationType.Isplata,
            Title = "Isplata sa računa",
            Message = $"Sa računa {Mask(account.AccountNumber)} isplaćeno je {MoneyRules.Format(transaction.Amount, transaction.Currency)}. " +
                      $"Novo stanje: {MoneyRules.Format(account.Balance, account.Currency)}.",
            TransactionId = transaction.Id,
            CreatedAt = now
        };

        public static Notification PaymentSent(Account from, PaymentOrder order, Transaction transaction, DateTime now) => new()
        {
            UserId = from.UserId,
            Type = NotificationType.PoslatTransfer,
            Title = order.IsInternal ? "Plaćanje izvršeno" : "Nalog poslat drugoj banci",
            Message = $"{MoneyRules.Format(order.Amount, order.Currency)} za {order.RecipientName} ({Mask(order.RecipientAccountNumber)}). " +
                      $"Novo stanje: {MoneyRules.Format(from.Balance, from.Currency)}.",
            TransactionId = transaction.Id,
            CreatedAt = now
        };

        public static Notification PaymentReceived(Account from, Account to, PaymentOrder order, Transaction transaction, DateTime now) => new()
        {
            UserId = to.UserId,
            Type = NotificationType.PrimljenTransfer,
            Title = "Primljena uplata",
            Message = $"Na račun {Mask(to.AccountNumber)} stiglo je {MoneyRules.Format(order.Amount, order.Currency)} " +
                      $"sa računa {Mask(from.AccountNumber)}. Svrha: {order.Purpose}.",
            TransactionId = transaction.Id,
            CreatedAt = now
        };

        public static Notification PaymentRejected(PaymentOrder order, DateTime now) => new()
        {
            UserId = order.UserId,
            Type = NotificationType.PoslatTransfer,
            Title = "Zakazani nalog nije izvršen",
            Message = $"Nalog na {MoneyRules.Format(order.Amount, order.Currency)} za {order.RecipientName} nije izvršen. " +
                      $"Razlog: {order.RejectionReason}",
            CreatedAt = now
        };

        public static Notification AccountOpened(Account account, DateTime now) => new()
        {
            UserId = account.UserId,
            Type = NotificationType.NoviRacun,
            Title = "Otvoren novi račun",
            Message = $"Otvoren je račun {Mask(account.AccountNumber)} u valuti {account.Currency}.",
            CreatedAt = now
        };

        public static Notification AccountStatusChanged(Account account, DateTime now) => new()
        {
            UserId = account.UserId,
            Type = NotificationType.StatusRacuna,
            Title = "Promenjen status računa",
            Message = $"Status računa {Mask(account.AccountNumber)} je promenjen u: {account.Status}.",
            CreatedAt = now
        };

        public static Notification LoginLocked(User user, DateTime now) => new()
        {
            UserId = user.Id,
            Type = NotificationType.Bezbednost,
            Title = "Nalog privremeno zaključan",
            Message = "Zabeleženo je više neuspešnih pokušaja prijave, pa je nalog privremeno zaključan. " +
                      "Ako to niste bili vi, odmah promenite lozinku i obratite se banci.",
            CreatedAt = now
        };

        public static Notification PasswordChanged(User user, DateTime now) => new()
        {
            UserId = user.Id,
            Type = NotificationType.Bezbednost,
            Title = "Lozinka je promenjena",
            Message = "Lozinka za vaš nalog je upravo promenjena i sve ostale sesije su odjavljene. " +
                      "Ako to niste bili vi, odmah se obratite banci.",
            CreatedAt = now
        };
    }
}
