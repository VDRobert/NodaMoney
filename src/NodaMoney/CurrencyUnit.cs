using System;
using System.Collections.Generic;
using System.Text;

namespace NodaMoney
{
    readonly struct CurrencyUnit
    {
        private readonly byte _namespace; // 1 byte
        private readonly short _number; // 2 bytes
        private readonly string _code; // char[3] = 6 bytes = A-Z => 3 byte? char c = (char)88; int asciiCode = (int)'A';

        // char[0] = n-65 (0-25)
        // char[1] = n-65+26 (26-50)
        // char[2] = n-65+26+26 (50-75)
    }
}
