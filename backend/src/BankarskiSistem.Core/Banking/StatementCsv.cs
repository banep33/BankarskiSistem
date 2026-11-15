using System.Globalization;
using System.Text;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Core.Banking
{
    public static class StatementCsv
    {
        private static readonly CultureInfo Serbian = CultureInfo.GetCultureInfo("sr-Latn-RS");

        public static string Write(AccountStatement statement)
        {
            var builder = new StringBuilder();
            builder.Append('﻿');
            AppendRow(builder, "Izvod po računu", statement.AccountNumber);
            AppendRow(builder, "Vlasnik", statement.OwnerName);
            AppendRow(builder, "Valuta", statement.Currency);
            AppendRow(builder, "Period", $"{statement.From:dd.MM.yyyy} - {statement.To:dd.MM.yyyy}");
            AppendRow(builder, "Početno stanje", Amount(statement.OpeningBalance));
            builder.AppendLine();
            AppendRow(builder, "Datum", "Referenca", "Vrsta", "Opis", "Protivstrana", "Duguje", "Potražuje", "Stanje");
            foreach (var line in statement.Lines)
            {
                AppendRow(builder, line.Date.ToString("dd.MM.yyyy HH:mm", Serbian), line.Reference, line.Type?.ToString() ?? "", line.Description,
                    line.Counterparty, line.Debit == 0 ? "" : Amount(line.Debit), line.Credit == 0 ? "" : Amount(line.Credit), Amount(line.BalanceAfter));
            }
            builder.AppendLine();
            AppendRow(builder, "Ukupno duguje", Amount(statement.TotalDebits));
            AppendRow(builder, "Ukupno potražuje", Amount(statement.TotalCredits));
            AppendRow(builder, "Krajnje stanje", Amount(statement.ClosingBalance));
            return builder.ToString();
        }

        public static string EscapeCsv(string value)
        {
            var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
            return safe.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
        }

        private static void AppendRow(StringBuilder builder, params string[] cells) =>
            builder.Append(string.Join(';', cells.Select(EscapeCsv))).Append("\r\n");

        private static string Amount(decimal value) => value.ToString("0.00", Serbian);
    }
}
