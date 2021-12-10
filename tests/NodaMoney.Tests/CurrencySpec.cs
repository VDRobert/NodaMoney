﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using System.Xml.Serialization;

namespace NodaMoney.Tests.CurrencySpec
{
    public class GivenIWantToKnowAllCurrencies
    {
        [Fact]
        public void WhenAskingForIt_ThenAllCurrenciesShouldBeReturned()
        {
            var currencies = Currency.GetAllCurrencies();

            currencies.Should().NotBeEmpty();
            currencies.Count().Should().BeGreaterThan(100);
        }

        [Fact(Skip = "For debugging.")]
        public void WriteAllRegionsToFile()
        {
            using (var stream = File.Open(@"..\..\Regions.txt", FileMode.Create))
            using (var writer = new StreamWriter(stream))
            {
                foreach (var c in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                {
                    var reg = new RegionInfo(c.LCID);
                    writer.WriteLine("CultureName: {0}", c.Name);
                    writer.WriteLine("CultureEnglishName: {0}", c.EnglishName);
                    writer.WriteLine("Name: {0}", reg.Name);
                    writer.WriteLine("NativeName: {0}", reg.NativeName);
                    writer.WriteLine("EnglishName: {0}", reg.EnglishName);
                    writer.WriteLine("DisplayName: {0}", reg.DisplayName);
                    writer.WriteLine("CurrencySymbol: {0}", reg.CurrencySymbol);
                    writer.WriteLine("ISOCurrencySymbol: {0}", reg.ISOCurrencySymbol);
                    writer.WriteLine("CurrencyEnglishName: {0}", reg.CurrencyEnglishName);
                    writer.WriteLine("CurrencyNativeName: {0}", reg.CurrencyNativeName);
                    writer.WriteLine(string.Empty);
                }
            }
        }

        [Fact(Skip = "For debugging.")]
        public void WriteAllCurrenciesToFile()
        {
            using (var stream = File.Open(@"..\..\ISOCurrencies1.txt", FileMode.Create))
            using (var writer = new StreamWriter(stream))
            {
                foreach (var currency in Currency.GetAllCurrencies())
                {
                    writer.WriteLine("EnglishName: {0}", currency.EnglishName);
                    writer.WriteLine("Code: {0}, Number: {1}, Sign: {2}", currency.Code, currency.Number, currency.Symbol);
                    writer.WriteLine(
                        "MajorUnit: {0}, MinorUnit: {1}, DecimalDigits: {2}",
                        currency.MajorUnit,
                        currency.MinorUnit,
                        currency.DecimalDigits);
                    writer.WriteLine(string.Empty);
                }
            }
        }

        [Fact(Skip = "For debugging.")]
        public void WriteAllCurrencySymbolsToFile()
        {
            var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
            var symbolLookup = new Dictionary<String, String>();

            using (var stream = File.Open(@"..\..\ISOSymbols.txt", FileMode.Create))
            using (var writer = new StreamWriter(stream))
            {
                foreach (var culture in cultures)
                {
                    var regionInfo = new RegionInfo(culture.LCID);
                    symbolLookup[regionInfo.ISOCurrencySymbol] = regionInfo.CurrencySymbol;
                }

                foreach (var keyvalue in symbolLookup.OrderBy(s => s.Key))
                {
                    writer.WriteLine("Code: {0}, Sign: {1}", keyvalue.Key, keyvalue.Value);
                    writer.WriteLine(string.Empty);
                }
            }
        }
    }

    public class GivenIWantCurrencyFromIsoCode
    {
        [Fact]
        public void WhenIsoCodeIsExisting_ThenCreatingShouldSucceed()
        {
            var currency = Currency.FromCode("EUR");

            currency.Should().NotBeNull();
            currency.Symbol.Should().Be("€");
            currency.Code.Should().Be("EUR");
            currency.EnglishName.Should().Be("Euro");
            currency.IsObsolete.Should().BeFalse();
        }

        [Fact]
        public void WhenIsoCodeIsUnknown_ThenCreatingShouldThrow()
        {
            Action action = () => Currency.FromCode("AAA");

            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void WhenIsoCodeIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => Currency.FromCode(null);

            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void WhenEstionianKrone_ThenItShouldBeObsolete()
        {
            var currency = Currency.FromCode("EEK");

            currency.Should().NotBeNull();
            currency.Symbol.Should().Be("kr");
            currency.IsObsolete.Should().BeTrue();
        }

    }

    public class GivenIWantCurrencyFromRegionOrCulture
    {
        [Fact]
        public void WhenUsingRegionInfo_ThenCreatingShouldSucceed()
        {
            var currency = Currency.FromRegion(new RegionInfo("NL"));

            currency.Should().NotBeNull();
            currency.Symbol.Should().Be("€");
            currency.Code.Should().Be("EUR");
            currency.EnglishName.Should().Be("Euro");
        }

        [Fact]
        public void WhenRegionInfoIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => Currency.FromRegion((RegionInfo)null);

            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void WhenUsingRegionName_ThenCreatingShouldSucceed()
        {
            var currency = Currency.FromRegion("NL");

            currency.Should().NotBeNull();
            currency.Symbol.Should().Be("€");
            currency.Code.Should().Be("EUR");
            currency.EnglishName.Should().Be("Euro");
        }

        [Fact]
        public void WhenUsingRegionNameThatIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => Currency.FromRegion((string)null);

            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void WhenUsingCultureInfo_ThenCreatingShouldSucceed()
        {
            var currency = Currency.FromCulture(CultureInfo.CreateSpecificCulture("nl-NL"));

            currency.Should().NotBeNull();
            currency.Symbol.Should().Be("€");
            currency.Code.Should().Be("EUR");
            currency.EnglishName.Should().Be("Euro");
        }

        [Fact]
        public void WhenCultureInfoIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => Currency.FromCulture(null);

            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void WhenCultureInfoIsNeutralCulture_ThenCreatingShouldThrow()
        {
            Action action = () => Currency.FromCulture(new CultureInfo("en"));

            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void WhenUsingCultureName_ThenCreatingShouldSucceed()
        {
            var currency = Currency.FromRegion("nl-NL");

            currency.Should().NotBeNull();
            currency.Symbol.Should().Be("€");
            currency.Code.Should().Be("EUR");
            currency.EnglishName.Should().Be("Euro");
        }

        [Fact]
        public void WhenUsingCurrentCurrency_ThenCreatingShouldSucceed()
        {
            var currency = Currency.CurrentCurrency;

            currency.Should().Be(Currency.FromRegion(RegionInfo.CurrentRegion));
        }
    }

    public class GivenIWantToCompareCurrencies
    {
        private Currency _euro1 = Currency.FromCode("EUR");

        private Currency _euro2 = Currency.FromCode("EUR");

        private Currency _dollar = Currency.FromCode("USD");

        [Fact]
        public void WhenComparingEquality_ThenCurrencyShouldBeEqual()
        {
            // Compare using Equal()
            _euro1.Should().Be(_euro2);
            _euro1.Should().NotBe(_dollar);
            _euro1.Should().NotBeNull();
            _euro1.Should().NotBe(new object(), "comparing Currency to a different object should fail!");
        }

        [Fact]
        public void WhenComparingStaticEquality_ThenCurrencyShouldBeEqual()
        {
            // Compare using static Equal()
            Currency.Equals(_euro1, _euro2).Should().BeTrue();
            Currency.Equals(_euro1, _dollar).Should().BeFalse();
        }

        [Fact]
        public void WhenComparingWithEqualityOperator_ThenCurrencyShouldBeEqual()
        {
            // Compare using Euality operators
            (_euro1 == _euro2).Should().BeTrue();
            (_euro1 != _dollar).Should().BeTrue();
        }

        [Fact]
        public void WhenComparingHashCodes_ThenCurrencyShouldBeEqual()
        {
            // Compare using GetHashCode()
            _euro1.GetHashCode().Should().Be(_euro2.GetHashCode());
            _euro1.GetHashCode().Should().NotBe(_dollar.GetHashCode());
        }
    }

    public class GivenIWantToKnowMinorUnit
    {
        private Currency _eur = Currency.FromCode("EUR");

        private Currency _yen = Currency.FromCode("JPY");

        private Currency _din = Currency.FromCode("BHD");

        private Currency _mga = Currency.FromCode("MGA"); // Malagasy ariary

        private Currency _xau = Currency.FromCode("XAU"); // Gold            

        [Fact]
        public void WhenAskingForEuro_ThenMinorUnitShouldBeOneCent()
        {
            _eur.MajorUnit.Should().Be(1m);
            _eur.MinorUnit.Should().Be(0.01m);
            _eur.DecimalDigits.Should().Be(2);
        }

        [Fact]
        public void WhenAskingForYen_ThenMinorUnitShouldBeOne()
        {
            _yen.MajorUnit.Should().Be(1m);
            _yen.MinorUnit.Should().Be(1m);
            _yen.DecimalDigits.Should().Be(0);
        }

        [Fact]
        public void WhenAskingForDinar_ThenMinorUnitShouldBeOneFils()
        {
            _din.MajorUnit.Should().Be(1m);
            _din.MinorUnit.Should().Be(0.001m);
            _din.DecimalDigits.Should().Be(3);
        }

        [Fact]
        public void WhenAskingForGold_ThenMinorUnitShouldBeOne()
        {
            _xau.MajorUnit.Should().Be(1m);
            _xau.MinorUnit.Should().Be(1m);
            _xau.DecimalDigits.Should().Be(-1); // DOT
        }

        [Fact]
        public void WhenAskingForMalagasyAriary_ThenMinorUnitShouldBeOneFith()
        {
            // The Malagasy ariary are technically divided into five subunits, where the coins display "1/5" on their face and
            // are referred to as a "fifth"; These are not used in practice, but when written out, a single significant digit
            // is used. E.g. 1.2 UM.
            _mga.MajorUnit.Should().Be(1m);
            _mga.MinorUnit.Should().Be(0.2m);
            _mga.DecimalDigits.Should().Be(0.69897000433601880478626110527551); // Z07:  Math.Log10(5);
        }
    }

    public class GivenIWantToInitiateInternallyACurrency
    {
        [Fact]
        public void WhenParamsAreCorrect_ThenCreatingShouldSucceed()
        {
            var eur = new Currency("EUR", "978", 2, "Euro", "€");

            eur.Code.Should().Be("EUR");
            eur.Number.Should().Be("978");
            eur.DecimalDigits.Should().Be(2);
            eur.EnglishName.Should().Be("Euro");
            eur.Symbol.Should().Be("€");
        }

        [Fact]
        public void WhenCodeIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => { var eur = new Currency(null, "978", 2, "Euro", "€"); };

            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void WhenNumberIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => { var eur = new Currency("EUR", null, 2, "Euro", "€"); };

            action.ShouldThrow<ArgumentNullException>();
        }

        [Fact]
        public void WhenEnglishNameIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => { var eur = new Currency("EUR", "978", 2, null, "€"); };

            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void WhenSignIsNull_ThenCreatingShouldThrow()
        {
            Action action = () => { var eur = new Currency("EUR", "978", 2, "Euro", null); };

            action.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void WhenDecimalDigitIsLowerThenMinusOne_ThenCreatingShouldThrow()
        {
            Action action = () => { var eur = new Currency("EUR", "978", -2, "Euro", "€"); };

            action.ShouldThrow<ArgumentOutOfRangeException>();
        }


        [Fact]
        public void WhenDefaultIsSetItShouldBeUSed()
        {
            var defaultRegionInfo = new RegionInfo("FR-fr");
            Currency.DefaultRegionInfo = defaultRegionInfo;
            Currency.CurrentCurrency.Code.Should().Be(defaultRegionInfo.ISOCurrencySymbol);
            //RESET to ensure that following tests work
            Currency.DefaultRegionInfo = null;
        }

        [Fact]
        public void WhenNoDefaultIsSetCurrentRegionShouldBeUsed()
        {
            Currency.CurrentCurrency.Code.Should().Be(RegionInfo.CurrentRegion.ISOCurrencySymbol);
        }
    }

    public class GivenIWantToSerializeCurrencyWitXmlSerializer
    {
        private Currency yen = Currency.FromCode("JPY");

        private Currency euro = Currency.FromCode("EUR");

        [Fact]
        public void WhenSerializingYen_ThenThisShouldSucceed()
        {
            //Console.WriteLine(StreamToString(Serialize(yen)));
            StreamToString(Serialize(yen));

            yen.Should().Be(Clone<Currency>(yen));
        }

        [Fact]
        public void WhenSerializingEuro_ThenThisShouldSucceed()
        {
            //Console.WriteLine(StreamToString(Serialize(euro)));
            StreamToString(Serialize(euro));

            euro.Should().Be(Clone<Currency>(euro));
        }

        public static Stream Serialize(object source)
        {
            Stream stream = new MemoryStream();
            XmlSerializer xmlSerializer = new XmlSerializer(source.GetType());
            xmlSerializer.Serialize(stream, source);
            return stream;
        }

        public static T Deserialize<T>(Stream stream)
        {
            stream.Position = 0L;
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            return (T)xmlSerializer.Deserialize(stream);
        }

        private static T Clone<T>(object source)
        {
            return Deserialize<T>(Serialize(source));
        }

        public static string StreamToString(Stream stream)
        {
            stream.Position = 0;
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }

    public class GiveIWantToUseALotOfCurrencies
    {
        [Fact(Skip = "Performance Test")]
        public void WhenCreatingOneMillion_ThenItShouldBeWithinFourSeconds()
        {
            var sw = Stopwatch.StartNew();
            var c = Currency.FromCode("EUR");
            Console.WriteLine("{0} ms for first call.", sw.ElapsedMilliseconds);

            double max = 1000000;
            Action action = () =>
                {
                    sw.Restart();
                    for (int i = 0; i < max; i++)
                    {
                        if (i % 3 == 0)
                            c = Currency.FromCode("EUR");
                        else if (i % 2 == 0)
                            c = Currency.FromCode("USD");
                        else
                            c = Currency.FromCode("JPY");
                    }
                    sw.Stop();
                };

            action.ExecutionTime().ShouldNotExceed(4.Seconds());
            Console.WriteLine(
                "{0} ms for creating {1:N0} currencies (avg {2:F5} ms).",
                sw.ElapsedMilliseconds,
                max,
                sw.ElapsedMilliseconds / (max));
        }
    }

    public class GivenIWantToDeconstructCurrency
    {
        [Fact]
        public void WhenDeconstructing_ThenShouldSucceed()
        {
            var currency = Currency.FromCode("EUR");

            var (code, number, symbol) = currency;

            code.Should().Be("EUR");
            number.Should().Be("978");
            symbol.Should().Be("€");
        }
    }
}