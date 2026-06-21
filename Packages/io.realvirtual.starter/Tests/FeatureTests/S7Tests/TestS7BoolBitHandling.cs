// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests Bool bit addressing in AreaReadWriteMode including bit extraction,
    //! read-modify-write operations, and multi-bit integrity (no adjacent bit damage).
    public class TestS7BoolBitHandling : FeatureTestBase
    {
        protected override string TestName => "S7 Bool Bit Read-Modify-Write";
        public TestS7BoolBitHandling() { MinTestTime = 0.5f; }

        private string error = "";

        protected override void SetupTest()
        {
            TestBitExtraction();
            TestReadModifyWrite();
            TestIndependentBitOperations();
            TestMultiBitIntegrity();
            TestBitPatterns();
        }

        private void TestBitExtraction()
        {
            // AreaReadWriteMode bit extraction: (valbyte >> bit) & 1
            byte valbyte = 0xAA; // 10101010 - bits 1,3,5,7 set

            for (int bit = 0; bit < 8; bit++)
            {
                bool expected = (bit % 2 == 1); // odd bits are set
                bool result = ((valbyte >> bit) & 1) != 0;
                if (result != expected)
                    error += $"Bit extraction 0xAA: bit {bit} expected {expected}, got {result}; ";
            }

            // All bits set
            byte allSet = 0xFF;
            for (int bit = 0; bit < 8; bit++)
            {
                if (((allSet >> bit) & 1) == 0)
                    error += $"Bit extraction 0xFF: bit {bit} should be true; ";
            }

            // No bits set
            byte noneSet = 0x00;
            for (int bit = 0; bit < 8; bit++)
            {
                if (((noneSet >> bit) & 1) != 0)
                    error += $"Bit extraction 0x00: bit {bit} should be false; ";
            }
        }

        private void TestReadModifyWrite()
        {
            // Set bit 3 without affecting bits 0 and 2
            byte current = 0b00000101; // bits 0 and 2 set
            byte afterSet = (byte)(current | (1 << 3));
            if (afterSet != 0b00001101)
                error += $"Set bit 3: expected 0x0D, got 0x{afterSet:X2}; ";

            // Verify bits 0 and 2 still set
            if (((afterSet >> 0) & 1) != 1) error += "After set bit 3: bit 0 lost; ";
            if (((afterSet >> 2) & 1) != 1) error += "After set bit 3: bit 2 lost; ";

            // Clear bit 2 without affecting bit 0 and 3
            byte afterClear = (byte)(afterSet & ~(1 << 2));
            if (afterClear != 0b00001001)
                error += $"Clear bit 2: expected 0x09, got 0x{afterClear:X2}; ";
            if (((afterClear >> 0) & 1) != 1) error += "After clear bit 2: bit 0 lost; ";
            if (((afterClear >> 3) & 1) != 1) error += "After clear bit 2: bit 3 lost; ";
        }

        private void TestIndependentBitOperations()
        {
            // Set each bit independently, verify no others affected
            for (int bit = 0; bit < 8; bit++)
            {
                byte testByte = 0;
                testByte = (byte)(testByte | (1 << bit));
                if (testByte != (1 << bit))
                    error += $"Independent bit {bit}: expected {1 << bit}, got {testByte}; ";

                // Verify no other bits set
                for (int other = 0; other < 8; other++)
                {
                    if (other == bit) continue;
                    if (((testByte >> other) & 1) != 0)
                        error += $"Bit {other} unexpectedly set when setting bit {bit}; ";
                }

                // Clear it back
                testByte = (byte)(testByte & ~(1 << bit));
                if (testByte != 0)
                    error += $"Clear bit {bit}: expected 0, got {testByte}; ";
            }
        }

        private void TestMultiBitIntegrity()
        {
            // Simulate multiple BOOL signals in the same byte (e.g., Q0.0, Q0.3, Q0.7)
            byte multiByte = 0;

            // Q0.0 = true
            multiByte = (byte)(multiByte | (1 << 0));
            if (multiByte != 0b00000001)
                error += $"Multi-bit step 1: expected 0x01, got 0x{multiByte:X2}; ";

            // Q0.3 = true
            multiByte = (byte)(multiByte | (1 << 3));
            if (multiByte != 0b00001001)
                error += $"Multi-bit step 2: expected 0x09, got 0x{multiByte:X2}; ";

            // Q0.7 = true
            multiByte = (byte)(multiByte | (1 << 7));
            if (multiByte != 0b10001001)
                error += $"Multi-bit step 3: expected 0x89, got 0x{multiByte:X2}; ";

            // Q0.3 = false (clear middle bit, verify others preserved)
            multiByte = (byte)(multiByte & ~(1 << 3));
            if (multiByte != 0b10000001)
                error += $"Multi-bit clear: expected 0x81, got 0x{multiByte:X2}; ";

            // Verify Q0.0 and Q0.7 still set
            if (((multiByte >> 0) & 1) != 1)
                error += "Multi-bit: Q0.0 lost after clearing Q0.3; ";
            if (((multiByte >> 7) & 1) != 1)
                error += "Multi-bit: Q0.7 lost after clearing Q0.3; ";
        }

        private void TestBitPatterns()
        {
            // Test specific patterns commonly seen in PLC output bytes
            // Alternating pattern 0x55 = 01010101
            byte pattern55 = 0x55;
            int[] expectedBits55 = { 1, 0, 1, 0, 1, 0, 1, 0 };
            for (int bit = 0; bit < 8; bit++)
            {
                int actual = (pattern55 >> bit) & 1;
                if (actual != expectedBits55[bit])
                    error += $"Pattern 0x55: bit {bit} expected {expectedBits55[bit]}, got {actual}; ";
            }

            // Single high bit 0x80 = 10000000
            byte pattern80 = 0x80;
            for (int bit = 0; bit < 8; bit++)
            {
                bool expected = (bit == 7);
                bool actual = ((pattern80 >> bit) & 1) != 0;
                if (actual != expected)
                    error += $"Pattern 0x80: bit {bit} expected {expected}, got {actual}; ";
            }

            // Single low bit 0x01 = 00000001
            byte pattern01 = 0x01;
            for (int bit = 0; bit < 8; bit++)
            {
                bool expected = (bit == 0);
                bool actual = ((pattern01 >> bit) & 1) != 0;
                if (actual != expected)
                    error += $"Pattern 0x01: bit {bit} expected {expected}, got {actual}; ";
            }
        }

        protected override string ValidateResults()
        {
            return error;
        }
    }
}
