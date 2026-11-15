namespace BankarskiSistem.Core.Banking
{
    public static class CardLimits
    {
        public static (decimal Purchase, decimal Atm) Default(string currency) => currency == "RSD" ? (100_000m, 40_000m) : (1_000m, 400m);

        public static (decimal Purchase, decimal Atm) Max(string currency) => currency == "RSD" ? (500_000m, 100_000m) : (5_000m, 1_000m);
    }
}
