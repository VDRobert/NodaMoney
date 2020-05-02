using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NodaMoney
{
    /// <summary>Represent the central thread-safe registry for currencies.</summary>
    internal static class CurrencyRegistry
    {
        /// <summary>
        /// Mauritania does not use a decimal division of units, setting 1 ouguiya (UM) equal to 5 khoums, and Madagascar has 1 ariary =
        /// 5 iraimbilanja. The coins display "1/5" on their face and are referred to as a "fifth". These are not used in practice, but when
        /// written out, a single significant digit is used. E.g. 1.2 UM.
        /// </summary>
        // internal const double Z07 = 0.69897000433601880478626110527551; // Math.Log10(5);
        internal const byte Z07Byte = 105; // Math.Log10(5);

        /// <summary>Used for indication that the number of decimal digits doesn't matter, for example for gold or silver.</summary>
        internal const double NotApplicable = -1;
        internal const byte NotApplicableByte = 255;

        /// <summary>Shortcut for namespace indexes.</summary>
        /// private const int Iso4217 = 0;
        private const int Iso4217Historic = 1;

        private static readonly Dictionary<int, int> Index;
        private static CurrencyInfo[] currencies;
        private static string[] namespaces = { "ISO-4217", "ISO-4217-HISTORIC" };

        private static object changeLock = new object();

        //private static Currency[][] CurrenciesJagged = new Currency[2][];
        //private static readonly Dictionary<int, short> IsoKeyLookup;

        static CurrencyRegistry()
        {
            currencies = InitializeIsoCurrenciesArray();

            // TODO: Parallel foreach? ReadOnlySpan<T>
            Index = new Dictionary<int, int>(currencies.Length);
            int i = 0;
            foreach (var c in currencies)
            {
                Index[c.GetHashCode()] = i++;
            }

            // var xa = Currencies.AsMemory();
            // TODO: Use ReadOnlySpan<T> or ReadOnlyMemory<T>  to split up namespaces? 0..999 ISO4127, 1000..9999 ISO4127-HISTORIC
            // To much useless gaps, but for first 0..999 performance boost, because of no key lookup?

            // Using JaggedArray
            //IsoKeyLookup = new Dictionary<int, short>(1000);
            //CurrenciesJagged[0] = new Currency[1000];
            //foreach (var keyValuePair in x)
            //{
            //    if (keyValuePair.Value.Namespace == "ISO-4217")
            //    {
            //        IsoKeyLookup[keyValuePair.Value.Code.GetHashCode()] = keyValuePair.Value.Number;
            //        CurrenciesJagged[0][keyValuePair.Value.Number] = keyValuePair.Value;
            //    }
            //}
        }

        /// <summary>Tries the get <see cref="CurrencyInfo"/> of the given code and namespace.</summary>
        /// <param name="code">A currency code, like EUR or USD.</param>
        /// <param name="currency">When this method returns, contains the <see cref="CurrencyInfo"/> that has the specified code, or the default value of the type if the operation failed.</param>
        /// <returns><b>true</b> if <see cref="CurrencyRegistry"/> contains a <see cref="CurrencyInfo"/> with the specified code; otherwise, <b>false</b>.</returns>
        /// <exception cref="System.ArgumentNullException">The value of 'code' cannot be null or empty.</exception>
        public static ref CurrencyInfo Get(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentNullException(nameof(code));
            
            // Using JaggedArray
            //if (IsoKeyLookup.TryGetValue(code.GetHashCode(), out short number))
            //{
            //    return ref CurrenciesJagged[0][number];
            //}

            for (int i = 0; i < namespaces.Length; i++)
            {
                int hash = CurrencyInfo.GetHashCode(code, (byte)i);
                if (Index.TryGetValue(hash, out int index))
                {
                    return ref currencies[index]; // TODO: If more than one, sort by prio.
                }
            }

            throw new InvalidCurrencyException($"{code} is unknown currency code!");
        }

        /// <summary>Tries the get <see cref="CurrencyInfo"/> of the given code and namespace.</summary>
        /// <param name="code">A currency code, like EUR or USD.</param>
        /// <param name="namespace">A namespace, like ISO-4217.</param>
        /// <param name="currency">When this method returns, contains the <see cref="CurrencyInfo"/> that has the specified code and namespace, or the default value of the type if the operation failed.</param>
        /// <returns><b>true</b> if <see cref="CurrencyRegistry"/> contains a <see cref="CurrencyInfo"/> with the specified code; otherwise, <b>false</b>.</returns>
        /// <exception cref="System.ArgumentNullException">The value of 'code' or 'namespace' cannot be null or empty.</exception>
        public static ref CurrencyInfo Get(string code, string @namespace)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(@namespace))
                throw new ArgumentNullException(nameof(@namespace));

            // Using JaggedArray
            //if (@namespace == "ISO-4217")
            //{
            //    if (!IsoKeyLookup.TryGetValue(code.GetHashCode(), out short number))
            //        throw new InvalidCurrencyException($"{code} is an unknown {@namespace} currency code!");

            //    return ref CurrenciesJagged[0][number];
            //}

            //return ref CurrenciesJagged[0][999];

            if (!Index.TryGetValue(CurrencyInfo.GetHashCode(code, GetNamespaceIndex(@namespace)), out int index))
            {
                throw new InvalidCurrencyException($"{code} is an unknown {@namespace} currency code!");
            }

            return ref currencies[index];
        }

        /// <summary>Attempts to add the <see cref="CurrencyInfo"/> of the given code and namespace.</summary>
        /// <param name="code">A currency code, like EUR or USD.</param>
        /// <param name="namespace">A namespace, like ISO-4217.</param>
        /// <param name="currency">When this method returns, contains the <see cref="CurrencyInfo"/> that has the specified code and namespace, or the default value of the type if the operation failed.</param>
        /// <returns><b>true</b> if the <see cref="CurrencyInfo"/> with the specified code is added; otherwise, <b>false</b>.</returns>
        /// <exception cref="System.ArgumentNullException">The value of 'code' or 'namespace' cannot be null or empty.</exception>
        public static bool TryAdd(string code, string @namespace, CurrencyInfo currency)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(@namespace))
                throw new ArgumentNullException(nameof(@namespace));

            int nsIndex = GetOrAddNamespaceIndex(@namespace);

            lock (changeLock)
            {
                int key = CurrencyInfo.GetHashCode(code, nsIndex);
                if (Index.ContainsKey(key))
                {
                    return false;
                }

                Debug.Assert(!currencies.Contains(currency), $"{nameof(Index)} and {nameof(currencies)} array should be equally mapped so it exist in both or it doesn't exist in both!");

                // TryGetValue?
                if (currencies.Length > short.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(@namespace), $"Can't add currency {code}! Maximum allowed currencies of {currencies.Length} is exceeded.");
                }

                Array.Resize(ref currencies, currencies.Length + 1);

                int index = currencies.Length - 1;
                currencies[index] = currency;
                Index.Add(key, index);

                return true;
            }
        }

        /// <summary>Attempts to remove the <see cref="CurrencyInfo"/> of the given code and namespace.</summary>
        /// <param name="code">A currency code, like EUR or USD.</param>
        /// <param name="namespace">A namespace, like ISO-4217.</param>
        /// <param name="currency">When this method returns, contains the <see cref="CurrencyInfo"/> that has the specified code and namespace, or the default value of the type if the operation failed.</param>
        /// <returns><b>true</b> if the <see cref="CurrencyInfo"/> with the specified code is removed; otherwise, <b>false</b>.</returns>
        /// <exception cref="System.ArgumentNullException">The value of 'code' or 'namespace' cannot be null or empty.</exception>
        public static bool TryRemove(string code, string @namespace, out CurrencyInfo currency)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(@namespace))
                throw new ArgumentNullException(nameof(@namespace));

            int nsIndex = GetOrAddNamespaceIndex(@namespace);

            lock (changeLock)
            {
                int key = CurrencyInfo.GetHashCode(code, nsIndex);
                if (Index.ContainsKey(key))
                {
                    int index = Index[key];
                    if (Index.Remove(key))
                    {
                        // We leave currency in the array
                        currency = currencies[index];

                        return true;
                    }
                }
            }

            //Debug.Assert(Currencies.Contains(currency), $"{nameof(KeyLookup)} and {nameof(Currencies)} array should be equally mapped so it exist in both or it doesn't exist in both!");
            currency = default;
            return false;
        }

        /// <summary>Get all registered currencies.</summary>
        /// <returns>An <see cref="IEnumerable{Currency}"/> of all registered currencies.</returns>
        public static IEnumerable<CurrencyInfo> GetAllCurrencies()
        {
            //return Currencies.Values.AsEnumerable();
            return currencies.AsEnumerable();
        }

        internal static string GetNamespace(in int index)
        {
            return namespaces[index];
        }

        internal static int GetNamespaceIndex(in string @namespace)
        {
            for (var i = 0; i < namespaces.Length; i++)
            {
                if (namespaces[i] == @namespace)
                    return i;
            }

            throw new ArgumentOutOfRangeException(nameof(@namespace), $"Namespace {@namespace} is not found!");
        }

        internal static int GetOrAddNamespaceIndex(in string @namespace)
        {
            // TODO: Can be optimized (max 256 entries)
            for (var i = 0; i < namespaces.Length; i++)
            {
                if (namespaces[i] == @namespace)
                    return (byte)i;
            }

            lock (changeLock)
            {
                // TODO: Namespaces.Contains(@namespace)
                if (namespaces.Length > byte.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(@namespace), $"Can't add namespace {@namespace}! Maximum allowed namespaces of {namespaces.Length} is exceeded.");
                }

                Array.Resize(ref namespaces, namespaces.Length + 1);
                namespaces[namespaces.Length - 1] = @namespace;

                return namespaces.Length - 1;
            }
        }

        private static IDictionary<string, CurrencyInfo> InitializeIsoCurrencies()
        {
            // TODO: Move to resource file.
            return new Dictionary<string, CurrencyInfo>
            {
                // ISO-4217 currencies (list one)
                ["ISO-4217::AED"] = new CurrencyInfo("AED", 784, 2, "United Arab Emirates dirham", "د.إ"),
                ["ISO-4217::AFN"] = new CurrencyInfo("AFN", 971, 2, "Afghan afghani", "؋"),
                ["ISO-4217::ALL"] = new CurrencyInfo("ALL", 008, 2, "Albanian lek", "L"),
                ["ISO-4217::AMD"] = new CurrencyInfo("AMD", 051, 2, "Armenian dram", "֏"),
                ["ISO-4217::ANG"] = new CurrencyInfo("ANG", 532, 2, "Netherlands Antillean guilder", "ƒ"),
                ["ISO-4217::AOA"] = new CurrencyInfo("AOA", 973, 2, "Angolan kwanza", "Kz"),
                ["ISO-4217::ARS"] = new CurrencyInfo("ARS", 032, 2, "Argentine peso", "$"),
                ["ISO-4217::AUD"] = new CurrencyInfo("AUD", 036, 2, "Australian dollar", "$"),
                ["ISO-4217::AWG"] = new CurrencyInfo("AWG", 533, 2, "Aruban florin", "ƒ"),
                ["ISO-4217::AZN"] = new CurrencyInfo("AZN", 944, 2, "Azerbaijan Manat", "ман"), // AZERBAIJAN
                ["ISO-4217::BAM"] = new CurrencyInfo("BAM", 977, 2, "Bosnia and Herzegovina convertible mark", "KM"),
                ["ISO-4217::BBD"] = new CurrencyInfo("BBD", 052, 2, "Barbados dollar", "$"),
                ["ISO-4217::BDT"] = new CurrencyInfo("BDT", 050, 2, "Bangladeshi taka", "৳"), // or Tk
                ["ISO-4217::BGN"] = new CurrencyInfo("BGN", 975, 2, "Bulgarian lev", "лв."),
                ["ISO-4217::BHD"] = new CurrencyInfo("BHD", 048, 3, "Bahraini dinar", "BD"), // or د.ب. (switched for unit tests to work)
                ["ISO-4217::BIF"] = new CurrencyInfo("BIF", 108, 0, "Burundian franc", "FBu"),
                ["ISO-4217::BMD"] = new CurrencyInfo("BMD", 060, 2, "Bermudian dollar", "$"),
                ["ISO-4217::BND"] = new CurrencyInfo("BND", 096, 2, "Brunei dollar", "$"), // or B$
                ["ISO-4217::BOB"] = new CurrencyInfo("BOB", 068, 2, "Boliviano", "Bs."), // or BS or $b
                ["ISO-4217::BOV"] = new CurrencyInfo("BOV", 984, 2, "Bolivian Mvdol (funds code)", CurrencyInfo.GenericCurrencySign), // <==== not found
                ["ISO-4217::BRL"] = new CurrencyInfo("BRL", 986, 2, "Brazilian real", "R$"),
                ["ISO-4217::BSD"] = new CurrencyInfo("BSD", 044, 2, "Bahamian dollar", "$"),
                ["ISO-4217::BTN"] = new CurrencyInfo("BTN", 064, 2, "Bhutanese ngultrum", "Nu."),
                ["ISO-4217::BWP"] = new CurrencyInfo("BWP", 072, 2, "Botswana pula", "P"),
                ["ISO-4217::BYN"] = new CurrencyInfo("BYN", 933, 2, "Belarusian ruble", "Br", validFrom: new DateTime(2006, 06, 01)),
                ["ISO-4217::BZD"] = new CurrencyInfo("BZD", 084, 2, "Belize dollar", "BZ$"),
                ["ISO-4217::CAD"] = new CurrencyInfo("CAD", 124, 2, "Canadian dollar", "$"),
                ["ISO-4217::CDF"] = new CurrencyInfo("CDF", 976, 2, "Congolese franc", "FC"),
                ["ISO-4217::CHE"] = new CurrencyInfo("CHE", 947, 2, "WIR Euro (complementary currency)", "CHE"),
                ["ISO-4217::CHF"] = new CurrencyInfo("CHF", 756, 2, "Swiss franc", "fr."), // or CHF
                ["ISO-4217::CHW"] = new CurrencyInfo("CHW", 948, 2, "WIR Franc (complementary currency)", "CHW"),
                ["ISO-4217::CLF"] = new CurrencyInfo("CLF", 990, 4, "Unidad de Fomento (funds code)", "CLF"),
                ["ISO-4217::CLP"] = new CurrencyInfo("CLP", 152, 0, "Chilean peso", "$"),
                ["ISO-4217::CNY"] = new CurrencyInfo("CNY", 156, 2, "Chinese yuan", "¥"),
                ["ISO-4217::COP"] = new CurrencyInfo("COP", 170, 2, "Colombian peso", "$"),
                ["ISO-4217::COU"] = new CurrencyInfo("COU", 970, 2, "Unidad de Valor Real", CurrencyInfo.GenericCurrencySign), // ???
                ["ISO-4217::CRC"] = new CurrencyInfo("CRC", 188, 2, "Costa Rican colon", "₡"),
                ["ISO-4217::CUC"] = new CurrencyInfo("CUC", 931, 2, "Cuban convertible peso", "CUC$"), // $ or CUC
                ["ISO-4217::CUP"] = new CurrencyInfo("CUP", 192, 2, "Cuban peso", "$"), // or ₱ (obsolete?)
                ["ISO-4217::CVE"] = new CurrencyInfo("CVE", 132, 2, "Cape Verde escudo", "$"),
                ["ISO-4217::CZK"] = new CurrencyInfo("CZK", 203, 2, "Czech koruna", "Kč"),
                ["ISO-4217::DJF"] = new CurrencyInfo("DJF", 262, 0, "Djiboutian franc", "Fdj"),
                ["ISO-4217::DKK"] = new CurrencyInfo("DKK", 208, 2, "Danish krone", "kr."),
                ["ISO-4217::DOP"] = new CurrencyInfo("DOP", 214, 2, "Dominican peso", "RD$"), // or $
                ["ISO-4217::DZD"] = new CurrencyInfo("DZD", 012, 2, "Algerian dinar", "DA"), // (Latin) or د.ج (Arabic)
                ["ISO-4217::EGP"] = new CurrencyInfo("EGP", 818, 2, "Egyptian pound", "LE"), // or E£ or ج.م (Arabic)
                ["ISO-4217::ERN"] = new CurrencyInfo("ERN", 232, 2, "Eritrean nakfa", "ERN"),
                ["ISO-4217::ETB"] = new CurrencyInfo("ETB", 230, 2, "Ethiopian birr", "Br"), // (Latin) or ብር (Ethiopic)
                ["ISO-4217::EUR"] = new CurrencyInfo("EUR", 978, 2, "Euro", "€"),
                ["ISO-4217::FJD"] = new CurrencyInfo("FJD", 242, 2, "Fiji dollar", "$"), // or FJ$
                ["ISO-4217::FKP"] = new CurrencyInfo("FKP", 238, 2, "Falkland Islands pound", "£"),
                ["ISO-4217::GBP"] = new CurrencyInfo("GBP", 826, 2, "Pound sterling", "£"),
                ["ISO-4217::GEL"] = new CurrencyInfo("GEL", 981, 2, "Georgian lari", "ლ."), // TODO: new symbol since July 18, 2014 => see http://en.wikipedia.org/wiki/Georgian_lari
                ["ISO-4217::GHS"] = new CurrencyInfo("GHS", 936, 2, "Ghanaian cedi", "GH¢"), // or GH₵
                ["ISO-4217::GIP"] = new CurrencyInfo("GIP", 292, 2, "Gibraltar pound", "£"),
                ["ISO-4217::GMD"] = new CurrencyInfo("GMD", 270, 2, "Gambian dalasi", "D"),
                ["ISO-4217::GNF"] = new CurrencyInfo("GNF", 324, 0, "Guinean Franc", "FG"), // (possibly also Fr or GFr)  GUINEA
                ["ISO-4217::GTQ"] = new CurrencyInfo("GTQ", 320, 2, "Guatemalan quetzal", "Q"),
                ["ISO-4217::GYD"] = new CurrencyInfo("GYD", 328, 2, "Guyanese dollar", "$"), // or G$
                ["ISO-4217::HKD"] = new CurrencyInfo("HKD", 344, 2, "Hong Kong dollar", "HK$"), // or $
                ["ISO-4217::HNL"] = new CurrencyInfo("HNL", 340, 2, "Honduran lempira", "L"),
                ["ISO-4217::HRK"] = new CurrencyInfo("HRK", 191, 2, "Croatian kuna", "kn"),
                ["ISO-4217::HTG"] = new CurrencyInfo("HTG", 332, 2, "Haitian gourde", "G"),
                ["ISO-4217::HUF"] = new CurrencyInfo("HUF", 348, 2, "Hungarian forint", "Ft"),
                ["ISO-4217::IDR"] = new CurrencyInfo("IDR", 360, 2, "Indonesian rupiah", "Rp"),
                ["ISO-4217::ILS"] = new CurrencyInfo("ILS", 376, 2, "Israeli new shekel", "₪"),
                ["ISO-4217::INR"] = new CurrencyInfo("INR", 356, 2, "Indian rupee", "₹"),
                ["ISO-4217::IQD"] = new CurrencyInfo("IQD", 368, 3, "Iraqi dinar", "د.ع"),
                ["ISO-4217::IRR"] = new CurrencyInfo("IRR", 364, 2, "Iranian rial", "ريال"),
                ["ISO-4217::ISK"] = new CurrencyInfo("ISK", 352, 0, "Icelandic króna", "kr"),
                ["ISO-4217::JMD"] = new CurrencyInfo("JMD", 388, 2, "Jamaican dollar", "J$"), // or $
                ["ISO-4217::JOD"] = new CurrencyInfo("JOD", 400, 3, "Jordanian dinar", "د.ا.‏"),
                ["ISO-4217::JPY"] = new CurrencyInfo("JPY", 392, 0, "Japanese yen", "¥"),
                ["ISO-4217::KES"] = new CurrencyInfo("KES", 404, 2, "Kenyan shilling", "KSh"),
                ["ISO-4217::KGS"] = new CurrencyInfo("KGS", 417, 2, "Kyrgyzstani som", "сом"),
                ["ISO-4217::KHR"] = new CurrencyInfo("KHR", 116, 2, "Cambodian riel", "៛"),
                ["ISO-4217::KMF"] = new CurrencyInfo("KMF", 174, 0, "Comorian Franc", "CF"), // COMOROS (THE)
                ["ISO-4217::KPW"] = new CurrencyInfo("KPW", 408, 2, "North Korean won", "₩"),
                ["ISO-4217::KRW"] = new CurrencyInfo("KRW", 410, 0, "South Korean won", "₩"),
                ["ISO-4217::KWD"] = new CurrencyInfo("KWD", 414, 3, "Kuwaiti dinar", "د.ك"), // or K.D.
                ["ISO-4217::KYD"] = new CurrencyInfo("KYD", 136, 2, "Cayman Islands dollar", "$"),
                ["ISO-4217::KZT"] = new CurrencyInfo("KZT", 398, 2, "Kazakhstani tenge", "₸"),
                ["ISO-4217::LAK"] = new CurrencyInfo("LAK", 418, 2, "Lao Kip", "₭"), // or ₭N,  LAO PEOPLE’S DEMOCRATIC REPUBLIC(THE), ISO says minor unit=2 but wiki says Historically, one kip was divided into 100 att (ອັດ).
                ["ISO-4217::LBP"] = new CurrencyInfo("LBP", 422, 2, "Lebanese pound", "ل.ل"),
                ["ISO-4217::LKR"] = new CurrencyInfo("LKR", 144, 2, "Sri Lankan rupee", "Rs"), // or රු
                ["ISO-4217::LRD"] = new CurrencyInfo("LRD", 430, 2, "Liberian dollar", "$"), // or L$, LD$
                ["ISO-4217::LSL"] = new CurrencyInfo("LSL", 426, 2, "Lesotho loti", "L"), // L or M (pl.)
                ["ISO-4217::LYD"] = new CurrencyInfo("LYD", 434, 3, "Libyan dinar", "ل.د"), // or LD
                ["ISO-4217::MAD"] = new CurrencyInfo("MAD", 504, 2, "Moroccan dirham", "د.م."),
                ["ISO-4217::MDL"] = new CurrencyInfo("MDL", 498, 2, "Moldovan leu", "L"),
                ["ISO-4217::MGA"] = new CurrencyInfo("MGA", 969, Z07Byte, "Malagasy ariary", "Ar"),  // divided into five subunits rather than by a power of ten. 5 is 10 to the power of 0.69897...
                ["ISO-4217::MKD"] = new CurrencyInfo("MKD", 807, 2, "Macedonian denar", "ден"),
                ["ISO-4217::MMK"] = new CurrencyInfo("MMK", 104, 2, "Myanma kyat", "K"),
                ["ISO-4217::MNT"] = new CurrencyInfo("MNT", 496, 2, "Mongolian tugrik", "₮"),
                ["ISO-4217::MOP"] = new CurrencyInfo("MOP", 446, 2, "Macanese pataca", "MOP$"),
                ["ISO-4217::MRU"] = new CurrencyInfo("MRU", 929, Z07Byte, "Mauritanian ouguiya", "UM", validFrom: new DateTime(2018, 01, 01)), // divided into five subunits rather than by a power of ten. 5 is 10 to the power of 0.69897...
                ["ISO-4217::MUR"] = new CurrencyInfo("MUR", 480, 2, "Mauritian rupee", "Rs"),
                ["ISO-4217::MVR"] = new CurrencyInfo("MVR", 462, 2, "Maldivian rufiyaa", "Rf"), // or , MRf, MVR, .ރ or /-
                ["ISO-4217::MWK"] = new CurrencyInfo("MWK", 454, 2, "Malawi kwacha", "MK"),
                ["ISO-4217::MXN"] = new CurrencyInfo("MXN", 484, 2, "Mexican peso", "$"),
                ["ISO-4217::MXV"] = new CurrencyInfo("MXV", 979, 2, "Mexican Unidad de Inversion (UDI) (funds code)", CurrencyInfo.GenericCurrencySign),  // <==== not found
                ["ISO-4217::MYR"] = new CurrencyInfo("MYR", 458, 2, "Malaysian ringgit", "RM"),
                ["ISO-4217::MZN"] = new CurrencyInfo("MZN", 943, 2, "Mozambican metical", "MTn"), // or MTN
                ["ISO-4217::NAD"] = new CurrencyInfo("NAD", 516, 2, "Namibian dollar", "N$"), // or $
                ["ISO-4217::NGN"] = new CurrencyInfo("NGN", 566, 2, "Nigerian naira", "₦"),
                ["ISO-4217::NIO"] = new CurrencyInfo("NIO", 558, 2, "Nicaraguan córdoba", "C$"),
                ["ISO-4217::NOK"] = new CurrencyInfo("NOK", 578, 2, "Norwegian krone", "kr"),
                ["ISO-4217::NPR"] = new CurrencyInfo("NPR", 524, 2, "Nepalese rupee", "Rs"), // or ₨ or रू
                ["ISO-4217::NZD"] = new CurrencyInfo("NZD", 554, 2, "New Zealand dollar", "$"),
                ["ISO-4217::OMR"] = new CurrencyInfo("OMR", 512, 3, "Omani rial", "ر.ع."),
                ["ISO-4217::PAB"] = new CurrencyInfo("PAB", 590, 2, "Panamanian balboa", "B/."),
                ["ISO-4217::PEN"] = new CurrencyInfo("PEN", 604, 2, "Peruvian sol", "S/."),
                ["ISO-4217::PGK"] = new CurrencyInfo("PGK", 598, 2, "Papua New Guinean kina", "K"),
                ["ISO-4217::PHP"] = new CurrencyInfo("PHP", 608, 2, "Philippine Peso", "₱"), // or P or PHP or PhP
                ["ISO-4217::PKR"] = new CurrencyInfo("PKR", 586, 2, "Pakistani rupee", "Rs"),
                ["ISO-4217::PLN"] = new CurrencyInfo("PLN", 985, 2, "Polish złoty", "zł"),
                ["ISO-4217::PYG"] = new CurrencyInfo("PYG", 600, 0, "Paraguayan guaraní", "₲"),
                ["ISO-4217::QAR"] = new CurrencyInfo("QAR", 634, 2, "Qatari riyal", "ر.ق"), // or QR
                ["ISO-4217::RON"] = new CurrencyInfo("RON", 946, 2, "Romanian new leu", "lei"),
                ["ISO-4217::RSD"] = new CurrencyInfo("RSD", 941, 2, "Serbian dinar", "РСД"), // or RSD (or дин or d./д)
                ["ISO-4217::RUB"] = new CurrencyInfo("RUB", 643, 2, "Russian rouble", "₽"), // or R or руб (both onofficial)
                ["ISO-4217::RWF"] = new CurrencyInfo("RWF", 646, 0, "Rwandan franc", "RFw"), // or RF, R₣
                ["ISO-4217::SAR"] = new CurrencyInfo("SAR", 682, 2, "Saudi riyal", "ر.س"), // or SR (Latin) or ﷼‎ (Unicode)
                ["ISO-4217::SBD"] = new CurrencyInfo("SBD", 090, 2, "Solomon Islands dollar", "SI$"),
                ["ISO-4217::SCR"] = new CurrencyInfo("SCR", 690, 2, "Seychelles rupee", "SR"), // or SRe
                ["ISO-4217::SDG"] = new CurrencyInfo("SDG", 938, 2, "Sudanese pound", "ج.س."),
                ["ISO-4217::SEK"] = new CurrencyInfo("SEK", 752, 2, "Swedish krona/kronor", "kr"),
                ["ISO-4217::SGD"] = new CurrencyInfo("SGD", 702, 2, "Singapore dollar", "S$"), // or $
                ["ISO-4217::SHP"] = new CurrencyInfo("SHP", 654, 2, "Saint Helena pound", "£"),
                ["ISO-4217::SLL"] = new CurrencyInfo("SLL", 694, 2, "Sierra Leonean leone", "Le"),
                ["ISO-4217::SOS"] = new CurrencyInfo("SOS", 706, 2, "Somali shilling", "S"), // or Sh.So.
                ["ISO-4217::SRD"] = new CurrencyInfo("SRD", 968, 2, "Surinamese dollar", "$"),
                ["ISO-4217::SSP"] = new CurrencyInfo("SSP", 728, 2, "South Sudanese pound", "£"), // not sure about symbol...
                ["ISO-4217::SVC"] = new CurrencyInfo("SVC", 222, 2, "El Salvador Colon", "₡"),
                ["ISO-4217::SYP"] = new CurrencyInfo("SYP", 760, 2, "Syrian pound", "ܠ.ܣ.‏"), // or LS or £S (or £)
                ["ISO-4217::SZL"] = new CurrencyInfo("SZL", 748, 2, "Swazi lilangeni", "L"), // or E (plural)
                ["ISO-4217::THB"] = new CurrencyInfo("THB", 764, 2, "Thai baht", "฿"),
                ["ISO-4217::TJS"] = new CurrencyInfo("TJS", 972, 2, "Tajikistani somoni", "смн"),
                ["ISO-4217::TMT"] = new CurrencyInfo("TMT", 934, 2, "Turkmenistani manat", "m"), // or T?
                ["ISO-4217::TND"] = new CurrencyInfo("TND", 788, 3, "Tunisian dinar", "د.ت"), // or DT (Latin)
                ["ISO-4217::TOP"] = new CurrencyInfo("TOP", 776, 2, "Tongan paʻanga", "T$"), // (sometimes PT)
                ["ISO-4217::TRY"] = new CurrencyInfo("TRY", 949, 2, "Turkish lira", "₺"),
                ["ISO-4217::TTD"] = new CurrencyInfo("TTD", 780, 2, "Trinidad and Tobago dollar", "$"), // or TT$
                ["ISO-4217::TWD"] = new CurrencyInfo("TWD", 901, 2, "New Taiwan dollar", "NT$"), // or $
                ["ISO-4217::TZS"] = new CurrencyInfo("TZS", 834, 2, "Tanzanian shilling", "x/y"), // or TSh
                ["ISO-4217::UAH"] = new CurrencyInfo("UAH", 980, 2, "Ukrainian hryvnia", "₴"),
                ["ISO-4217::UGX"] = new CurrencyInfo("UGX", 800, 0, "Ugandan shilling", "USh"),
                ["ISO-4217::USD"] = new CurrencyInfo("USD", 840, 2, "United States dollar", "$"), // or US$
                ["ISO-4217::USN"] = new CurrencyInfo("USN", 997, 2, "United States dollar (next day) (funds code)", "$"),
                ["ISO-4217::UYI"] = new CurrencyInfo("UYI", 940, 0, "Uruguay Peso en Unidades Indexadas (UI) (funds code)", CurrencyInfo.GenericCurrencySign), // List two
                ["ISO-4217::UYU"] = new CurrencyInfo("UYU", 858, 2, "Uruguayan peso", "$"), // or $U
                ["ISO-4217::UZS"] = new CurrencyInfo("UZS", 860, 2, "Uzbekistan som", "лв"), // or сўм ?
                ["ISO-4217::VES"] = new CurrencyInfo("VES", 928, 2, "Venezuelan Bolívar Soberano", "Bs.", validFrom: new DateTime(2018, 8, 20)), // or Bs.F. , Amendment 167 talks about delay but from multiple sources on the web the date seems to be 20 aug.
                ["ISO-4217::VND"] = new CurrencyInfo("VND", 704, 0, "Vietnamese dong", "₫"),
                ["ISO-4217::VUV"] = new CurrencyInfo("VUV", 548, 0, "Vanuatu vatu", "VT"),
                ["ISO-4217::WST"] = new CurrencyInfo("WST", 882, 2, "Samoan tala", "WS$"), // sometimes SAT, ST or T
                ["ISO-4217::XAF"] = new CurrencyInfo("XAF", 950, 0, "CFA franc BEAC", "FCFA"),
                ["ISO-4217::XAG"] = new CurrencyInfo("XAG", 961, NotApplicableByte, "Silver (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XAU"] = new CurrencyInfo("XAU", 959, NotApplicableByte, "Gold (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XBA"] = new CurrencyInfo("XBA", 955, NotApplicableByte, "European Composite Unit (EURCO) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XBB"] = new CurrencyInfo("XBB", 956, NotApplicableByte, "European Monetary Unit (E.M.U.-6) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XBC"] = new CurrencyInfo("XBC", 957, NotApplicableByte, "European Unit of Account 9 (E.U.A.-9) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XBD"] = new CurrencyInfo("XBD", 958, NotApplicableByte, "European Unit of Account 17 (E.U.A.-17) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XCD"] = new CurrencyInfo("XCD", 951, 2, "East Caribbean dollar", "$"), // or EC$
                ["ISO-4217::XDR"] = new CurrencyInfo("XDR", 960, NotApplicableByte, "Special drawing rights", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XOF"] = new CurrencyInfo("XOF", 952, 0, "CFA franc BCEAO", "CFA"),
                ["ISO-4217::XPD"] = new CurrencyInfo("XPD", 964, NotApplicableByte, "Palladium (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XPF"] = new CurrencyInfo("XPF", 953, 0, "CFP franc", "F"),
                ["ISO-4217::XPT"] = new CurrencyInfo("XPT", 962, NotApplicableByte, "Platinum (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XSU"] = new CurrencyInfo("XSU", 994, NotApplicableByte, "SUCRE", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XTS"] = new CurrencyInfo("XTS", 963, NotApplicableByte, "Code reserved for testing purposes", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XUA"] = new CurrencyInfo("XUA", 965, NotApplicableByte, "ADB Unit of Account", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::XXX"] = new CurrencyInfo("XXX", 999, NotApplicableByte, "No currency", CurrencyInfo.GenericCurrencySign),
                ["ISO-4217::YER"] = new CurrencyInfo("YER", 886, 2, "Yemeni rial", "﷼"), // or ر.ي.‏‏ ?
                ["ISO-4217::ZAR"] = new CurrencyInfo("ZAR", 710, 2, "South African rand", "R"),
                ["ISO-4217::ZMW"] = new CurrencyInfo("ZMW", 967, 2, "Zambian kwacha", "ZK"), // or ZMW
                ["ISO-4217::ZWL"] = new CurrencyInfo("ZWL", 932, 2, "Zimbabwean dollar", "$"),
                ["ISO-4217::STN"] = new CurrencyInfo("STN", 930, 2, "Dobra", "Db", validFrom: new DateTime(2018, 1, 1)), // New Currency of São Tomé and Príncipe from 1 Jan 2018 (Amendment 164)
                ["ISO-4217::STD"] = new CurrencyInfo("STD", 678, 2, "Dobra", "Db", validTo: new DateTime(2018, 1, 1)), // To be replaced Currency of São Tomé and Príncipe from 1 Jan 2018 (Amendment 164),  inflation has rendered the cêntimo obsolete
                ["ISO-4217::UYW"] = new CurrencyInfo("UYW", 927, 4, "Unidad Previsional", "Db", validFrom: new DateTime(2018, 8, 29)), // The Central Bank of Uruguay is applying for new Fund currency code (Amendment 169)

                // Historic ISO-4217 currencies (list three)
                ["ISO-4217-HISTORIC::BYR"] = new CurrencyInfo("BYR", 974, 0, "Belarusian ruble", "Br", Iso4217Historic, validTo: new DateTime(2016, 12, 31), validFrom: new DateTime(2000, 01, 01)),
                ["ISO-4217-HISTORIC::VEF"] = new CurrencyInfo("VEF", 937, 2, "Venezuelan bolívar", "Bs.", Iso4217Historic, new DateTime(2018, 8, 20)), // replaced by VEF, The conversion rate is 1000 (old) Bolívar to 1 (new) Bolívar Soberano (1000:1). The expiration date of the current bolívar will be defined later and communicated by the Central Bank of Venezuela in due time.
                ["ISO-4217-HISTORIC::MRO"] = new CurrencyInfo("MRO", 478, Z07Byte, "Mauritanian ouguiya", "UM", Iso4217Historic, new DateTime(2018, 1, 1)), // replaced by MRU
                ["ISO-4217-HISTORIC::ESA"] = new CurrencyInfo("ESA", 996, NotApplicableByte, "Spanish peseta (account A)", "Pta", Iso4217Historic, new DateTime(2002, 3, 1)), // replaced by ESP (EUR)
                ["ISO-4217-HISTORIC::ESB"] = new CurrencyInfo("ESB", 995, NotApplicableByte, "Spanish peseta (account B)", "Pta", Iso4217Historic, new DateTime(2002, 3, 1)), // replaced by ESP (EUR)
                ["ISO-4217-HISTORIC::LTL"] = new CurrencyInfo("LTL", 440, 2, "Lithuanian litas", "Lt", Iso4217Historic, new DateTime(2014, 12, 31), new DateTime(1993, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::USS"] = new CurrencyInfo("USS", 998, 2, "United States dollar (same day) (funds code)", "$", Iso4217Historic, new DateTime(2014, 3, 28)), // replaced by (no successor)
                ["ISO-4217-HISTORIC::LVL"] = new CurrencyInfo("LVL", 428, 2, "Latvian lats", "Ls", Iso4217Historic, new DateTime(2013, 12, 31), new DateTime(1992, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::XFU"] = new CurrencyInfo("XFU",   0, NotApplicableByte, "UIC franc (special settlement currency) International Union of Railways", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2013, 11, 7)), // replaced by EUR
                ["ISO-4217-HISTORIC::ZMK"] = new CurrencyInfo("ZMK", 894, 2, "Zambian kwacha", "ZK", Iso4217Historic, new DateTime(2013, 1, 1), new DateTime(1968, 1, 16)), // replaced by ZMW
                ["ISO-4217-HISTORIC::EEK"] = new CurrencyInfo("EEK", 233, 2, "Estonian kroon", "kr", Iso4217Historic, new DateTime(2010, 12, 31), new DateTime(1992, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::ZWR"] = new CurrencyInfo("ZWR", 935, 2, "Zimbabwean dollar A/09", "$", Iso4217Historic, new DateTime(2009, 2, 2), new DateTime(2008, 8, 1)), // replaced by ZWL
                ["ISO-4217-HISTORIC::SKK"] = new CurrencyInfo("SKK", 703, 2, "Slovak koruna", "Sk", Iso4217Historic, new DateTime(2008, 12, 31), new DateTime(1993, 2, 8)), // replaced by EUR
                ["ISO-4217-HISTORIC::TMM"] = new CurrencyInfo("TMM", 795, 0, "Turkmenistani manat", "T", Iso4217Historic, new DateTime(2008, 12, 31), new DateTime(1993, 11, 1)), // replaced by TMT
                ["ISO-4217-HISTORIC::ZWN"] = new CurrencyInfo("ZWN", 942, 2, "Zimbabwean dollar A/08", "$", Iso4217Historic, new DateTime(2008, 7, 31), new DateTime(2006, 8, 1)), // replaced by ZWR
                ["ISO-4217-HISTORIC::VEB"] = new CurrencyInfo("VEB", 862, 2, "Venezuelan bolívar", "Bs.", Iso4217Historic, new DateTime(2008, 1, 1)), // replaced by VEF
                ["ISO-4217-HISTORIC::CYP"] = new CurrencyInfo("CYP", 196, 2, "Cypriot pound", "£", Iso4217Historic, new DateTime(2007, 12, 31), new DateTime(1879, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::MTL"] = new CurrencyInfo("MTL", 470, 2, "Maltese lira", "₤", Iso4217Historic, new DateTime(2007, 12, 31), new DateTime(1972, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::GHC"] = new CurrencyInfo("GHC", 288, 0, "Ghanaian cedi", "GH₵", Iso4217Historic, new DateTime(2007, 7, 1), new DateTime(1967, 1, 1)), // replaced by GHS
                ["ISO-4217-HISTORIC::SDD"] = new CurrencyInfo("SDD", 736, NotApplicableByte, "Sudanese dinar", "£Sd", Iso4217Historic, new DateTime(2007, 1, 10), new DateTime(1992, 6, 8)), // replaced by SDG
                ["ISO-4217-HISTORIC::SIT"] = new CurrencyInfo("SIT", 705, 2, "Slovenian tolar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2006, 12, 31), new DateTime(1991, 10, 8)), // replaced by EUR
                ["ISO-4217-HISTORIC::ZWD"] = new CurrencyInfo("ZWD", 716, 2, "Zimbabwean dollar A/06", "$", Iso4217Historic, new DateTime(2006, 7, 31), new DateTime(1980, 4, 18)), // replaced by ZWN
                ["ISO-4217-HISTORIC::MZM"] = new CurrencyInfo("MZM", 508, 0, "Mozambican metical", "MT", Iso4217Historic, new DateTime(2006, 6, 30), new DateTime(1980, 1, 1)), // replaced by MZN
                ["ISO-4217-HISTORIC::AZM"] = new CurrencyInfo("AZM", 031, 0, "Azerbaijani manat", "₼", Iso4217Historic, new DateTime(2006, 1, 1), new DateTime(1992, 8, 15)), // replaced by AZN
                ["ISO-4217-HISTORIC::CSD"] = new CurrencyInfo("CSD", 891, 2, "Serbian dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2006, 12, 31), new DateTime(2003, 7, 3)), // replaced by RSD
                ["ISO-4217-HISTORIC::MGF"] = new CurrencyInfo("MGF", 450, 2, "Malagasy franc", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2005, 1, 1), new DateTime(1963, 7, 1)), // replaced by MGA
                ["ISO-4217-HISTORIC::ROL"] = new CurrencyInfo("ROL", 642, NotApplicableByte, "Romanian leu A/05", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2005, 12, 31), new DateTime(1952, 1, 28)), // replaced by RON
                ["ISO-4217-HISTORIC::TRL"] = new CurrencyInfo("TRL", 792, 0, "Turkish lira A/05", "₺", Iso4217Historic, new DateTime(2005, 12, 31)), // replaced by TRY
                ["ISO-4217-HISTORIC::SRG"] = new CurrencyInfo("SRG", 740, NotApplicableByte, "Suriname guilder", "ƒ", Iso4217Historic, new DateTime(2004, 12, 31)), // replaced by SRD
                ["ISO-4217-HISTORIC::YUM"] = new CurrencyInfo("YUM", 891, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(2003, 7, 2), new DateTime(1994, 1, 24)), // replaced by CSD
                ["ISO-4217-HISTORIC::AFA"] = new CurrencyInfo("AFA", 004, NotApplicableByte, "Afghan afghani", "؋", Iso4217Historic, new DateTime(2003, 12, 31), new DateTime(1925, 1, 1)), // replaced by AFN
                ["ISO-4217-HISTORIC::XFO"] = new CurrencyInfo("XFO",   0, NotApplicableByte, "Gold franc (special settlement currency)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2003, 12, 31), new DateTime(1803, 1, 1)), // replaced by XDR
                ["ISO-4217-HISTORIC::GRD"] = new CurrencyInfo("GRD", 300, 2, "Greek drachma", "₯", Iso4217Historic, new DateTime(2000, 12, 31), new DateTime(1954, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::TJR"] = new CurrencyInfo("TJR", 762, NotApplicableByte, "Tajikistani ruble", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2000, 10, 30), new DateTime(1995, 5, 10)), // replaced by TJS
                ["ISO-4217-HISTORIC::ECV"] = new CurrencyInfo("ECV", 983, NotApplicableByte, "Ecuador Unidad de Valor Constante (funds code)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2000, 1, 9), new DateTime(1993, 1, 1)), // replaced by (no successor)
                ["ISO-4217-HISTORIC::ECS"] = new CurrencyInfo("ECS", 218, 0, "Ecuadorian sucre", "S/.", Iso4217Historic, new DateTime(2000, 12, 31), new DateTime(1884, 1, 1)), // replaced by USD
                ["ISO-4217-HISTORIC::BYB"] = new CurrencyInfo("BYB", 112, 2, "Belarusian ruble", "Br", Iso4217Historic, new DateTime(1999, 12, 31), new DateTime(1992, 1, 1)), // replaced by BYR
                ["ISO-4217-HISTORIC::AOR"] = new CurrencyInfo("AOR", 982, 0, "Angolan kwanza readjustado", "Kz", Iso4217Historic, new DateTime(1999, 11, 30), new DateTime(1995, 7, 1)), // replaced by AOA
                ["ISO-4217-HISTORIC::BGL"] = new CurrencyInfo("BGL", 100, 2, "Bulgarian lev A/99", "лв.", Iso4217Historic, new DateTime(1999, 7, 5), new DateTime(1962, 1, 1)), // replaced by BGN
                ["ISO-4217-HISTORIC::ADF"] = new CurrencyInfo("ADF",   0, 2, "Andorran franc (1:1 peg to the French franc)", "Fr", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1960, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::ADP"] = new CurrencyInfo("ADP", 020, 0, "Andorran peseta (1:1 peg to the Spanish peseta)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1869, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::ATS"] = new CurrencyInfo("ATS", 040, 2, "Austrian schilling", "öS", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1945, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::BEF"] = new CurrencyInfo("BEF", 056, 2, "Belgian franc (currency union with LUF)", "fr.", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1832, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::DEM"] = new CurrencyInfo("DEM", 276, 2, "German mark", "DM", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1948, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::ESP"] = new CurrencyInfo("ESP", 724, 0, "Spanish peseta", "Pta", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1869, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::FIM"] = new CurrencyInfo("FIM", 246, 2, "Finnish markka", "mk", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1860, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::FRF"] = new CurrencyInfo("FRF", 250, 2, "French franc", "Fr", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1960, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::IEP"] = new CurrencyInfo("IEP", 372, 2, "Irish pound (punt in Irish language)", "£", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1938, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::ITL"] = new CurrencyInfo("ITL", 380, 0, "Italian lira", "₤", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1861, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::LUF"] = new CurrencyInfo("LUF", 442, 2, "Luxembourg franc (currency union with BEF)", "fr.", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1944, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::MCF"] = new CurrencyInfo("MCF",   0, 2, "Monegasque franc (currency union with FRF)", "fr.", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1960, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::NLG"] = new CurrencyInfo("NLG", 528, 2, "Dutch guilder", "ƒ", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1810, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::PTE"] = new CurrencyInfo("PTE", 620, 0, "Portuguese escudo", "$", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(4160, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::SML"] = new CurrencyInfo("SML",   0, 0, "San Marinese lira (currency union with ITL and VAL)", "₤", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1864, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::VAL"] = new CurrencyInfo("VAL",   0, 0, "Vatican lira (currency union with ITL and SML)", "₤", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1929, 1, 1)), // replaced by EUR
                ["ISO-4217-HISTORIC::XEU"] = new CurrencyInfo("XEU", 954, NotApplicableByte, "European Currency Unit (1 XEU = 1 EUR)", "ECU", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1979, 3, 13)), // replaced by EUR
                ["ISO-4217-HISTORIC::BAD"] = new CurrencyInfo("BAD",   0, 2, "Bosnia and Herzegovina dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1992, 7, 1)), // replaced by BAM
                ["ISO-4217-HISTORIC::RUR"] = new CurrencyInfo("RUR", 810, 2, "Russian ruble A/97", "₽", Iso4217Historic, new DateTime(1997, 12, 31), new DateTime(1992, 1, 1)), // replaced by RUB
                ["ISO-4217-HISTORIC::GWP"] = new CurrencyInfo("GWP", 624, NotApplicableByte, "Guinea-Bissau peso", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1997, 12, 31), new DateTime(1975, 1, 1)), // replaced by XOF
                ["ISO-4217-HISTORIC::ZRN"] = new CurrencyInfo("ZRN", 180, 2, "Zaïrean new zaïre", "Ƶ", Iso4217Historic, new DateTime(1997, 12, 31), new DateTime(1993, 1, 1)), // replaced by CDF
                ["ISO-4217-HISTORIC::UAK"] = new CurrencyInfo("UAK", 804, NotApplicableByte, "Ukrainian karbovanets", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1996, 9, 1), new DateTime(1992, 10, 1)), // replaced by UAH
                ["ISO-4217-HISTORIC::YDD"] = new CurrencyInfo("YDD", 720, NotApplicableByte, "South Yemeni dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1996, 6, 11)), // replaced by YER
                ["ISO-4217-HISTORIC::AON"] = new CurrencyInfo("AON", 024, 0, "Angolan new kwanza", "Kz", Iso4217Historic, new DateTime(1995, 6, 30), new DateTime(1990, 9, 25)), // replaced by AOR
                ["ISO-4217-HISTORIC::ZAL"] = new CurrencyInfo("ZAL", 991, NotApplicableByte, "South African financial rand (funds code)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1995, 3, 13), new DateTime(1985, 9, 1)), // replaced by (no successor)
                ["ISO-4217-HISTORIC::PLZ"] = new CurrencyInfo("PLZ", 616, NotApplicableByte, "Polish zloty A/94", "zł", Iso4217Historic, new DateTime(1994, 12, 31), new DateTime(1950, 10, 30)), // replaced by PLN
                ["ISO-4217-HISTORIC::BRR"] = new CurrencyInfo("BRR",   0, 2, "Brazilian cruzeiro real", "CR$", Iso4217Historic, new DateTime(1994, 6, 30), new DateTime(1993, 8, 1)), // replaced by BRL
                ["ISO-4217-HISTORIC::HRD"] = new CurrencyInfo("HRD",   0, NotApplicableByte, "Croatian dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1994, 5, 30), new DateTime(1991, 12, 23)), // replaced by HRK
                ["ISO-4217-HISTORIC::YUG"] = new CurrencyInfo("YUG",   0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1994, 1, 23), new DateTime(1994, 1, 1)), // replaced by YUM
                ["ISO-4217-HISTORIC::YUO"] = new CurrencyInfo("YUO",   0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1993, 12, 31), new DateTime(1993, 10, 1)), // replaced by YUG
                ["ISO-4217-HISTORIC::YUR"] = new CurrencyInfo("YUR",   0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1993, 9, 30), new DateTime(1992, 7, 1)), // replaced by YUO
                ["ISO-4217-HISTORIC::BRE"] = new CurrencyInfo("BRE",   0, 2, "Brazilian cruzeiro", "₢", Iso4217Historic, new DateTime(1993, 8, 1), new DateTime(1990, 3, 15)), // replaced by BRR
                ["ISO-4217-HISTORIC::UYN"] = new CurrencyInfo("UYN", 858, NotApplicableByte, "Uruguay Peso", "$U", Iso4217Historic, new DateTime(1993, 3, 1), new DateTime(1975, 7, 1)), // replaced by UYU
                ["ISO-4217-HISTORIC::CSK"] = new CurrencyInfo("CSK", 200, NotApplicableByte, "Czechoslovak koruna", "Kčs", Iso4217Historic, new DateTime(1993, 2, 8), new DateTime(7040, 1, 1)), // replaced by CZK and SKK (CZK and EUR)
                ["ISO-4217-HISTORIC::MKN"] = new CurrencyInfo("MKN", 0, NotApplicableByte, "Old Macedonian denar A/93", "ден", Iso4217Historic, new DateTime(1993, 12, 31)), // replaced by MKD
                ["ISO-4217-HISTORIC::MXP"] = new CurrencyInfo("MXP", 484, NotApplicableByte, "Mexican peso", "$", Iso4217Historic, new DateTime(1993, 12, 31)), // replaced by MXN
                ["ISO-4217-HISTORIC::ZRZ"] = new CurrencyInfo("ZRZ",   0, 3, "Zaïrean zaïre", "Ƶ", Iso4217Historic, new DateTime(1993, 12, 31), new DateTime(1967, 1, 1)), // replaced by ZRN
                ["ISO-4217-HISTORIC::YUN"] = new CurrencyInfo("YUN",   0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1992, 6, 30), new DateTime(1990, 1, 1)), // replaced by YUR
                ["ISO-4217-HISTORIC::SDP"] = new CurrencyInfo("SDP", 736, NotApplicableByte, "Sudanese old pound", "ج.س.", Iso4217Historic, new DateTime(1992, 6, 8), new DateTime(1956, 1, 1)), // replaced by SDD
                ["ISO-4217-HISTORIC::ARA"] = new CurrencyInfo("ARA",   0, 2, "Argentine austral", "₳", Iso4217Historic, new DateTime(1991, 12, 31), new DateTime(1985, 6, 15)), // replaced by ARS
                ["ISO-4217-HISTORIC::PEI"] = new CurrencyInfo("PEI",   0, NotApplicableByte, "Peruvian inti", "I/.", Iso4217Historic, new DateTime(1991, 10, 1), new DateTime(1985, 2, 1)), // replaced by PEN
                ["ISO-4217-HISTORIC::SUR"] = new CurrencyInfo("SUR", 810, NotApplicableByte, "Soviet Union Ruble", "руб", Iso4217Historic, new DateTime(1991, 12, 31), new DateTime(1961, 1, 1)), // replaced by RUR
                ["ISO-4217-HISTORIC::AOK"] = new CurrencyInfo("AOK", 024, 0, "Angolan kwanza", "Kz", Iso4217Historic, new DateTime(1990, 9, 24), new DateTime(1977, 1, 8)), // replaced by AON
                ["ISO-4217-HISTORIC::DDM"] = new CurrencyInfo("DDM", 278, NotApplicableByte, "East German Mark of the GDR (East Germany)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1990, 7, 1), new DateTime(1948, 6, 21)), // replaced by DEM (EUR)
                ["ISO-4217-HISTORIC::BRN"] = new CurrencyInfo("BRN",   0, 2, "Brazilian cruzado novo", "NCz$", Iso4217Historic, new DateTime(1990, 3, 15), new DateTime(1989, 1, 16)), // replaced by BRE
                ["ISO-4217-HISTORIC::YUD"] = new CurrencyInfo("YUD", 891, 2, "New Yugoslavian Dinar", "дин.", Iso4217Historic, new DateTime(1989, 12, 31), new DateTime(1966, 1, 1)), // replaced by YUN
                ["ISO-4217-HISTORIC::BRC"] = new CurrencyInfo("BRC",   0, 2, "Brazilian cruzado", "Cz$", Iso4217Historic, new DateTime(1989, 1, 15), new DateTime(1986, 2, 28)), // replaced by BRN
                ["ISO-4217-HISTORIC::BOP"] = new CurrencyInfo("BOP", 068, 2, "Peso boliviano", "b$.", Iso4217Historic, new DateTime(1987, 1, 1), new DateTime(1963, 1, 1)), // replaced by BOB
                ["ISO-4217-HISTORIC::UGS"] = new CurrencyInfo("UGS", 800, NotApplicableByte, "Ugandan shilling A/87", "USh", Iso4217Historic, new DateTime(1987, 12, 31)), // replaced by UGX
                ["ISO-4217-HISTORIC::BRB"] = new CurrencyInfo("BRB", 076, 2, "Brazilian cruzeiro", "₢", Iso4217Historic, new DateTime(1986, 2, 28), new DateTime(1970, 1, 1)), // replaced by BRC
                ["ISO-4217-HISTORIC::ILR"] = new CurrencyInfo("ILR", 376, 2, "Israeli shekel", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 12, 31), new DateTime(1980, 2, 24)), // replaced by ILS
                ["ISO-4217-HISTORIC::ARP"] = new CurrencyInfo("ARP",   0, 2, "Argentine peso argentino", "$a", Iso4217Historic, new DateTime(1985, 6, 14), new DateTime(1983, 6, 6)), // replaced by ARA
                ["ISO-4217-HISTORIC::PEH"] = new CurrencyInfo("PEH", 604, NotApplicableByte, "Peruvian old sol", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 2, 1), new DateTime(1863, 1, 1)), // replaced by PEI
                ["ISO-4217-HISTORIC::GQE"] = new CurrencyInfo("GQE",   0, NotApplicableByte, "Equatorial Guinean ekwele", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 12, 31), new DateTime(1975, 1, 1)), // replaced by XAF
                ["ISO-4217-HISTORIC::GNE"] = new CurrencyInfo("GNE", 324, NotApplicableByte, "Guinean syli", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 12, 31), new DateTime(1971, 1, 1)), // replaced by GNF
                ["ISO-4217-HISTORIC::MLF"] = new CurrencyInfo("MLF",   0, NotApplicableByte, "Mali franc", "MAF", Iso4217Historic, new DateTime(1984, 12, 31)), // replaced by XOF
                ["ISO-4217-HISTORIC::ARL"] = new CurrencyInfo("ARL",   0, 2, "Argentine peso ley", "$L", Iso4217Historic, new DateTime(1983, 5, 5), new DateTime(1970, 1, 1)), // replaced by ARP
                ["ISO-4217-HISTORIC::ISJ"] = new CurrencyInfo("ISJ", 352, 2, "Icelandic krona", "kr", Iso4217Historic, new DateTime(1981, 12, 31), new DateTime(1922, 1, 1)), // replaced by ISK
                ["ISO-4217-HISTORIC::MVQ"] = new CurrencyInfo("MVQ", 462, NotApplicableByte, "Maldivian rupee", "Rf", Iso4217Historic, new DateTime(1981, 12, 31)), // replaced by MVR
                ["ISO-4217-HISTORIC::ILP"] = new CurrencyInfo("ILP", 376, 3, "Israeli lira", "I£", Iso4217Historic, new DateTime(1980, 12, 31), new DateTime(1948, 1, 1)), // ISRAEL Pound,  replaced by ILR
                ["ISO-4217-HISTORIC::ZWC"] = new CurrencyInfo("ZWC", 716, 2, "Rhodesian dollar", "$", Iso4217Historic, new DateTime(1980, 12, 31), new DateTime(1970, 2, 17)), // replaced by ZWD
                ["ISO-4217-HISTORIC::LAJ"] = new CurrencyInfo("LAJ", 418, NotApplicableByte, "Pathet Lao Kip", "₭", Iso4217Historic, new DateTime(1979, 12, 31)), // replaced by LAK
                ["ISO-4217-HISTORIC::TPE"] = new CurrencyInfo("TPE",   0, NotApplicableByte, "Portuguese Timorese escudo", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1976, 12, 31), new DateTime(1959, 1, 1)), // replaced by IDR
                ["ISO-4217-HISTORIC::UYP"] = new CurrencyInfo("UYP", 858, NotApplicableByte, "Uruguay Peso", "$", Iso4217Historic, new DateTime(1975, 7, 1), new DateTime(1896, 1, 1)), // replaced by UYN
                ["ISO-4217-HISTORIC::CLE"] = new CurrencyInfo("CLE",   0, NotApplicableByte, "Chilean escudo", "Eº", Iso4217Historic, new DateTime(1975, 12, 31), new DateTime(1960, 1, 1)), // replaced by CLP
                ["ISO-4217-HISTORIC::MAF"] = new CurrencyInfo("MAF",   0, NotApplicableByte, "Moroccan franc", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1976, 12, 31), new DateTime(1921, 1, 1)), // replaced by MAD
                ["ISO-4217-HISTORIC::PTP"] = new CurrencyInfo("PTP",   0, NotApplicableByte, "Portuguese Timorese pataca", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1958, 12, 31), new DateTime(1894, 1, 1)), // replaced by TPE
                ["ISO-4217-HISTORIC::TNF"] = new CurrencyInfo("TNF",   0, 2, "Tunisian franc", "F", Iso4217Historic, new DateTime(1958, 12, 31), new DateTime(1991, 7, 1)), // replaced by TND
                ["ISO-4217-HISTORIC::NFD"] = new CurrencyInfo("NFD",   0, 2, "Newfoundland dollar", "$", Iso4217Historic, new DateTime(1949, 12, 31), new DateTime(1865, 1, 1)), // replaced by CAD

                // Added historic currencies of amendment 164 (research dates and other info)
                ["ISO-4217-HISTORIC::VNC"] = new CurrencyInfo("VNC", 704, 2, "Old Dong", "₫", Iso4217Historic, new DateTime(2014, 1, 1)), // VIETNAM, replaced by VND with same number! Formerly, it was subdivided into 10 hào.
                ["ISO-4217-HISTORIC::GNS"] = new CurrencyInfo("GNS", 324, NotApplicableByte, "Guinean Syli", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1970, 12, 31)), // GUINEA, replaced by GNE?
                ["ISO-4217-HISTORIC::UGW"] = new CurrencyInfo("UGW", 800, NotApplicableByte, "Old Shilling", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // UGANDA
                ["ISO-4217-HISTORIC::RHD"] = new CurrencyInfo("RHD", 716, NotApplicableByte, "Rhodesian Dollar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // SOUTHERN RHODESIA
                ["ISO-4217-HISTORIC::ROK"] = new CurrencyInfo("ROK", 642, NotApplicableByte, "Leu A/52", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // ROMANIA
                ["ISO-4217-HISTORIC::NIC"] = new CurrencyInfo("NIC", 558, NotApplicableByte, "Cordoba", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // NICARAGUA
                ["ISO-4217-HISTORIC::MZE"] = new CurrencyInfo("MZE", 508, NotApplicableByte, "Mozambique Escudo", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // MOZAMBIQUE
                ["ISO-4217-HISTORIC::MTP"] = new CurrencyInfo("MTP", 470, NotApplicableByte, "Maltese Pound", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // MALTA
                ["ISO-4217-HISTORIC::LSM"] = new CurrencyInfo("LSM", 426, NotApplicableByte, "Loti", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // LESOTHO
                ["ISO-4217-HISTORIC::GWE"] = new CurrencyInfo("GWE", 624, NotApplicableByte, "Guinea Escudo", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // GUINEA-BISSAU
                ["ISO-4217-HISTORIC::CSJ"] = new CurrencyInfo("CSJ", 203, NotApplicableByte, "Krona A/53", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // CZECHOSLOVAKIA
                ["ISO-4217-HISTORIC::BUK"] = new CurrencyInfo("BUK", 104, NotApplicableByte, "Kyat", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // BURMA
                ["ISO-4217-HISTORIC::BGK"] = new CurrencyInfo("BGK", 100, NotApplicableByte, "Lev A / 62", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // BULGARIA
                ["ISO-4217-HISTORIC::BGJ"] = new CurrencyInfo("BGJ", 100, NotApplicableByte, "Lev A / 52", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // BULGARIA
                ["ISO-4217-HISTORIC::ARY"] = new CurrencyInfo("ARY", 032, NotApplicableByte, "Peso", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // ARGENTINA
            };
        }

        private static CurrencyInfo[] InitializeIsoCurrenciesArray()
        {
            // TODO: Move to resource file.
            return new CurrencyInfo[]
            {
                // ISO-4217 currencies (list one)
                new CurrencyInfo("AED", 784, 2, "United Arab Emirates dirham", "د.إ"),
                new CurrencyInfo("AFN", 971, 2, "Afghan afghani", "؋"),
                new CurrencyInfo("ALL", 008, 2, "Albanian lek", "L"),
                new CurrencyInfo("AMD", 051, 2, "Armenian dram", "֏"),
                new CurrencyInfo("ANG", 532, 2, "Netherlands Antillean guilder", "ƒ"),
                new CurrencyInfo("AOA", 973, 2, "Angolan kwanza", "Kz"),
                new CurrencyInfo("ARS", 032, 2, "Argentine peso", "$"),
                new CurrencyInfo("AUD", 036, 2, "Australian dollar", "$"),
                new CurrencyInfo("AWG", 533, 2, "Aruban florin", "ƒ"),
                new CurrencyInfo("AZN", 944, 2, "Azerbaijan Manat", "ман"), // AZERBAIJAN
                new CurrencyInfo("BAM", 977, 2, "Bosnia and Herzegovina convertible mark", "KM"),
                new CurrencyInfo("BBD", 052, 2, "Barbados dollar", "$"),
                new CurrencyInfo("BDT", 050, 2, "Bangladeshi taka", "৳"), // or Tk
                new CurrencyInfo("BGN", 975, 2, "Bulgarian lev", "лв."),
                new CurrencyInfo("BHD", 048, 3, "Bahraini dinar", "BD"), // or د.ب. (switched for unit tests to work)
                new CurrencyInfo("BIF", 108, 0, "Burundian franc", "FBu"),
                new CurrencyInfo("BMD", 060, 2, "Bermudian dollar", "$"),
                new CurrencyInfo("BND", 096, 2, "Brunei dollar", "$"), // or B$
                new CurrencyInfo("BOB", 068, 2, "Boliviano", "Bs."), // or BS or $b
                new CurrencyInfo("BOV", 984, 2, "Bolivian Mvdol (funds code)", CurrencyInfo.GenericCurrencySign), // <==== not found
                new CurrencyInfo("BRL", 986, 2, "Brazilian real", "R$"),
                new CurrencyInfo("BSD", 044, 2, "Bahamian dollar", "$"),
                new CurrencyInfo("BTN", 064, 2, "Bhutanese ngultrum", "Nu."),
                new CurrencyInfo("BWP", 072, 2, "Botswana pula", "P"),
                new CurrencyInfo("BYN", 933, 2, "Belarusian ruble", "Br", validFrom: new DateTime(2006, 06, 01)),
                new CurrencyInfo("BZD", 084, 2, "Belize dollar", "BZ$"),
                new CurrencyInfo("CAD", 124, 2, "Canadian dollar", "$"),
                new CurrencyInfo("CDF", 976, 2, "Congolese franc", "FC"),
                new CurrencyInfo("CHE", 947, 2, "WIR Euro (complementary currency)", "CHE"),
                new CurrencyInfo("CHF", 756, 2, "Swiss franc", "fr."), // or CHF
                new CurrencyInfo("CHW", 948, 2, "WIR Franc (complementary currency)", "CHW"),
                new CurrencyInfo("CLF", 990, 4, "Unidad de Fomento (funds code)", "CLF"),
                new CurrencyInfo("CLP", 152, 0, "Chilean peso", "$"),
                new CurrencyInfo("CNY", 156, 2, "Chinese yuan", "¥"),
                new CurrencyInfo("COP", 170, 2, "Colombian peso", "$"),
                new CurrencyInfo("COU", 970, 2, "Unidad de Valor Real", CurrencyInfo.GenericCurrencySign), // ???
                new CurrencyInfo("CRC", 188, 2, "Costa Rican colon", "₡"),
                new CurrencyInfo("CUC", 931, 2, "Cuban convertible peso", "CUC$"), // $ or CUC
                new CurrencyInfo("CUP", 192, 2, "Cuban peso", "$"), // or ₱ (obsolete?)
                new CurrencyInfo("CVE", 132, 2, "Cape Verde escudo", "$"),
                new CurrencyInfo("CZK", 203, 2, "Czech koruna", "Kč"),
                new CurrencyInfo("DJF", 262, 0, "Djiboutian franc", "Fdj"),
                new CurrencyInfo("DKK", 208, 2, "Danish krone", "kr."),
                new CurrencyInfo("DOP", 214, 2, "Dominican peso", "RD$"), // or $
                new CurrencyInfo("DZD", 012, 2, "Algerian dinar", "DA"), // (Latin) or د.ج (Arabic)
                new CurrencyInfo("EGP", 818, 2, "Egyptian pound", "LE"), // or E£ or ج.م (Arabic)
                new CurrencyInfo("ERN", 232, 2, "Eritrean nakfa", "ERN"),
                new CurrencyInfo("ETB", 230, 2, "Ethiopian birr", "Br"), // (Latin) or ብር (Ethiopic)
                new CurrencyInfo("EUR", 978, 2, "Euro", "€"),
                new CurrencyInfo("FJD", 242, 2, "Fiji dollar", "$"), // or FJ$
                new CurrencyInfo("FKP", 238, 2, "Falkland Islands pound", "£"),
                new CurrencyInfo("GBP", 826, 2, "Pound sterling", "£"),
                new CurrencyInfo("GEL", 981, 2, "Georgian lari", "ლ."), // TODO: new symbol since July 18, 2014 => see http://en.wikipedia.org/wiki/Georgian_lari
                new CurrencyInfo("GHS", 936, 2, "Ghanaian cedi", "GH¢"), // or GH₵
                new CurrencyInfo("GIP", 292, 2, "Gibraltar pound", "£"),
                new CurrencyInfo("GMD", 270, 2, "Gambian dalasi", "D"),
                new CurrencyInfo("GNF", 324, 0, "Guinean Franc", "FG"), // (possibly also Fr or GFr)  GUINEA
                new CurrencyInfo("GTQ", 320, 2, "Guatemalan quetzal", "Q"),
                new CurrencyInfo("GYD", 328, 2, "Guyanese dollar", "$"), // or G$
                new CurrencyInfo("HKD", 344, 2, "Hong Kong dollar", "HK$"), // or $
                new CurrencyInfo("HNL", 340, 2, "Honduran lempira", "L"),
                new CurrencyInfo("HRK", 191, 2, "Croatian kuna", "kn"),
                new CurrencyInfo("HTG", 332, 2, "Haitian gourde", "G"),
                new CurrencyInfo("HUF", 348, 2, "Hungarian forint", "Ft"),
                new CurrencyInfo("IDR", 360, 2, "Indonesian rupiah", "Rp"),
                new CurrencyInfo("ILS", 376, 2, "Israeli new shekel", "₪"),
                new CurrencyInfo("INR", 356, 2, "Indian rupee", "₹"),
                new CurrencyInfo("IQD", 368, 3, "Iraqi dinar", "د.ع"),
                new CurrencyInfo("IRR", 364, 2, "Iranian rial", "ريال"),
                new CurrencyInfo("ISK", 352, 0, "Icelandic króna", "kr"),
                new CurrencyInfo("JMD", 388, 2, "Jamaican dollar", "J$"), // or $
                new CurrencyInfo("JOD", 400, 3, "Jordanian dinar", "د.ا.‏"),
                new CurrencyInfo("JPY", 392, 0, "Japanese yen", "¥"),
                new CurrencyInfo("KES", 404, 2, "Kenyan shilling", "KSh"),
                new CurrencyInfo("KGS", 417, 2, "Kyrgyzstani som", "сом"),
                new CurrencyInfo("KHR", 116, 2, "Cambodian riel", "៛"),
                new CurrencyInfo("KMF", 174, 0, "Comorian Franc", "CF"), // COMOROS (THE)
                new CurrencyInfo("KPW", 408, 2, "North Korean won", "₩"),
                new CurrencyInfo("KRW", 410, 0, "South Korean won", "₩"),
                new CurrencyInfo("KWD", 414, 3, "Kuwaiti dinar", "د.ك"), // or K.D.
                new CurrencyInfo("KYD", 136, 2, "Cayman Islands dollar", "$"),
                new CurrencyInfo("KZT", 398, 2, "Kazakhstani tenge", "₸"),
                new CurrencyInfo("LAK", 418, 2, "Lao Kip", "₭"), // or ₭N,  LAO PEOPLE’S DEMOCRATIC REPUBLIC(THE), ISO says minor unit=2 but wiki says Historically, one kip was divided into 100 att (ອັດ).
                new CurrencyInfo("LBP", 422, 2, "Lebanese pound", "ل.ل"),
                new CurrencyInfo("LKR", 144, 2, "Sri Lankan rupee", "Rs"), // or රු
                new CurrencyInfo("LRD", 430, 2, "Liberian dollar", "$"), // or L$, LD$
                new CurrencyInfo("LSL", 426, 2, "Lesotho loti", "L"), // L or M (pl.)
                new CurrencyInfo("LYD", 434, 3, "Libyan dinar", "ل.د"), // or LD
                new CurrencyInfo("MAD", 504, 2, "Moroccan dirham", "د.م."),
                new CurrencyInfo("MDL", 498, 2, "Moldovan leu", "L"),
                new CurrencyInfo("MGA", 969, Z07Byte, "Malagasy ariary", "Ar"),  // divided into five subunits rather than by a power of ten. 5 is 10 to the power of 0.69897...
                new CurrencyInfo("MKD", 807, 2, "Macedonian denar", "ден"),
                new CurrencyInfo("MMK", 104, 2, "Myanma kyat", "K"),
                new CurrencyInfo("MNT", 496, 2, "Mongolian tugrik", "₮"),
                new CurrencyInfo("MOP", 446, 2, "Macanese pataca", "MOP$"),
                new CurrencyInfo("MRU", 929, Z07Byte, "Mauritanian ouguiya", "UM", validFrom: new DateTime(2018, 01, 01)), // divided into five subunits rather than by a power of ten. 5 is 10 to the power of 0.69897...
                new CurrencyInfo("MUR", 480, 2, "Mauritian rupee", "Rs"),
                new CurrencyInfo("MVR", 462, 2, "Maldivian rufiyaa", "Rf"), // or , MRf, MVR, .ރ or /-
                new CurrencyInfo("MWK", 454, 2, "Malawi kwacha", "MK"),
                new CurrencyInfo("MXN", 484, 2, "Mexican peso", "$"),
                new CurrencyInfo("MXV", 979, 2, "Mexican Unidad de Inversion (UDI) (funds code)", CurrencyInfo.GenericCurrencySign),  // <==== not found
                new CurrencyInfo("MYR", 458, 2, "Malaysian ringgit", "RM"),
                new CurrencyInfo("MZN", 943, 2, "Mozambican metical", "MTn"), // or MTN
                new CurrencyInfo("NAD", 516, 2, "Namibian dollar", "N$"), // or $
                new CurrencyInfo("NGN", 566, 2, "Nigerian naira", "₦"),
                new CurrencyInfo("NIO", 558, 2, "Nicaraguan córdoba", "C$"),
                new CurrencyInfo("NOK", 578, 2, "Norwegian krone", "kr"),
                new CurrencyInfo("NPR", 524, 2, "Nepalese rupee", "Rs"), // or ₨ or रू
                new CurrencyInfo("NZD", 554, 2, "New Zealand dollar", "$"),
                new CurrencyInfo("OMR", 512, 3, "Omani rial", "ر.ع."),
                new CurrencyInfo("PAB", 590, 2, "Panamanian balboa", "B/."),
                new CurrencyInfo("PEN", 604, 2, "Peruvian sol", "S/."),
                new CurrencyInfo("PGK", 598, 2, "Papua New Guinean kina", "K"),
                new CurrencyInfo("PHP", 608, 2, "Philippine Peso", "₱"), // or P or PHP or PhP
                new CurrencyInfo("PKR", 586, 2, "Pakistani rupee", "Rs"),
                new CurrencyInfo("PLN", 985, 2, "Polish złoty", "zł"),
                new CurrencyInfo("PYG", 600, 0, "Paraguayan guaraní", "₲"),
                new CurrencyInfo("QAR", 634, 2, "Qatari riyal", "ر.ق"), // or QR
                new CurrencyInfo("RON", 946, 2, "Romanian new leu", "lei"),
                new CurrencyInfo("RSD", 941, 2, "Serbian dinar", "РСД"), // or RSD (or дин or d./д)
                new CurrencyInfo("RUB", 643, 2, "Russian rouble", "₽"), // or R or руб (both onofficial)
                new CurrencyInfo("RWF", 646, 0, "Rwandan franc", "RFw"), // or RF, R₣
                new CurrencyInfo("SAR", 682, 2, "Saudi riyal", "ر.س"), // or SR (Latin) or ﷼‎ (Unicode)
                new CurrencyInfo("SBD", 090, 2, "Solomon Islands dollar", "SI$"),
                new CurrencyInfo("SCR", 690, 2, "Seychelles rupee", "SR"), // or SRe
                new CurrencyInfo("SDG", 938, 2, "Sudanese pound", "ج.س."),
                new CurrencyInfo("SEK", 752, 2, "Swedish krona/kronor", "kr"),
                new CurrencyInfo("SGD", 702, 2, "Singapore dollar", "S$"), // or $
                new CurrencyInfo("SHP", 654, 2, "Saint Helena pound", "£"),
                new CurrencyInfo("SLL", 694, 2, "Sierra Leonean leone", "Le"),
                new CurrencyInfo("SOS", 706, 2, "Somali shilling", "S"), // or Sh.So.
                new CurrencyInfo("SRD", 968, 2, "Surinamese dollar", "$"),
                new CurrencyInfo("SSP", 728, 2, "South Sudanese pound", "£"), // not sure about symbol...
                new CurrencyInfo("SVC", 222, 2, "El Salvador Colon", "₡"),
                new CurrencyInfo("SYP", 760, 2, "Syrian pound", "ܠ.ܣ.‏"), // or LS or £S (or £)
                new CurrencyInfo("SZL", 748, 2, "Swazi lilangeni", "L"), // or E (plural)
                new CurrencyInfo("THB", 764, 2, "Thai baht", "฿"),
                new CurrencyInfo("TJS", 972, 2, "Tajikistani somoni", "смн"),
                new CurrencyInfo("TMT", 934, 2, "Turkmenistani manat", "m"), // or T?
                new CurrencyInfo("TND", 788, 3, "Tunisian dinar", "د.ت"), // or DT (Latin)
                new CurrencyInfo("TOP", 776, 2, "Tongan paʻanga", "T$"), // (sometimes PT)
                new CurrencyInfo("TRY", 949, 2, "Turkish lira", "₺"),
                new CurrencyInfo("TTD", 780, 2, "Trinidad and Tobago dollar", "$"), // or TT$
                new CurrencyInfo("TWD", 901, 2, "New Taiwan dollar", "NT$"), // or $
                new CurrencyInfo("TZS", 834, 2, "Tanzanian shilling", "x/y"), // or TSh
                new CurrencyInfo("UAH", 980, 2, "Ukrainian hryvnia", "₴"),
                new CurrencyInfo("UGX", 800, 0, "Ugandan shilling", "USh"),
                new CurrencyInfo("USD", 840, 2, "United States dollar", "$"), // or US$
                new CurrencyInfo("USN", 997, 2, "United States dollar (next day) (funds code)", "$"),
                new CurrencyInfo("UYI", 940, 0, "Uruguay Peso en Unidades Indexadas (UI) (funds code)", CurrencyInfo.GenericCurrencySign), // List two
                new CurrencyInfo("UYU", 858, 2, "Uruguayan peso", "$"), // or $U
                new CurrencyInfo("UZS", 860, 2, "Uzbekistan som", "лв"), // or сўм ?
                new CurrencyInfo("VES", 928, 2, "Venezuelan Bolívar Soberano", "Bs.", validFrom: new DateTime(2018, 8, 20)), // or Bs.F. , Amendment 167 talks about delay but from multiple sources on the web the date seems to be 20 aug.
                new CurrencyInfo("VND", 704, 0, "Vietnamese dong", "₫"),
                new CurrencyInfo("VUV", 548, 0, "Vanuatu vatu", "VT"),
                new CurrencyInfo("WST", 882, 2, "Samoan tala", "WS$"), // sometimes SAT, ST or T
                new CurrencyInfo("XAF", 950, 0, "CFA franc BEAC", "FCFA"),
                new CurrencyInfo("XAG", 961, NotApplicableByte, "Silver (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XAU", 959, NotApplicableByte, "Gold (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XBA", 955, NotApplicableByte, "European Composite Unit (EURCO) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XBB", 956, NotApplicableByte, "European Monetary Unit (E.M.U.-6) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XBC", 957, NotApplicableByte, "European Unit of Account 9 (E.U.A.-9) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XBD", 958, NotApplicableByte, "European Unit of Account 17 (E.U.A.-17) (bond market unit)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XCD", 951, 2, "East Caribbean dollar", "$"), // or EC$
                new CurrencyInfo("XDR", 960, NotApplicableByte, "Special drawing rights", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XOF", 952, 0, "CFA franc BCEAO", "CFA"),
                new CurrencyInfo("XPD", 964, NotApplicableByte, "Palladium (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XPF", 953, 0, "CFP franc", "F"),
                new CurrencyInfo("XPT", 962, NotApplicableByte, "Platinum (one troy ounce)", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XSU", 994, NotApplicableByte, "SUCRE", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XTS", 963, NotApplicableByte, "Code reserved for testing purposes", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XUA", 965, NotApplicableByte, "ADB Unit of Account", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("XXX", 999, NotApplicableByte, "No currency", CurrencyInfo.GenericCurrencySign),
                new CurrencyInfo("YER", 886, 2, "Yemeni rial", "﷼"), // or ر.ي.‏‏ ?
                new CurrencyInfo("ZAR", 710, 2, "South African rand", "R"),
                new CurrencyInfo("ZMW", 967, 2, "Zambian kwacha", "ZK"), // or ZMW
                new CurrencyInfo("ZWL", 932, 2, "Zimbabwean dollar", "$"),
                new CurrencyInfo("STN", 930, 2, "Dobra", "Db", validFrom: new DateTime(2018, 1, 1)), // New Currency of São Tomé and Príncipe from 1 Jan 2018 (Amendment 164)
                new CurrencyInfo("STD", 678, 2, "Dobra", "Db", validTo: new DateTime(2018, 1, 1)), // To be replaced Currency of São Tomé and Príncipe from 1 Jan 2018 (Amendment 164),  inflation has rendered the cêntimo obsolete
                new CurrencyInfo("UYW", 927, 4, "Unidad Previsional", "Db", validFrom: new DateTime(2018, 8, 29)), // The Central Bank of Uruguay is applying for new Fund currency code (Amendment 169)

                // Historic ISO-4217 currencies (list three)
                new CurrencyInfo("BYR", 974, 0, "Belarusian ruble", "Br", Iso4217Historic, validTo: new DateTime(2016, 12, 31), validFrom: new DateTime(2000, 01, 01)),
                new CurrencyInfo("VEF", 937, 2, "Venezuelan bolívar", "Bs.", Iso4217Historic, new DateTime(2018, 8, 20)), // replaced by VEF, The conversion rate is 1000 (old) Bolívar to 1 (new) Bolívar Soberano (1000:1). The expiration date of the current bolívar will be defined later and communicated by the Central Bank of Venezuela in due time.
                new CurrencyInfo("MRO", 478, Z07Byte, "Mauritanian ouguiya", "UM", Iso4217Historic, new DateTime(2018, 1, 1)), // replaced by MRU
                new CurrencyInfo("ESA", 996, NotApplicableByte, "Spanish peseta (account A)", "Pta", Iso4217Historic, new DateTime(2002, 3, 1)), // replaced by ESP (EUR)
                new CurrencyInfo("ESB", 995, NotApplicableByte, "Spanish peseta (account B)", "Pta", Iso4217Historic, new DateTime(2002, 3, 1)), // replaced by ESP (EUR)
                new CurrencyInfo("LTL", 440, 2, "Lithuanian litas", "Lt", Iso4217Historic, new DateTime(2014, 12, 31), new DateTime(1993, 1, 1)), // replaced by EUR
                new CurrencyInfo("USS", 998, 2, "United States dollar (same day) (funds code)", "$", Iso4217Historic, new DateTime(2014, 3, 28)), // replaced by (no successor)
                new CurrencyInfo("LVL", 428, 2, "Latvian lats", "Ls", Iso4217Historic, new DateTime(2013, 12, 31), new DateTime(1992, 1, 1)), // replaced by EUR
                new CurrencyInfo("XFU", 0, NotApplicableByte, "UIC franc (special settlement currency) International Union of Railways", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2013, 11, 7)), // replaced by EUR
                new CurrencyInfo("ZMK", 894, 2, "Zambian kwacha", "ZK", Iso4217Historic, new DateTime(2013, 1, 1), new DateTime(1968, 1, 16)), // replaced by ZMW
                new CurrencyInfo("EEK", 233, 2, "Estonian kroon", "kr", Iso4217Historic, new DateTime(2010, 12, 31), new DateTime(1992, 1, 1)), // replaced by EUR
                new CurrencyInfo("ZWR", 935, 2, "Zimbabwean dollar A/09", "$", Iso4217Historic, new DateTime(2009, 2, 2), new DateTime(2008, 8, 1)), // replaced by ZWL
                new CurrencyInfo("SKK", 703, 2, "Slovak koruna", "Sk", Iso4217Historic, new DateTime(2008, 12, 31), new DateTime(1993, 2, 8)), // replaced by EUR
                new CurrencyInfo("TMM", 795, 0, "Turkmenistani manat", "T", Iso4217Historic, new DateTime(2008, 12, 31), new DateTime(1993, 11, 1)), // replaced by TMT
                new CurrencyInfo("ZWN", 942, 2, "Zimbabwean dollar A/08", "$", Iso4217Historic, new DateTime(2008, 7, 31), new DateTime(2006, 8, 1)), // replaced by ZWR
                new CurrencyInfo("VEB", 862, 2, "Venezuelan bolívar", "Bs.", Iso4217Historic, new DateTime(2008, 1, 1)), // replaced by VEF
                new CurrencyInfo("CYP", 196, 2, "Cypriot pound", "£", Iso4217Historic, new DateTime(2007, 12, 31), new DateTime(1879, 1, 1)), // replaced by EUR
                new CurrencyInfo("MTL", 470, 2, "Maltese lira", "₤", Iso4217Historic, new DateTime(2007, 12, 31), new DateTime(1972, 1, 1)), // replaced by EUR
                new CurrencyInfo("GHC", 288, 0, "Ghanaian cedi", "GH₵", Iso4217Historic, new DateTime(2007, 7, 1), new DateTime(1967, 1, 1)), // replaced by GHS
                new CurrencyInfo("SDD", 736, NotApplicableByte, "Sudanese dinar", "£Sd", Iso4217Historic, new DateTime(2007, 1, 10), new DateTime(1992, 6, 8)), // replaced by SDG
                new CurrencyInfo("SIT", 705, 2, "Slovenian tolar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2006, 12, 31), new DateTime(1991, 10, 8)), // replaced by EUR
                new CurrencyInfo("ZWD", 716, 2, "Zimbabwean dollar A/06", "$", Iso4217Historic, new DateTime(2006, 7, 31), new DateTime(1980, 4, 18)), // replaced by ZWN
                new CurrencyInfo("MZM", 508, 0, "Mozambican metical", "MT", Iso4217Historic, new DateTime(2006, 6, 30), new DateTime(1980, 1, 1)), // replaced by MZN
                new CurrencyInfo("AZM", 031, 0, "Azerbaijani manat", "₼", Iso4217Historic, new DateTime(2006, 1, 1), new DateTime(1992, 8, 15)), // replaced by AZN
                new CurrencyInfo("CSD", 891, 2, "Serbian dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2006, 12, 31), new DateTime(2003, 7, 3)), // replaced by RSD
                new CurrencyInfo("MGF", 450, 2, "Malagasy franc", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2005, 1, 1), new DateTime(1963, 7, 1)), // replaced by MGA
                new CurrencyInfo("ROL", 642, NotApplicableByte, "Romanian leu A/05", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2005, 12, 31), new DateTime(1952, 1, 28)), // replaced by RON
                new CurrencyInfo("TRL", 792, 0, "Turkish lira A/05", "₺", Iso4217Historic, new DateTime(2005, 12, 31)), // replaced by TRY
                new CurrencyInfo("SRG", 740, NotApplicableByte, "Suriname guilder", "ƒ", Iso4217Historic, new DateTime(2004, 12, 31)), // replaced by SRD
                new CurrencyInfo("YUM", 891, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(2003, 7, 2), new DateTime(1994, 1, 24)), // replaced by CSD
                new CurrencyInfo("AFA", 004, NotApplicableByte, "Afghan afghani", "؋", Iso4217Historic, new DateTime(2003, 12, 31), new DateTime(1925, 1, 1)), // replaced by AFN
                new CurrencyInfo("XFO", 0, NotApplicableByte, "Gold franc (special settlement currency)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2003, 12, 31), new DateTime(1803, 1, 1)), // replaced by XDR
                new CurrencyInfo("GRD", 300, 2, "Greek drachma", "₯", Iso4217Historic, new DateTime(2000, 12, 31), new DateTime(1954, 1, 1)), // replaced by EUR
                new CurrencyInfo("TJR", 762, NotApplicableByte, "Tajikistani ruble", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2000, 10, 30), new DateTime(1995, 5, 10)), // replaced by TJS
                new CurrencyInfo("ECV", 983, NotApplicableByte, "Ecuador Unidad de Valor Constante (funds code)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2000, 1, 9), new DateTime(1993, 1, 1)), // replaced by (no successor)
                new CurrencyInfo("ECS", 218, 0, "Ecuadorian sucre", "S/.", Iso4217Historic, new DateTime(2000, 12, 31), new DateTime(1884, 1, 1)), // replaced by USD
                new CurrencyInfo("BYB", 112, 2, "Belarusian ruble", "Br", Iso4217Historic, new DateTime(1999, 12, 31), new DateTime(1992, 1, 1)), // replaced by BYR
                new CurrencyInfo("AOR", 982, 0, "Angolan kwanza readjustado", "Kz", Iso4217Historic, new DateTime(1999, 11, 30), new DateTime(1995, 7, 1)), // replaced by AOA
                new CurrencyInfo("BGL", 100, 2, "Bulgarian lev A/99", "лв.", Iso4217Historic, new DateTime(1999, 7, 5), new DateTime(1962, 1, 1)), // replaced by BGN
                new CurrencyInfo("ADF", 0, 2, "Andorran franc (1:1 peg to the French franc)", "Fr", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1960, 1, 1)), // replaced by EUR
                new CurrencyInfo("ADP", 020, 0, "Andorran peseta (1:1 peg to the Spanish peseta)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1869, 1, 1)), // replaced by EUR
                new CurrencyInfo("ATS", 040, 2, "Austrian schilling", "öS", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1945, 1, 1)), // replaced by EUR
                new CurrencyInfo("BEF", 056, 2, "Belgian franc (currency union with LUF)", "fr.", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1832, 1, 1)), // replaced by EUR
                new CurrencyInfo("DEM", 276, 2, "German mark", "DM", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1948, 1, 1)), // replaced by EUR
                new CurrencyInfo("ESP", 724, 0, "Spanish peseta", "Pta", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1869, 1, 1)), // replaced by EUR
                new CurrencyInfo("FIM", 246, 2, "Finnish markka", "mk", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1860, 1, 1)), // replaced by EUR
                new CurrencyInfo("FRF", 250, 2, "French franc", "Fr", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1960, 1, 1)), // replaced by EUR
                new CurrencyInfo("IEP", 372, 2, "Irish pound (punt in Irish language)", "£", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1938, 1, 1)), // replaced by EUR
                new CurrencyInfo("ITL", 380, 0, "Italian lira", "₤", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1861, 1, 1)), // replaced by EUR
                new CurrencyInfo("LUF", 442, 2, "Luxembourg franc (currency union with BEF)", "fr.", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1944, 1, 1)), // replaced by EUR
                new CurrencyInfo("MCF", 0, 2, "Monegasque franc (currency union with FRF)", "fr.", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1960, 1, 1)), // replaced by EUR
                new CurrencyInfo("NLG", 528, 2, "Dutch guilder", "ƒ", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1810, 1, 1)), // replaced by EUR
                new CurrencyInfo("PTE", 620, 0, "Portuguese escudo", "$", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(4160, 1, 1)), // replaced by EUR
                new CurrencyInfo("SML", 0, 0, "San Marinese lira (currency union with ITL and VAL)", "₤", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1864, 1, 1)), // replaced by EUR
                new CurrencyInfo("VAL", 0, 0, "Vatican lira (currency union with ITL and SML)", "₤", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1929, 1, 1)), // replaced by EUR
                new CurrencyInfo("XEU", 954, NotApplicableByte, "European Currency Unit (1 XEU = 1 EUR)", "ECU", Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1979, 3, 13)), // replaced by EUR
                new CurrencyInfo("BAD", 0, 2, "Bosnia and Herzegovina dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1998, 12, 31), new DateTime(1992, 7, 1)), // replaced by BAM
                new CurrencyInfo("RUR", 810, 2, "Russian ruble A/97", "₽", Iso4217Historic, new DateTime(1997, 12, 31), new DateTime(1992, 1, 1)), // replaced by RUB
                new CurrencyInfo("GWP", 624, NotApplicableByte, "Guinea-Bissau peso", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1997, 12, 31), new DateTime(1975, 1, 1)), // replaced by XOF
                new CurrencyInfo("ZRN", 180, 2, "Zaïrean new zaïre", "Ƶ", Iso4217Historic, new DateTime(1997, 12, 31), new DateTime(1993, 1, 1)), // replaced by CDF
                new CurrencyInfo("UAK", 804, NotApplicableByte, "Ukrainian karbovanets", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1996, 9, 1), new DateTime(1992, 10, 1)), // replaced by UAH
                new CurrencyInfo("YDD", 720, NotApplicableByte, "South Yemeni dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1996, 6, 11)), // replaced by YER
                new CurrencyInfo("AON", 024, 0, "Angolan new kwanza", "Kz", Iso4217Historic, new DateTime(1995, 6, 30), new DateTime(1990, 9, 25)), // replaced by AOR
                new CurrencyInfo("ZAL", 991, NotApplicableByte, "South African financial rand (funds code)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1995, 3, 13), new DateTime(1985, 9, 1)), // replaced by (no successor)
                new CurrencyInfo("PLZ", 616, NotApplicableByte, "Polish zloty A/94", "zł", Iso4217Historic, new DateTime(1994, 12, 31), new DateTime(1950, 10, 30)), // replaced by PLN
                new CurrencyInfo("BRR", 0, 2, "Brazilian cruzeiro real", "CR$", Iso4217Historic, new DateTime(1994, 6, 30), new DateTime(1993, 8, 1)), // replaced by BRL
                new CurrencyInfo("HRD", 0, NotApplicableByte, "Croatian dinar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1994, 5, 30), new DateTime(1991, 12, 23)), // replaced by HRK
                new CurrencyInfo("YUG", 0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1994, 1, 23), new DateTime(1994, 1, 1)), // replaced by YUM
                new CurrencyInfo("YUO", 0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1993, 12, 31), new DateTime(1993, 10, 1)), // replaced by YUG
                new CurrencyInfo("YUR", 0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1993, 9, 30), new DateTime(1992, 7, 1)), // replaced by YUO
                new CurrencyInfo("BRE", 0, 2, "Brazilian cruzeiro", "₢", Iso4217Historic, new DateTime(1993, 8, 1), new DateTime(1990, 3, 15)), // replaced by BRR
                new CurrencyInfo("UYN", 858, NotApplicableByte, "Uruguay Peso", "$U", Iso4217Historic, new DateTime(1993, 3, 1), new DateTime(1975, 7, 1)), // replaced by UYU
                new CurrencyInfo("CSK", 200, NotApplicableByte, "Czechoslovak koruna", "Kčs", Iso4217Historic, new DateTime(1993, 2, 8), new DateTime(7040, 1, 1)), // replaced by CZK and SKK (CZK and EUR)
                new CurrencyInfo("MKN", 0, NotApplicableByte, "Old Macedonian denar A/93", "ден", Iso4217Historic, new DateTime(1993, 12, 31)), // replaced by MKD
                new CurrencyInfo("MXP", 484, NotApplicableByte, "Mexican peso", "$", Iso4217Historic, new DateTime(1993, 12, 31)), // replaced by MXN
                new CurrencyInfo("ZRZ", 0, 3, "Zaïrean zaïre", "Ƶ", Iso4217Historic, new DateTime(1993, 12, 31), new DateTime(1967, 1, 1)), // replaced by ZRN
                new CurrencyInfo("YUN", 0, 2, "Yugoslav dinar", "дин.", Iso4217Historic, new DateTime(1992, 6, 30), new DateTime(1990, 1, 1)), // replaced by YUR
                new CurrencyInfo("SDP", 736, NotApplicableByte, "Sudanese old pound", "ج.س.", Iso4217Historic, new DateTime(1992, 6, 8), new DateTime(1956, 1, 1)), // replaced by SDD
                new CurrencyInfo("ARA", 0, 2, "Argentine austral", "₳", Iso4217Historic, new DateTime(1991, 12, 31), new DateTime(1985, 6, 15)), // replaced by ARS
                new CurrencyInfo("PEI", 0, NotApplicableByte, "Peruvian inti", "I/.", Iso4217Historic, new DateTime(1991, 10, 1), new DateTime(1985, 2, 1)), // replaced by PEN
                new CurrencyInfo("SUR", 810, NotApplicableByte, "Soviet Union Ruble", "руб", Iso4217Historic, new DateTime(1991, 12, 31), new DateTime(1961, 1, 1)), // replaced by RUR
                new CurrencyInfo("AOK", 024, 0, "Angolan kwanza", "Kz", Iso4217Historic, new DateTime(1990, 9, 24), new DateTime(1977, 1, 8)), // replaced by AON
                new CurrencyInfo("DDM", 278, NotApplicableByte, "East German Mark of the GDR (East Germany)", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1990, 7, 1), new DateTime(1948, 6, 21)), // replaced by DEM (EUR)
                new CurrencyInfo("BRN", 0, 2, "Brazilian cruzado novo", "NCz$", Iso4217Historic, new DateTime(1990, 3, 15), new DateTime(1989, 1, 16)), // replaced by BRE
                new CurrencyInfo("YUD", 891, 2, "New Yugoslavian Dinar", "дин.", Iso4217Historic, new DateTime(1989, 12, 31), new DateTime(1966, 1, 1)), // replaced by YUN
                new CurrencyInfo("BRC", 0, 2, "Brazilian cruzado", "Cz$", Iso4217Historic, new DateTime(1989, 1, 15), new DateTime(1986, 2, 28)), // replaced by BRN
                new CurrencyInfo("BOP", 068, 2, "Peso boliviano", "b$.", Iso4217Historic, new DateTime(1987, 1, 1), new DateTime(1963, 1, 1)), // replaced by BOB
                new CurrencyInfo("UGS", 800, NotApplicableByte, "Ugandan shilling A/87", "USh", Iso4217Historic, new DateTime(1987, 12, 31)), // replaced by UGX
                new CurrencyInfo("BRB", 076, 2, "Brazilian cruzeiro", "₢", Iso4217Historic, new DateTime(1986, 2, 28), new DateTime(1970, 1, 1)), // replaced by BRC
                new CurrencyInfo("ILR", 376, 2, "Israeli shekel", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 12, 31), new DateTime(1980, 2, 24)), // replaced by ILS
                new CurrencyInfo("ARP", 0, 2, "Argentine peso argentino", "$a", Iso4217Historic, new DateTime(1985, 6, 14), new DateTime(1983, 6, 6)), // replaced by ARA
                new CurrencyInfo("PEH", 604, NotApplicableByte, "Peruvian old sol", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 2, 1), new DateTime(1863, 1, 1)), // replaced by PEI
                new CurrencyInfo("GQE", 0, NotApplicableByte, "Equatorial Guinean ekwele", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 12, 31), new DateTime(1975, 1, 1)), // replaced by XAF
                new CurrencyInfo("GNE", 324, NotApplicableByte, "Guinean syli", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1985, 12, 31), new DateTime(1971, 1, 1)), // replaced by GNF
                new CurrencyInfo("MLF", 0, NotApplicableByte, "Mali franc", "MAF", Iso4217Historic, new DateTime(1984, 12, 31)), // replaced by XOF
                new CurrencyInfo("ARL", 0, 2, "Argentine peso ley", "$L", Iso4217Historic, new DateTime(1983, 5, 5), new DateTime(1970, 1, 1)), // replaced by ARP
                new CurrencyInfo("ISJ", 352, 2, "Icelandic krona", "kr", Iso4217Historic, new DateTime(1981, 12, 31), new DateTime(1922, 1, 1)), // replaced by ISK
                new CurrencyInfo("MVQ", 462, NotApplicableByte, "Maldivian rupee", "Rf", Iso4217Historic, new DateTime(1981, 12, 31)), // replaced by MVR
                new CurrencyInfo("ILP", 376, 3, "Israeli lira", "I£", Iso4217Historic, new DateTime(1980, 12, 31), new DateTime(1948, 1, 1)), // ISRAEL Pound,  replaced by ILR
                new CurrencyInfo("ZWC", 716, 2, "Rhodesian dollar", "$", Iso4217Historic, new DateTime(1980, 12, 31), new DateTime(1970, 2, 17)), // replaced by ZWD
                new CurrencyInfo("LAJ", 418, NotApplicableByte, "Pathet Lao Kip", "₭", Iso4217Historic, new DateTime(1979, 12, 31)), // replaced by LAK
                new CurrencyInfo("TPE", 0, NotApplicableByte, "Portuguese Timorese escudo", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1976, 12, 31), new DateTime(1959, 1, 1)), // replaced by IDR
                new CurrencyInfo("UYP", 858, NotApplicableByte, "Uruguay Peso", "$", Iso4217Historic, new DateTime(1975, 7, 1), new DateTime(1896, 1, 1)), // replaced by UYN
                new CurrencyInfo("CLE", 0, NotApplicableByte, "Chilean escudo", "Eº", Iso4217Historic, new DateTime(1975, 12, 31), new DateTime(1960, 1, 1)), // replaced by CLP
                new CurrencyInfo("MAF", 0, NotApplicableByte, "Moroccan franc", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1976, 12, 31), new DateTime(1921, 1, 1)), // replaced by MAD
                new CurrencyInfo("PTP", 0, NotApplicableByte, "Portuguese Timorese pataca", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1958, 12, 31), new DateTime(1894, 1, 1)), // replaced by TPE
                new CurrencyInfo("TNF", 0, 2, "Tunisian franc", "F", Iso4217Historic, new DateTime(1958, 12, 31), new DateTime(1991, 7, 1)), // replaced by TND
                new CurrencyInfo("NFD", 0, 2, "Newfoundland dollar", "$", Iso4217Historic, new DateTime(1949, 12, 31), new DateTime(1865, 1, 1)), // replaced by CAD

                // Added historic currencies of amendment 164 (research dates and other info)
                new CurrencyInfo("VNC", 704, 2, "Old Dong", "₫", Iso4217Historic, new DateTime(2014, 1, 1)), // VIETNAM, replaced by VND with same number! Formerly, it was subdivided into 10 hào.
                new CurrencyInfo("GNS", 324, NotApplicableByte, "Guinean Syli", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(1970, 12, 31)), // GUINEA, replaced by GNE?
                new CurrencyInfo("UGW", 800, NotApplicableByte, "Old Shilling", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // UGANDA
                new CurrencyInfo("RHD", 716, NotApplicableByte, "Rhodesian Dollar", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // SOUTHERN RHODESIA
                new CurrencyInfo("ROK", 642, NotApplicableByte, "Leu A/52", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // ROMANIA
                new CurrencyInfo("NIC", 558, NotApplicableByte, "Cordoba", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // NICARAGUA
                new CurrencyInfo("MZE", 508, NotApplicableByte, "Mozambique Escudo", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // MOZAMBIQUE
                new CurrencyInfo("MTP", 470, NotApplicableByte, "Maltese Pound", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // MALTA
                new CurrencyInfo("LSM", 426, NotApplicableByte, "Loti", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // LESOTHO
                new CurrencyInfo("GWE", 624, NotApplicableByte, "Guinea Escudo", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // GUINEA-BISSAU
                new CurrencyInfo("CSJ", 203, NotApplicableByte, "Krona A/53", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // CZECHOSLOVAKIA
                new CurrencyInfo("BUK", 104, NotApplicableByte, "Kyat", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // BURMA
                new CurrencyInfo("BGK", 100, NotApplicableByte, "Lev A / 62", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // BULGARIA
                new CurrencyInfo("BGJ", 100, NotApplicableByte, "Lev A / 52", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // BULGARIA
                new CurrencyInfo("ARY", 032, NotApplicableByte, "Peso", CurrencyInfo.GenericCurrencySign, Iso4217Historic, new DateTime(2017, 9, 22)), // ARGENTINA
            };
        }
    }
}
