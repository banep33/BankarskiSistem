using System.Text.RegularExpressions;

namespace BankarskiSistem.Core.Banking
{
    public readonly partial record struct SerbianAccountNumber
    {
        public const int BankCodeLength = 3;
        public const int AccountPartLength = 13;
        public const int ControlLength = 2;

        public string BankCode { get; }
        public string AccountPart { get; }
        public string Control { get; }

        private SerbianAccountNumber(string bankCode, string accountPart, string control)
        {
            BankCode = bankCode;
            AccountPart = accountPart;
            Control = control;
        }

        public string Formatted => $"{BankCode}-{AccountPart}-{Control}";

        public bool HasValidControlDigits => Control == CalculateControl(BankCode, AccountPart);

        public override string ToString() => Formatted;

        public static bool TryParse(string? input, out SerbianAccountNumber accountNumber)
        {
            accountNumber = default;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var compact = input.Trim().Replace(" ", string.Empty);
            var dashed = DashedPattern().Match(compact);
            if (dashed.Success)
            {
                accountNumber = new SerbianAccountNumber(
                    dashed.Groups["bank"].Value,
                    dashed.Groups["account"].Value.PadLeft(AccountPartLength, '0'),
                    dashed.Groups["control"].Value);
                return true;
            }

            if (DigitsOnlyPattern().IsMatch(compact))
            {
                accountNumber = new SerbianAccountNumber(compact[..3], compact[3..16], compact[16..]);
                return true;
            }

            return false;
        }

        public static SerbianAccountNumber Create(string bankCode, long accountSequence)
        {
            var accountPart = accountSequence.ToString().PadLeft(AccountPartLength, '0');
            return new SerbianAccountNumber(bankCode, accountPart, CalculateControl(bankCode, accountPart));
        }

        public static string CalculateControl(string bankCode, string accountPart)
        {
            var number = long.Parse(bankCode + accountPart);
            var remainder = (int)(number % 97 * 100 % 97);
            return (98 - remainder).ToString("D2");
        }

        [GeneratedRegex(@"^(?<bank>\d{3})-(?<account>\d{1,13})-(?<control>\d{2})$")]
        private static partial Regex DashedPattern();

        [GeneratedRegex(@"^\d{18}$")]
        private static partial Regex DigitsOnlyPattern();
    }
}
