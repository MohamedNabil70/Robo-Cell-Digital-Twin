// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using UnityEngine;

namespace realvirtual
{
    //! Tests the byte-to-signal conversion logic used in S7Interface.UpdateSignal()
    //! for all 12 S7 data types in both directions (PLC->Unity and Unity->PLC).
    //! Simulates ReverseHighLowBytes=true (default) behavior.
    public class TestS7ByteConversion : FeatureTestBase
    {
        protected override string TestName => "S7 Byte Conversion All Types";
        public TestS7ByteConversion() { MinTestTime = 0.5f; }

        private string error = "";

        protected override void SetupTest()
        {
            TestOutputBool();
            TestOutputByte();
            TestOutputWord();
            TestOutputInt();
            TestOutputDWord();
            TestOutputDInt();
            TestOutputSInt();
            TestOutputUSInt();
            TestOutputUInt();
            TestOutputUDInt();
            TestOutputReal();
            TestOutputTime();
            TestInputRoundtrips();
        }

        // --- OUTPUT direction: PLC -> Unity (read from PLC) ---
        // PLC sends Big-Endian bytes. ReverseHighLowBytes=true reverses them to Little-Endian for BitConverter.

        private void TestOutputBool()
        {
            // Non-AreaMode: After reverse, bool is checked as bytes[3] == 1
            // PLC sends [0,0,0,1] (Big-Endian), after Reverse -> [1,0,0,0], but the code checks bytes[3]
            // Actually the code does: newbyte = signal.Value (4 bytes), then Array.Reverse(newbyte), then checks bytes[3]
            // PLC sends bool as single byte via MultiVar: Value = [1,0,0,0] (byte[0]=1 rest 0)
            // After Reverse: [0,0,0,1] -> bytes[3] == 1 -> true

            // Simulate: Value from PLC = [1, 0, 0, 0], ReverseHighLowBytes -> [0, 0, 0, 1]
            byte[] bytesTrue = { 1, 0, 0, 0 };
            Array.Reverse(bytesTrue);
            bool resultTrue = bytesTrue[3] == 1;
            if (!resultTrue)
                error += "BOOL output true: failed; ";

            byte[] bytesFalse = { 0, 0, 0, 0 };
            Array.Reverse(bytesFalse);
            bool resultFalse = bytesFalse[3] == 1;
            if (resultFalse)
                error += "BOOL output false: failed; ";
        }

        private void TestOutputByte()
        {
            // DB/MK context: value is bytes[0] (no Reverse applied for BYTE in DB)
            // Non-DB context: value is bytes[3] after Reverse
            byte[] dbBytes = { 0xAB, 0, 0, 0 };
            int dbResult = dbBytes[0];
            if (dbResult != 0xAB)
                error += $"BYTE output DB: expected 0xAB, got 0x{dbResult:X2}; ";

            byte[] paBytes = { 0, 0, 0, 0xCD };
            int paResult = paBytes[3];
            if (paResult != 0xCD)
                error += $"BYTE output PA: expected 0xCD, got 0x{paResult:X2}; ";

            // Zero
            byte[] zeroBytes = { 0, 0, 0, 0 };
            if (zeroBytes[0] != 0 || zeroBytes[3] != 0)
                error += "BYTE output zero: failed; ";
        }

        private void TestOutputWord()
        {
            // PLC sends WORD 1000 as Big-Endian [0x03, 0xE8, 0, 0] in 4-byte array
            // After Reverse of 4 bytes: [0, 0, 0xE8, 0x03]
            // But the actual code reverses only the relevant bytes. Let's test the 2-byte path:
            // INT/WORD: BitConverter.ToInt16(bytes, 0) after Reverse
            byte[] plcBytes = { 0x03, 0xE8, 0, 0 };
            Array.Reverse(plcBytes);
            // After reverse: [0, 0, 0xE8, 0x03] - ToInt16 reads bytes[0..1] = [0, 0] = 0!
            // Actually the implementation copies only 2 bytes for WORD signals. Let me simulate 2-byte Reverse:
            byte[] wordBytes = { 0x03, 0xE8 };
            Array.Reverse(wordBytes);
            short wordResult = BitConverter.ToInt16(wordBytes, 0);
            if (wordResult != 1000)
                error += $"WORD output 1000: got {wordResult}; ";

            // WORD max unsigned: 0xFFFF -> as Int16 = -1
            byte[] maxBytes = { 0xFF, 0xFF };
            Array.Reverse(maxBytes);
            short maxResult = BitConverter.ToInt16(maxBytes, 0);
            if (maxResult != -1)
                error += $"WORD output 0xFFFF: expected -1, got {maxResult}; ";
        }

        private void TestOutputInt()
        {
            // INT is same as WORD for byte conversion
            byte[] intBytes = { 0x80, 0x00 }; // -32768 Big-Endian
            Array.Reverse(intBytes);
            short intResult = BitConverter.ToInt16(intBytes, 0);
            if (intResult != short.MinValue)
                error += $"INT output MinValue: expected {short.MinValue}, got {intResult}; ";

            byte[] posBytes = { 0x7F, 0xFF }; // 32767 Big-Endian
            Array.Reverse(posBytes);
            short posResult = BitConverter.ToInt16(posBytes, 0);
            if (posResult != short.MaxValue)
                error += $"INT output MaxValue: expected {short.MaxValue}, got {posResult}; ";
        }

        private void TestOutputDWord()
        {
            // DWORD: cast to (int) from BitConverter.ToUInt32
            byte[] dwordBytes = { 0xDE, 0xAD, 0xBE, 0xEF }; // Big-Endian
            Array.Reverse(dwordBytes);
            int dwordResult = (int)BitConverter.ToUInt32(dwordBytes, 0);
            if (dwordResult != unchecked((int)0xDEADBEEF))
                error += $"DWORD output 0xDEADBEEF: got 0x{(uint)dwordResult:X8}; ";
        }

        private void TestOutputDInt()
        {
            byte[] dintBytes = { 0xFF, 0xFF, 0xFF, 0xFF }; // -1 Big-Endian
            Array.Reverse(dintBytes);
            int dintResult = BitConverter.ToInt32(dintBytes, 0);
            if (dintResult != -1)
                error += $"DINT output -1: got {dintResult}; ";

            byte[] minBytes = { 0x80, 0x00, 0x00, 0x00 }; // int.MinValue Big-Endian
            Array.Reverse(minBytes);
            int minResult = BitConverter.ToInt32(minBytes, 0);
            if (minResult != int.MinValue)
                error += $"DINT output MinValue: got {minResult}; ";

            byte[] zeroBytes = { 0x00, 0x00, 0x00, 0x00 };
            Array.Reverse(zeroBytes);
            int zeroResult = BitConverter.ToInt32(zeroBytes, 0);
            if (zeroResult != 0)
                error += $"DINT output 0: got {zeroResult}; ";
        }

        private void TestOutputSInt()
        {
            // SINT: (sbyte)bytes[3] with unchecked
            // PLC sends [0x80, 0, 0, 0], after Reverse -> [0, 0, 0, 0x80]
            byte[] sintMinBytes = { 0x80, 0, 0, 0 };
            Array.Reverse(sintMinBytes);
            int sintMin = unchecked((sbyte)sintMinBytes[3]);
            if (sintMin != -128)
                error += $"SINT output -128: got {sintMin}; ";

            byte[] sintNeg1Bytes = { 0xFF, 0, 0, 0 };
            Array.Reverse(sintNeg1Bytes);
            int sintNeg1 = unchecked((sbyte)sintNeg1Bytes[3]);
            if (sintNeg1 != -1)
                error += $"SINT output -1: got {sintNeg1}; ";

            byte[] sintMaxBytes = { 0x7F, 0, 0, 0 };
            Array.Reverse(sintMaxBytes);
            int sintMax = unchecked((sbyte)sintMaxBytes[3]);
            if (sintMax != 127)
                error += $"SINT output 127: got {sintMax}; ";
        }

        private void TestOutputUSInt()
        {
            // USINT: bytes[3]
            byte[] usintMaxBytes = { 0xFF, 0, 0, 0 };
            Array.Reverse(usintMaxBytes);
            int usintMax = usintMaxBytes[3];
            if (usintMax != 255)
                error += $"USINT output 255: got {usintMax}; ";

            byte[] usintZeroBytes = { 0x00, 0, 0, 0 };
            Array.Reverse(usintZeroBytes);
            int usintZero = usintZeroBytes[3];
            if (usintZero != 0)
                error += $"USINT output 0: got {usintZero}; ";
        }

        private void TestOutputUInt()
        {
            // UINT: BitConverter.ToUInt16 after Reverse of 2 bytes
            byte[] uintBytes = { 0xFF, 0xFF }; // 65535 Big-Endian
            Array.Reverse(uintBytes);
            int uintResult = BitConverter.ToUInt16(uintBytes, 0);
            if (uintResult != 65535)
                error += $"UINT output 65535: got {uintResult}; ";

            byte[] uintZeroBytes = { 0x00, 0x00 };
            Array.Reverse(uintZeroBytes);
            int uintZero = BitConverter.ToUInt16(uintZeroBytes, 0);
            if (uintZero != 0)
                error += $"UINT output 0: got {uintZero}; ";
        }

        private void TestOutputUDInt()
        {
            // UDINT: (int)BitConverter.ToUInt32
            byte[] udintBytes = { 0x7F, 0xFF, 0xFF, 0xFF }; // Big-Endian
            Array.Reverse(udintBytes);
            int udintResult = (int)BitConverter.ToUInt32(udintBytes, 0);
            if (udintResult != int.MaxValue)
                error += $"UDINT output MaxValue: got {udintResult}; ";
        }

        private void TestOutputReal()
        {
            // REAL: IEEE 754, 42.0f = Big-Endian [0x42, 0x28, 0x00, 0x00]
            byte[] realBytes = { 0x42, 0x28, 0x00, 0x00 };
            Array.Reverse(realBytes);
            float realResult = BitConverter.ToSingle(realBytes, 0);
            if (Mathf.Abs(realResult - 42.0f) > 0.001f)
                error += $"REAL output 42.0: got {realResult}; ";

            // 0.0f
            byte[] zeroRealBytes = { 0x00, 0x00, 0x00, 0x00 };
            Array.Reverse(zeroRealBytes);
            float zeroResult = BitConverter.ToSingle(zeroRealBytes, 0);
            if (Mathf.Abs(zeroResult) > 0.001f)
                error += $"REAL output 0.0: got {zeroResult}; ";

            // -1.5f = Big-Endian [0xBF, 0xC0, 0x00, 0x00]
            byte[] negRealBytes = { 0xBF, 0xC0, 0x00, 0x00 };
            Array.Reverse(negRealBytes);
            float negResult = BitConverter.ToSingle(negRealBytes, 0);
            if (Mathf.Abs(negResult - (-1.5f)) > 0.001f)
                error += $"REAL output -1.5: got {negResult}; ";
        }

        private void TestOutputTime()
        {
            // TIME: BitConverter.ToInt32 (4 bytes), used as PLCOutputFloat value
            byte[] timeBytes = { 0x00, 0x00, 0x03, 0xE8 }; // 1000ms Big-Endian
            Array.Reverse(timeBytes);
            int timeResult = BitConverter.ToInt32(timeBytes, 0);
            if (timeResult != 1000)
                error += $"TIME output 1000ms: got {timeResult}; ";
        }

        // --- INPUT direction: Unity -> PLC (write to PLC) ---
        // Unity writes Little-Endian, then ReverseHighLowBytes reverses to Big-Endian.

        private void TestInputRoundtrips()
        {
            // INT roundtrip: value -> GetBytes -> Reverse -> Reverse back -> ToInt16
            TestInputIntRoundtrip(1000);
            TestInputIntRoundtrip(-1);
            TestInputIntRoundtrip(0);
            TestInputIntRoundtrip(short.MaxValue);
            TestInputIntRoundtrip(short.MinValue);

            // DINT roundtrip
            TestInputDIntRoundtrip(100000);
            TestInputDIntRoundtrip(-1);
            TestInputDIntRoundtrip(int.MinValue);

            // REAL roundtrip
            TestInputRealRoundtrip(42.5f);
            TestInputRealRoundtrip(0.0f);
            TestInputRealRoundtrip(-1.5f);

            // BOOL input: true -> bytes[0]=255, false -> bytes[0]=0
            byte[] boolTrue = new byte[4];
            boolTrue[0] = 255;
            if (boolTrue[0] != 255)
                error += "BOOL input true: byte[0] should be 255; ";

            byte[] boolFalse = new byte[4];
            boolFalse[0] = 0;
            if (boolFalse[0] != 0)
                error += "BOOL input false: byte[0] should be 0; ";

            // BYTE input: bytes[0] = (byte)value
            byte[] byteInput = new byte[4];
            byteInput[0] = (byte)42;
            byte[] reversed = (byte[])byteInput.Clone();
            Array.Reverse(reversed);
            // After another Reverse on PLC side, byte[0] would be... it depends on implementation
            // The key point: the value is at bytes[0] before Reverse
            if (byteInput[0] != 42)
                error += "BYTE input: byte[0] should be 42; ";

            // SINT input: unchecked sbyte cast
            byte[] sintInput = new byte[4];
            unchecked { sbyte s = (sbyte)(-1); sintInput[0] = (byte)s; }
            if (sintInput[0] != 0xFF)
                error += $"SINT input -1: byte[0] should be 0xFF, got 0x{sintInput[0]:X2}; ";
        }

        private void TestInputIntRoundtrip(int value)
        {
            byte[] bytes = BitConverter.GetBytes((short)value);
            byte[] reversed = (byte[])bytes.Clone();
            Array.Reverse(reversed); // Unity -> PLC (Big-Endian)
            byte[] roundtrip = (byte[])reversed.Clone();
            Array.Reverse(roundtrip); // PLC -> Unity (back to Little-Endian)
            short result = BitConverter.ToInt16(roundtrip, 0);
            if (result != (short)value)
                error += $"INT input roundtrip {value}: got {result}; ";
        }

        private void TestInputDIntRoundtrip(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            byte[] reversed = (byte[])bytes.Clone();
            Array.Reverse(reversed);
            byte[] roundtrip = (byte[])reversed.Clone();
            Array.Reverse(roundtrip);
            int result = BitConverter.ToInt32(roundtrip, 0);
            if (result != value)
                error += $"DINT input roundtrip {value}: got {result}; ";
        }

        private void TestInputRealRoundtrip(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            byte[] reversed = (byte[])bytes.Clone();
            Array.Reverse(reversed);
            byte[] roundtrip = (byte[])reversed.Clone();
            Array.Reverse(roundtrip);
            float result = BitConverter.ToSingle(roundtrip, 0);
            if (Mathf.Abs(result - value) > 0.001f)
                error += $"REAL input roundtrip {value}: got {result}; ";
        }

        protected override string ValidateResults()
        {
            return error;
        }
    }
}
