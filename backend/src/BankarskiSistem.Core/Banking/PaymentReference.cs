using System.Numerics;
using System.Text.RegularExpressions;

namespace BankarskiSistem.Core.Banking
{
    public static partial class PaymentReference
    {
        public const string ModelNone = "00";
        public const string Model11 = "11";
        public const string Model97 = "97";
        public const int MaxReferenceLength = 25;

        public static readonly IReadOnlyList<string> SupportedModels = [ModelNone, Model11, Model97];

        public static string? Validate(string? model, string? reference)
        {
            if (string.IsNullOrWhiteSpace(model) && string.IsNullOrWhiteSpace(reference))
                return null;

            if (string.IsNullOrWhiteSpace(model))
                return "Uz poziv na broj morate izabrati model.";

            if (!SupportedModels.Contains(model))
                return "Podržani modeli su 00, 11 i 97.";

            if (string.IsNullOrWhiteSpace(reference))
                return model == ModelNone ? null : $"Za model {model} poziv na broj je obavezan.";

            if (reference.Length > MaxReferenceLength || !AllowedCharacters().IsMatch(reference))
                return "Poziv na broj može imati najviše 25 znakova: cifre, slova i crticu.";

            if (model == Model97 && !IsValidModel97(reference))
                return "Kontrolni broj poziva na broj nije ispravan za model 97.";

            if (model == Model11 && !Model11Format().IsMatch(reference))
                return "Za model 11 poziv na broj sme sadržati samo cifre i crtice.";

            return null;
        }

        public static bool IsValidModel97(string reference)
        {
            var compact = reference.Replace("-", string.Empty).ToUpperInvariant();
            if (compact.Length < 3 || !char.IsDigit(compact[0]) || !char.IsDigit(compact[1]))
                return false;

            var control = int.Parse(compact[..2]);
            var payload = compact[2..];
            return CalculateModel97Control(payload) == control;
        }

        public static int CalculateModel97Control(string payload)
        {
            var numeric = string.Concat(payload.ToUpperInvariant().Select(c =>
                char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString()));
            var remainder = (int)(BigInteger.Parse(numeric) * 100 % 97);
            return 98 - remainder;
        }

        [GeneratedRegex("^[0-9A-Za-z-]+$")]
        private static partial Regex AllowedCharacters();

        [GeneratedRegex("^[0-9-]+$")]
        private static partial Regex Model11Format();
    }

    public static class PaymentCodes
    {
        public static readonly IReadOnlyDictionary<string, string> Common = new Dictionary<string, string>
        {
            ["189"] = "Transakcije po nalogu građana (gotovinsko)",
            ["220"] = "Promet robe i usluga, međufazna potrošnja",
            ["221"] = "Promet robe i usluga, finalna potrošnja",
            ["240"] = "Isplata zarada i naknada zarada",
            ["253"] = "Uplata javnih prihoda izuzev poreza i doprinosa",
            ["254"] = "Uplata poreza i doprinosa po odbitku",
            ["263"] = "Ostali transferi",
            ["289"] = "Transakcije po nalogu građana",
            ["290"] = "Druge transakcije"
        };

        public static bool IsValidFormat(string? code) =>
            code is { Length: 3 } && (code[0] == '1' || code[0] == '2') && char.IsDigit(code[1]) && char.IsDigit(code[2]);
    }
}
