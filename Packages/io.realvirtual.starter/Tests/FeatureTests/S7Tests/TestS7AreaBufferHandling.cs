// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using UnityEngine;

namespace realvirtual
{
    //! Tests AreaReadWriteMode buffer offset calculations, BlockCopy operations,
    //! boundary handling, and GetByteLengthForSignalType mapping.
    public class TestS7AreaBufferHandling : FeatureTestBase
    {
        protected override string TestName => "S7 Area Buffer Offset Handling";
        public TestS7AreaBufferHandling() { MinTestTime = 0.5f; }

        private string error = "";

        protected override void SetupTest()
        {
            TestBufferOffsetCalculation();
            TestBufferNonZeroMin();
            TestBufferBoundary();
            TestBoolInBuffer();
            TestByteLengthForSignalType();
            TestBufferBlockCopyIntegrity();
        }

        private void TestBufferOffsetCalculation()
        {
            // DB area from byte 0 to 143 -> buffer size = 144 + 4 (safety margin used in S7Interface)
            int minArea = 0;
            int maxArea = 143;
            int bufferSize = maxArea - minArea + 1 + 4;
            byte[] buffer = new byte[bufferSize];

            // Write REAL (42.0f) at mempos=10
            float testReal = 42.0f;
            byte[] realBytes = BitConverter.GetBytes(testReal);
            int offset = 10 - minArea; // = 10
            Buffer.BlockCopy(realBytes, 0, buffer, offset, 4);

            // Read back
            byte[] readBack = new byte[4];
            Buffer.BlockCopy(buffer, offset, readBack, 0, 4);
            float readReal = BitConverter.ToSingle(readBack, 0);
            if (Mathf.Abs(readReal - testReal) > 0.001f)
                error += $"Buffer REAL at offset 10: expected {testReal}, got {readReal}; ";

            // Write INT (500) at mempos=20
            short testInt = 500;
            byte[] intBytes = BitConverter.GetBytes(testInt);
            int intOffset = 20 - minArea;
            Buffer.BlockCopy(intBytes, 0, buffer, intOffset, 2);

            byte[] readIntBack = new byte[2];
            Buffer.BlockCopy(buffer, intOffset, readIntBack, 0, 2);
            short readInt = BitConverter.ToInt16(readIntBack, 0);
            if (readInt != testInt)
                error += $"Buffer INT at offset 20: expected {testInt}, got {readInt}; ";

            // Verify REAL at offset 10 is still intact
            Buffer.BlockCopy(buffer, 10, readBack, 0, 4);
            readReal = BitConverter.ToSingle(readBack, 0);
            if (Mathf.Abs(readReal - testReal) > 0.001f)
                error += $"Buffer REAL integrity after INT write: expected {testReal}, got {readReal}; ";
        }

        private void TestBufferNonZeroMin()
        {
            // Simulate DB starting at byte 100
            int minArea = 100;
            int maxArea = 243;
            int bufferSize = maxArea - minArea + 1 + 4;
            byte[] buffer = new byte[bufferSize];

            // Signal at mempos=110 -> offset = 110 - 100 = 10
            int offset = 110 - minArea;
            short testInt = 1234;
            byte[] intBytes = BitConverter.GetBytes(testInt);
            Buffer.BlockCopy(intBytes, 0, buffer, offset, 2);

            byte[] readBack = new byte[2];
            Buffer.BlockCopy(buffer, offset, readBack, 0, 2);
            short readInt = BitConverter.ToInt16(readBack, 0);
            if (readInt != testInt)
                error += $"Buffer non-zero min INT at offset {offset}: expected {testInt}, got {readInt}; ";

            // Signal at mempos=100 (exactly at min) -> offset = 0
            int offsetMin = 100 - minArea;
            byte[] minBytes = BitConverter.GetBytes((short)42);
            Buffer.BlockCopy(minBytes, 0, buffer, offsetMin, 2);

            byte[] readMinBack = new byte[2];
            Buffer.BlockCopy(buffer, offsetMin, readMinBack, 0, 2);
            short readMin = BitConverter.ToInt16(readMinBack, 0);
            if (readMin != 42)
                error += $"Buffer at min offset: expected 42, got {readMin}; ";
        }

        private void TestBufferBoundary()
        {
            // Test DWORD at buffer boundary (last valid position for 4-byte type)
            int minArea = 0;
            int maxArea = 99;
            int bufferSize = maxArea - minArea + 1 + 4; // 104
            byte[] buffer = new byte[bufferSize];

            // DWORD at maxArea - 3 = byte 96 (occupies 96, 97, 98, 99)
            int boundaryOffset = maxArea - 3;
            byte[] dwordBytes = BitConverter.GetBytes(unchecked((uint)0xDEADBEEF));
            Buffer.BlockCopy(dwordBytes, 0, buffer, boundaryOffset, 4);

            byte[] readBack = new byte[4];
            Buffer.BlockCopy(buffer, boundaryOffset, readBack, 0, 4);
            uint readDword = BitConverter.ToUInt32(readBack, 0);
            if (readDword != 0xDEADBEEF)
                error += $"Buffer DWORD at boundary: expected 0xDEADBEEF, got 0x{readDword:X8}; ";

            // WORD at last valid 2-byte position (byte 98-99)
            int wordOffset = maxArea - 1;
            byte[] wordBytes = BitConverter.GetBytes((ushort)0xCAFE);
            Buffer.BlockCopy(wordBytes, 0, buffer, wordOffset, 2);

            byte[] readWordBack = new byte[2];
            Buffer.BlockCopy(buffer, wordOffset, readWordBack, 0, 2);
            ushort readWord = BitConverter.ToUInt16(readWordBack, 0);
            if (readWord != 0xCAFE)
                error += $"Buffer WORD at boundary: expected 0xCAFE, got 0x{readWord:X4}; ";
        }

        private void TestBoolInBuffer()
        {
            byte[] buffer = new byte[10];

            // Set byte at offset 5 with known bit pattern
            buffer[5] = 0b10101010; // bits 1,3,5,7 set

            // Extract bit 3 (should be true)
            byte boolByte = buffer[5];
            bool bit3 = ((boolByte >> 3) & 1) != 0;
            if (!bit3)
                error += "Buffer BOOL bit 3 extraction failed (should be true); ";

            // Extract bit 0 (should be false)
            bool bit0 = ((boolByte >> 0) & 1) != 0;
            if (bit0)
                error += "Buffer BOOL bit 0 should be false; ";

            // Modify bit 0 (set to true) without affecting others
            buffer[5] = (byte)(buffer[5] | (1 << 0));
            if (buffer[5] != 0b10101011)
                error += $"Buffer BOOL set bit 0: expected 0xAB, got 0x{buffer[5]:X2}; ";

            // Clear bit 7 without affecting others
            buffer[5] = (byte)(buffer[5] & ~(1 << 7));
            if (buffer[5] != 0b00101011)
                error += $"Buffer BOOL clear bit 7: expected 0x2B, got 0x{buffer[5]:X2}; ";
        }

        private void TestByteLengthForSignalType()
        {
            // Create S7Interface to access GetByteLengthForSignalType
            var go = CreateGameObject("S7Interface");
            var s7 = go.AddComponent<S7Interface>();
            s7.NoThreading = true;

            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.BOOL, 1, "BOOL");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.BYTE, 1, "BYTE");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.SINT, 1, "SINT");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.USINT, 1, "USINT");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.WORD, 2, "WORD");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.INT, 2, "INT");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.UINT, 2, "UINT");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.TIME, 2, "TIME");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.DWORD, 4, "DWORD");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.DINT, 4, "DINT");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.UDINT, 4, "UDINT");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.REAL, 4, "REAL");
            AssertByteLength(s7, S7InterfaceSignal.S7TYPE.UNDEFINED, 0, "UNDEFINED");
        }

        private void AssertByteLength(S7Interface s7, S7InterfaceSignal.S7TYPE type, int expected, string desc)
        {
            int result = s7.GetByteLengthForSignalType(type);
            if (result != expected)
                error += $"ByteLength {desc}: expected {expected}, got {result}; ";
        }

        private void TestBufferBlockCopyIntegrity()
        {
            // Write multiple values to buffer and verify they don't overlap
            int bufferSize = 100;
            byte[] buffer = new byte[bufferSize];

            // BOOL at byte 0
            buffer[0] = 0b00001000; // bit 3 set

            // INT at byte 2-3
            byte[] intBytes = BitConverter.GetBytes((short)999);
            Buffer.BlockCopy(intBytes, 0, buffer, 2, 2);

            // REAL at byte 4-7
            byte[] realBytes = BitConverter.GetBytes(3.14f);
            Buffer.BlockCopy(realBytes, 0, buffer, 4, 4);

            // DINT at byte 8-11
            byte[] dintBytes = BitConverter.GetBytes(-12345);
            Buffer.BlockCopy(dintBytes, 0, buffer, 8, 4);

            // Verify all values are still correct
            if (((buffer[0] >> 3) & 1) != 1)
                error += "BlockCopy integrity: BOOL bit 3 lost; ";

            byte[] readInt = new byte[2];
            Buffer.BlockCopy(buffer, 2, readInt, 0, 2);
            if (BitConverter.ToInt16(readInt, 0) != 999)
                error += "BlockCopy integrity: INT value corrupted; ";

            byte[] readReal = new byte[4];
            Buffer.BlockCopy(buffer, 4, readReal, 0, 4);
            if (Mathf.Abs(BitConverter.ToSingle(readReal, 0) - 3.14f) > 0.001f)
                error += "BlockCopy integrity: REAL value corrupted; ";

            byte[] readDint = new byte[4];
            Buffer.BlockCopy(buffer, 8, readDint, 0, 4);
            if (BitConverter.ToInt32(readDint, 0) != -12345)
                error += "BlockCopy integrity: DINT value corrupted; ";
        }

        protected override string ValidateResults()
        {
            return error;
        }
    }
}
