using Nimblist.api.Services;
using Xunit;

namespace Nimblist.test.Services
{
    public class QuantityHelperTests
    {
        // --- Null / empty guard cases ---

        [Fact]
        public void Merge_BothNull_ReturnsNull()
        {
            var result = QuantityHelper.Merge(null, null);
            Assert.Null(result);
        }

        [Fact]
        public void Merge_IncomingNull_ReturnsExisting()
        {
            var result = QuantityHelper.Merge("2 cups", null);
            Assert.Equal("2 cups", result);
        }

        [Fact]
        public void Merge_ExistingNull_ReturnsIncoming()
        {
            var result = QuantityHelper.Merge(null, "1 cups");
            Assert.Equal("1 cups", result);
        }

        // --- Same-unit addition ---

        [Fact]
        public void Merge_SameWholeNumberUnit_AddsAmounts()
        {
            // "2 cups" + "1 cups" = "3 cups"
            var result = QuantityHelper.Merge("2 cups", "1 cups");
            Assert.Equal("3 cups", result);
        }

        [Fact]
        public void Merge_SameDecimalUnit_AddsAmounts()
        {
            // "1.5 tbsp" + "0.5 tbsp" = "2 tbsp"
            var result = QuantityHelper.Merge("1.5 tbsp", "0.5 tbsp");
            Assert.Equal("2 tbsp", result);
        }

        // --- Different units fall back to concatenation ---

        [Fact]
        public void Merge_DifferentUnits_ConcatenatesWithPlus()
        {
            var result = QuantityHelper.Merge("2 cups", "1 tbsp");
            Assert.Equal("2 cups + 1 tbsp", result);
        }

        // --- Unicode fractions ---

        [Fact]
        public void Merge_UnicodeFractionSameUnit_AddsAmounts()
        {
            // ½ + ½ = 1
            var result = QuantityHelper.Merge("½ cup", "½ cup");
            Assert.Equal("1 cup", result);
        }

        [Fact]
        public void Merge_SlashFractionSameUnit_AddsAmounts()
        {
            // "1/4 tsp" + "1/4 tsp" = 0.25 + 0.25 = 0.5 → "½ tsp"
            // The regex uses (?!/) so the leading digit is not consumed into the whole-number
            // group when immediately followed by '/', leaving the full "1/4" for the fraction group.
            var result = QuantityHelper.Merge("1/4 tsp", "1/4 tsp");
            Assert.Equal("½ tsp", result);
        }

        [Fact]
        public void Merge_MixedNumberSameUnit_AddsAmounts()
        {
            // "1 1/4 cup" + "1 1/4 cup" = 2.5 cups → "2 ½ cup"
            var result = QuantityHelper.Merge("1 1/4 cup", "1 1/4 cup");
            Assert.Equal("2 ½ cup", result);
        }

        // --- No parseable quantity → concatenate ---

        [Fact]
        public void Merge_NoQuantity_ConcatenatesAsStrings()
        {
            // Neither "a pinch" nor "to taste" parse to a numeric quantity
            var result = QuantityHelper.Merge("a pinch", "to taste");
            Assert.Equal("a pinch + to taste", result);
        }

        // --- Mixed whole + fraction same unit ---

        [Fact]
        public void Merge_WholeAndFractionSameUnit_AddsCorrectly()
        {
            // 1 cup + ½ cup = 1.5 cups → formatted as "1 ½ cups"
            var result = QuantityHelper.Merge("1 cup", "½ cup");
            // The Format helper renders 1.5 as "1 ½"
            Assert.Equal("1 ½ cup", result);
        }

        // --- Edge case: empty strings ---

        [Fact]
        public void Merge_BothEmpty_ReturnsBothConcatenated()
        {
            // Both sides are whitespace-only; Merge treats whitespace as null/empty
            // incoming is whitespace → returns existing (also whitespace)
            var result = QuantityHelper.Merge("", "");
            // Whitespace-only incoming → return existing
            Assert.Equal("", result);
        }
    }
}
