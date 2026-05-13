using System.Text.RegularExpressions;

namespace Nimblist.api.Services
{
    internal static class QuantityHelper
    {
        private static readonly Dictionary<string, double> UnicodeFractions = new()
        {
            ["½"] = 0.5,    ["⅓"] = 1.0 / 3, ["⅔"] = 2.0 / 3,
            ["¼"] = 0.25,   ["¾"] = 0.75,
            ["⅛"] = 0.125,  ["⅜"] = 0.375,   ["⅝"] = 0.625,  ["⅞"] = 0.875,
        };

        private static readonly Regex QtyRegex = new(
            @"^(\d+(?:\.\d+)?)?\s*([½⅓⅔¼¾⅛⅜⅝⅞]|\d+/\d+)?\s*(.*)",
            RegexOptions.Compiled);

        /// <summary>
        /// Merges two quantity strings. If both parse to the same unit, adds the amounts;
        /// otherwise concatenates as "existing + incoming".
        /// </summary>
        public static string? Merge(string? existing, string? incoming)
        {
            if (string.IsNullOrWhiteSpace(incoming)) return existing;
            if (string.IsNullOrWhiteSpace(existing)) return incoming;

            var (existAmt, existUnit) = Parse(existing.Trim());
            var (inAmt, inUnit) = Parse(incoming.Trim());

            if (existAmt.HasValue && inAmt.HasValue
                && string.Equals(existUnit, inUnit, StringComparison.OrdinalIgnoreCase))
            {
                var total = existAmt.Value + inAmt.Value;
                var formatted = Format(total);
                return string.IsNullOrEmpty(existUnit) ? formatted : $"{formatted} {existUnit}";
            }

            return $"{existing} + {incoming}";
        }

        private static (double? Amount, string Unit) Parse(string qty)
        {
            var m = QtyRegex.Match(qty);
            if (!m.Success) return (null, "");

            var wholeStr = m.Groups[1].Value;
            var fracStr  = m.Groups[2].Value;
            var unitStr  = m.Groups[3].Value.Trim();

            if (string.IsNullOrEmpty(wholeStr) && string.IsNullOrEmpty(fracStr)) return (null, "");

            double amount = 0;
            if (!string.IsNullOrEmpty(wholeStr) && double.TryParse(wholeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var whole))
                amount += whole;

            if (!string.IsNullOrEmpty(fracStr))
            {
                if (UnicodeFractions.TryGetValue(fracStr, out var uf))
                    amount += uf;
                else if (fracStr.Contains('/'))
                {
                    var parts = fracStr.Split('/');
                    if (parts.Length == 2
                        && double.TryParse(parts[0], out var n)
                        && double.TryParse(parts[1], out var d)
                        && d != 0)
                        amount += n / d;
                }
            }

            return amount == 0 ? (null, "") : (amount, unitStr);
        }

        private static string Format(double n)
        {
            if (n <= 0) return "0";

            var whole = (int)Math.Floor(n);
            var frac  = n - whole;

            (double Val, string Sym)[] knownFracs =
            [
                (0.125, "⅛"), (0.25, "¼"), (1.0 / 3, "⅓"), (0.375, "⅜"),
                (0.5, "½"), (0.625, "⅝"), (2.0 / 3, "⅔"), (0.75, "¾"), (0.875, "⅞"),
            ];

            foreach (var (val, sym) in knownFracs)
                if (Math.Abs(frac - val) < 0.04)
                    return whole > 0 ? $"{whole} {sym}" : sym;

            if (n >= 100) return $"{Math.Round(n)}";
            if (n >= 10)  return $"{Math.Round(n * 2) / 2}";
            if (n >= 1)   return n.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            return n.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
