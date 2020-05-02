using FluentAssertions;
using Xunit;

namespace NodaMoney.Tests.UnaryOperatorsSpec
{
    public class GivenIWantToIncrementAndDecrementMoneyUnary
    {
        [Theory]
        [MemberData(nameof(TestData))]
        public void WhenIncrementing_ThenAmountShouldIncrementWithMinorUnit(Money money, CurrencyInfo expectedCurrency, decimal expectedDifference)
        {
            decimal amountBefore = money.Amount;

            Money result = ++money;

            result.Currency.Should().Be(expectedCurrency);
            result.Amount.Should().Be(amountBefore + expectedDifference);
            money.Currency.Should().Be(expectedCurrency);
            money.Amount.Should().Be(amountBefore + expectedDifference);
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void WhenDecrementing_ThenAmountShouldDecrementWithMinorUnit(Money money, CurrencyInfo expectedCurrency, decimal expectedDifference)
        {
            decimal amountBefore = money.Amount;

            Money result = --money;

            result.Currency.Should().Be(expectedCurrency);
            result.Amount.Should().Be(amountBefore - expectedDifference);
            money.Currency.Should().Be(expectedCurrency);
            money.Amount.Should().Be(amountBefore - expectedDifference);
        }

        public static TheoryData<Money, CurrencyInfo, decimal> TestData => new TheoryData<Money, CurrencyInfo, decimal>
        {
            { new Money(765m, CurrencyInfo.FromCode("JPY")), CurrencyInfo.FromCode("JPY"), CurrencyInfo.FromCode("JPY").MinimalAmount },
            { new Money(765.43m, CurrencyInfo.FromCode("EUR")), CurrencyInfo.FromCode("EUR"), CurrencyInfo.FromCode("EUR").MinimalAmount },
            { new Money(765.43m, CurrencyInfo.FromCode("USD")), CurrencyInfo.FromCode("USD"), CurrencyInfo.FromCode("USD").MinimalAmount },
            { new Money(765.432m, CurrencyInfo.FromCode("BHD")), CurrencyInfo.FromCode("BHD"), CurrencyInfo.FromCode("BHD").MinimalAmount }
        };
    }

    public class GivenIWantToAddAndSubtractMoneyUnary
    {
        private readonly Money _tenEuroPlus = new Money(10.00m, "EUR");
        private readonly Money _tenEuroMin = new Money(-10.00m, "EUR");

        [Fact]
        public void WhenUsingUnaryPlusOperator_ThenThisSucceed()
        {
            var r1 = +_tenEuroPlus;
            var r2 = +_tenEuroMin;

            r1.Amount.Should().Be(10.00m);
            r1.Currency.Code.Should().Be("EUR");
            r2.Amount.Should().Be(-10.00m);
            r2.Currency.Code.Should().Be("EUR");
        }

        [Fact]
        public void WhenUsingUnaryMinOperator_ThenThisSucceed()
        {
            var r1 = -_tenEuroPlus;
            var r2 = -_tenEuroMin;

            r1.Amount.Should().Be(-10.00m);
            r1.Currency.Code.Should().Be("EUR");
            r2.Amount.Should().Be(10.00m);
            r2.Currency.Code.Should().Be("EUR");
        }
    }
}
