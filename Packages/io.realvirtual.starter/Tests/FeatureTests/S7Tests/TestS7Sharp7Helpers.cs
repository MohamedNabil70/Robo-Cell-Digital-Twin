// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using Sharp7;

namespace realvirtual
{
    public class TestS7Sharp7Helpers : FeatureTestBase
    {
        protected override string TestName => "S7 Sharp7 Byte Helpers Roundtrip";
        public TestS7Sharp7Helpers() { MinTestTime = 0.5f; }

        private string error = "";

        protected override void SetupTest()
        {
            TestBitOperations();
            TestIntRoundtrip();
            TestDIntRoundtrip();
            TestRealRoundtrip();
            TestSIntRoundtrip();
            TestUSIntRoundtrip();
            TestByteRoundtrip();
            TestAllBitPositions();
        }

        private void TestBitOperations()
        {
            byte[] buf = new byte[4];
            S7.SetBitAt(ref buf, 0, 3, true);
            if (!S7.GetBitAt(buf, 0, 3))
                error += "BitAt roundtrip failed for bit 3; ";
            if (S7.GetBitAt(buf, 0, 2))
                error += "Adjacent bit 2 was unexpectedly set; ";
            if (S7.GetBitAt(buf, 0, 4))
                error += "Adjacent bit 4 was unexpectedly set; ";

            // Set and clear
            S7.SetBitAt(ref buf, 0, 3, false);
            if (S7.GetBitAt(buf, 0, 3))
                error += "BitAt clear failed for bit 3; ";
        }

        private void TestIntRoundtrip()
        {
            byte[] buf = new byte[2];

            // Positive value
            S7.SetIntAt(buf, 0, 1000);
            short result = S7.GetIntAt(buf, 0);
            if (result != 1000)
                error += $"Int roundtrip: expected 1000, got {result}; ";

            // Negative value
            S7.SetIntAt(buf, 0, -1);
            if (S7.GetIntAt(buf, 0) != -1)
                error += "Int roundtrip for -1 failed; ";

            // Zero
            S7.SetIntAt(buf, 0, 0);
            if (S7.GetIntAt(buf, 0) != 0)
                error += "Int roundtrip for 0 failed; ";

            // Max value
            S7.SetIntAt(buf, 0, short.MaxValue);
            if (S7.GetIntAt(buf, 0) != short.MaxValue)
                error += $"Int roundtrip for MaxValue failed; ";

            // Min value
            S7.SetIntAt(buf, 0, short.MinValue);
            if (S7.GetIntAt(buf, 0) != short.MinValue)
                error += $"Int roundtrip for MinValue failed; ";
        }

        private void TestDIntRoundtrip()
        {
            byte[] buf = new byte[4];

            S7.SetDIntAt(buf, 0, -2147483648);
            int result = S7.GetDIntAt(buf, 0);
            if (result != -2147483648)
                error += $"DInt min roundtrip: expected -2147483648, got {result}; ";

            S7.SetDIntAt(buf, 0, 2147483647);
            result = S7.GetDIntAt(buf, 0);
            if (result != 2147483647)
                error += $"DInt max roundtrip: expected 2147483647, got {result}; ";

            S7.SetDIntAt(buf, 0, 0);
            result = S7.GetDIntAt(buf, 0);
            if (result != 0)
                error += "DInt roundtrip for 0 failed; ";

            S7.SetDIntAt(buf, 0, -1);
            result = S7.GetDIntAt(buf, 0);
            if (result != -1)
                error += "DInt roundtrip for -1 failed; ";
        }

        private void TestRealRoundtrip()
        {
            byte[] buf = new byte[4];

            S7.SetRealAt(buf, 0, 42.5f);
            float result = S7.GetRealAt(buf, 0);
            if (Mathf.Abs(result - 42.5f) > 0.001f)
                error += $"Real roundtrip: expected 42.5, got {result}; ";

            S7.SetRealAt(buf, 0, 0.0f);
            result = S7.GetRealAt(buf, 0);
            if (Mathf.Abs(result) > 0.001f)
                error += $"Real roundtrip for 0.0: got {result}; ";

            S7.SetRealAt(buf, 0, -1.5f);
            result = S7.GetRealAt(buf, 0);
            if (Mathf.Abs(result - (-1.5f)) > 0.001f)
                error += $"Real roundtrip for -1.5: got {result}; ";

            // Large value
            S7.SetRealAt(buf, 0, 1e10f);
            result = S7.GetRealAt(buf, 0);
            if (Mathf.Abs(result - 1e10f) > 1e5f)
                error += $"Real roundtrip for 1e10: got {result}; ";
        }

        private void TestSIntRoundtrip()
        {
            byte[] buf = new byte[1];

            S7.SetSIntAt(buf, 0, -128);
            int result = S7.GetSIntAt(buf, 0);
            if (result != -128)
                error += $"SInt min: expected -128, got {result}; ";

            S7.SetSIntAt(buf, 0, 127);
            result = S7.GetSIntAt(buf, 0);
            if (result != 127)
                error += $"SInt max: expected 127, got {result}; ";

            S7.SetSIntAt(buf, 0, -1);
            result = S7.GetSIntAt(buf, 0);
            if (result != -1)
                error += $"SInt -1: got {result}; ";
        }

        private void TestUSIntRoundtrip()
        {
            byte[] buf = new byte[1];

            S7.SetUSIntAt(buf, 0, 255);
            int result = S7.GetUSIntAt(buf, 0);
            if (result != 255)
                error += $"USInt max: expected 255, got {result}; ";

            S7.SetUSIntAt(buf, 0, 0);
            result = S7.GetUSIntAt(buf, 0);
            if (result != 0)
                error += $"USInt 0: got {result}; ";
        }

        private void TestByteRoundtrip()
        {
            byte[] buf = new byte[1];

            S7.SetByteAt(buf, 0, 0xAB);
            if (S7.GetByteAt(buf, 0) != 0xAB)
                error += "Byte roundtrip for 0xAB failed; ";

            S7.SetByteAt(buf, 0, 0x00);
            if (S7.GetByteAt(buf, 0) != 0x00)
                error += "Byte roundtrip for 0x00 failed; ";

            S7.SetByteAt(buf, 0, 0xFF);
            if (S7.GetByteAt(buf, 0) != 0xFF)
                error += "Byte roundtrip for 0xFF failed; ";
        }

        private void TestAllBitPositions()
        {
            byte[] buf = new byte[1];
            for (int bit = 0; bit < 8; bit++)
            {
                buf[0] = 0;
                S7.SetBitAt(ref buf, 0, bit, true);
                if (buf[0] != (1 << bit))
                    error += $"SetBitAt pos {bit}: expected {1 << bit}, got {buf[0]}; ";
                if (!S7.GetBitAt(buf, 0, bit))
                    error += $"GetBitAt pos {bit} should be true; ";

                // Verify no other bits are set
                for (int other = 0; other < 8; other++)
                {
                    if (other == bit) continue;
                    if (S7.GetBitAt(buf, 0, other))
                        error += $"Bit {other} should be false when only bit {bit} is set; ";
                }
            }
        }

        protected override string ValidateResults()
        {
            return error;
        }
    }
}
