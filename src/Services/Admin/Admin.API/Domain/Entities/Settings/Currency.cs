namespace Admin.API.Domain.Entities.Settings
{
    /// <summary>
    /// Danh mục tiền tệ hệ thống. Code là Primary Key (ISO 4217).
    /// Ví dụ: VND, USD, EUR.
    /// </summary>
    public class Currency
    {
        public string Code          { get; private set; } = null!;  // PK — ISO 4217
        public string Name          { get; private set; } = null!;
        public string Symbol        { get; private set; } = null!;  // VD: ₫, $, €
        public int    DecimalPlaces { get; private set; }           // VND=0, USD=2
        public bool   IsActive      { get; private set; }

        private Currency() { }

        public static Currency Create(string code, string name, string symbol, int decimalPlaces = 2)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Currency code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Currency name is required.", nameof(name));
            if (decimalPlaces < 0 || decimalPlaces > 4)
                throw new ArgumentOutOfRangeException(nameof(decimalPlaces), "Decimal places must be 0-4.");

            return new Currency
            {
                Code          = code.Trim().ToUpperInvariant(),
                Name          = name.Trim(),
                Symbol        = symbol.Trim(),
                DecimalPlaces = decimalPlaces,
                IsActive      = true
            };
        }

        public void Update(string name, string symbol, int decimalPlaces)
        {
            Name          = name.Trim();
            Symbol        = symbol.Trim();
            DecimalPlaces = decimalPlaces;
        }

        public void Deactivate() => IsActive = false;
        public void Activate()   => IsActive = true;
    }
}
