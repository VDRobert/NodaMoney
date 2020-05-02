using System;
using FluentAssertions;
using Xunit;
using NodaMoney.Tests.Helpers;

namespace NodaMoney.Tests.CurrencyBuilderSpec
{
    [Collection(nameof(NoParallelization))]
    public class GivenIWantToCreateCustomCurrency
    {
        [Fact]
        public void WhenRegisterAsSimpleAsPossible_ThenShouldBeAvailableWithDefaults()
        {
            CurrencyInfo result = new CurrencyBuilder("BTC4", "ISO-4217").Register();

            CurrencyInfo bitcoin = CurrencyInfo.FromCode("BTC4");
            bitcoin.Namespace.Should().Be("ISO-4217");
            bitcoin.Symbol.Should().Be(CurrencyInfo.GenericCurrencySign); // ¤
            bitcoin.Should().BeEquivalentTo(result);
        }

        [Fact]
        public void WhenRegisterBitCoinInIsoNamespace_ThenShouldBeAvailable()
        {
            var builder = new CurrencyBuilder("BTC", "ISO-4217")
            {
                EnglishName = "Bitcoin",
                Symbol = "฿",
                NumericCode = "123", // iso number
                DecimalDigits = 8
            };

            CurrencyInfo result = builder.Register();

            CurrencyInfo bitcoin = CurrencyInfo.FromCode("BTC");
            bitcoin.Symbol.Should().Be("฿");
            bitcoin.Should().BeEquivalentTo(result);
        }

        [Fact]
        public void WhenRegisterBitCoin_ThenShouldBeAvailableByExplicitNamespace()
        {
            var builder = new CurrencyBuilder("BTC1", "virtual")
            {
                EnglishName = "Bitcoin",
                Symbol = "฿",
                NumericCode = "123", // iso number
                DecimalDigits = 8
            };

            CurrencyInfo result = builder.Register();

            CurrencyInfo bitcoin = CurrencyInfo.FromCode("BTC1", "virtual");
            bitcoin.Symbol.Should().Be("฿");
            bitcoin.Should().BeEquivalentTo(result);
        }

        [Fact]
        public void WhenBuildBitCoin_ThenItShouldSucceedButNotBeRegistered()
        {
            var builder = new CurrencyBuilder("BTC2", "virtual")
            {
                EnglishName = "Bitcoin",
                Symbol = "฿",
                NumericCode = "123", // iso number
                DecimalDigits = 8
            };

            CurrencyInfo result = builder.Build();
            result.Symbol.Should().Be("฿");

            Action action = () => CurrencyInfo.FromCode("BTC2", "virtual");
            action.Should().Throw<InvalidCurrencyException>().WithMessage("BTC2 is an unknown virtual currency code!");
        }

        [Fact]
        public void WhenFromExistingCurrency_ThenThisShouldSucceed()
        {
            var builder = new CurrencyBuilder("BTC3", "virtual");

            var euro = CurrencyInfo.FromCode("EUR");
            builder.LoadDataFromCurrency(euro);

            builder.Code.Should().Be("BTC3");
            builder.Namespace.Should().Be("virtual");
            builder.EnglishName.Should().Be(euro.EnglishName);
            builder.Symbol.Should().Be(euro.Symbol);
            builder.NumericCode.Should().Be(euro.NumericCode);
            builder.DecimalDigits.Should().Be(euro.DecimalDigits);
            builder.ValidFrom.Should().Be(euro.ValidFrom);
            builder.ValidTo.Should().Be(euro.ValidTo);
        }

        [Fact]
        public void WhenRegisterExistingCurrency_ThenThrowException()
        {
            var builder = new CurrencyBuilder("EUR", "ISO-4217");

            var euro = CurrencyInfo.FromCode("EUR");
            builder.LoadDataFromCurrency(euro);

            Action action = () => builder.Register();

            action.Should().Throw<InvalidCurrencyException>().WithMessage("*is already registered*");
        }

        [Fact]
        public void WhenCodeIsNull_ThenThrowException()
        {
            Action action = () => new CurrencyBuilder(null, "virtual");

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenCodeIsEmpty_ThenThrowException()
        {
            Action action = () => new CurrencyBuilder("", "virtual");

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenNamespaceIsNull_ThenThrowException()
        {
            Action action = () => new CurrencyBuilder("BTC4", null);

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenNamespaceIsEmpty_ThenThrowException()
        {
            Action action = () => new CurrencyBuilder("BTC4", "");

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenSymbolIsEmpty_ThenSymbolMustBeDefaultCurrencySign()
        {
            CurrencyInfo result = new CurrencyBuilder("BTC5", "ISO-4217")
            {
                EnglishName = "Bitcoin",
                //Symbol = "฿",
                NumericCode = "123", // iso number
                DecimalDigits = 8,
            }.Register();

            CurrencyInfo bitcoin = CurrencyInfo.FromCode("BTC5");
            bitcoin.Symbol.Should().Be(CurrencyInfo.GenericCurrencySign); // ¤
            bitcoin.Should().BeEquivalentTo(result);
        }
    }

    [Collection(nameof(NoParallelization))]
    public class GivenIWantToUnregisterCurrency
    {
        [Fact]
        public void WhenUnregisterIsoCurrency_ThenThisMustSucceed()
        {
            var money = CurrencyInfo.FromCode("PEN"); // should work

            CurrencyBuilder.Unregister("PEN", "ISO-4217");
            Action action = () => CurrencyInfo.FromCode("PEN");

            action.Should().Throw<InvalidCurrencyException>().WithMessage("*unknown*currency*");
        }

        [Fact]
        public void WhenUnregisterCustomCurrency_ThenThisMustSucceed()
        {
            var builder = new CurrencyBuilder("XYZ", "virtual")
            {
                EnglishName = "Xyz",
                Symbol = "฿",
                NumericCode = "123", // iso number
                DecimalDigits = 4
            };

            builder.Register();
            CurrencyInfo xyz = CurrencyInfo.FromCode("XYZ", "virtual"); // should work

            CurrencyBuilder.Unregister("XYZ", "virtual");
            Action action = () => CurrencyInfo.FromCode("XYZ", "virtual");

            action.Should().Throw<InvalidCurrencyException>().WithMessage("*unknown*currency*");
        }

        [Fact]
        public void WhenCurrencyDoesNotExist_ThenThrowException()
        {
            Action action = () => CurrencyBuilder.Unregister("ABC", "virtual");

            action.Should().Throw<InvalidCurrencyException>().WithMessage("*specifies a currency that is not found*");
        }

        [Fact]
        public void WhenCodeIsNull_ThenThrowException()
        {
            Action action = () => CurrencyBuilder.Unregister(null, "ISO-4217");

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenCodeIsEmpty_ThenThrowException()
        {
            Action action = () => CurrencyBuilder.Unregister("", "ISO-4217");

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenNamespaceIsNull_ThenThrowException()
        {
            Action action = () => CurrencyBuilder.Unregister("EUR", null);

            action.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WhenNamespaceIsEmpty_ThenThrowException()
        {
            Action action = () => CurrencyBuilder.Unregister("EUR", "");

            action.Should().Throw<ArgumentNullException>();
        }
    }

    [Collection(nameof(NoParallelization))]
    public class GivenIWantToReplaceIsoCurrencyWithOwnVersion
    {
        [Fact]
        public void WhenReplacingEuroWithCustom_ThenThisShouldSucceed()
        {
            // Panamanian balboa
            CurrencyInfo oldEuro = CurrencyBuilder.Unregister("PAB", "ISO-4217");

            var builder = new CurrencyBuilder("PAB", "ISO-4217");
            builder.LoadDataFromCurrency(oldEuro);
            builder.EnglishName = "New Panamanian balboa";
            builder.DecimalDigits = 1;

            builder.Register();

            CurrencyInfo newEuro = CurrencyInfo.FromCode("PAB");
            newEuro.Symbol.Should().Be("B/.");
            newEuro.EnglishName.Should().Be("New Panamanian balboa");
            newEuro.DecimalDigits.Should().Be(1);
        }
    }
}
