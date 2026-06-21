// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using Sharp7;

namespace realvirtual
{
    public class TestS7AddressParsing : FeatureTestBase
    {
        protected override string TestName => "S7 Address Parsing";
        public TestS7AddressParsing() { MinTestTime = 0.5f; }

        private S7Interface s7;
        private string error = "";

        protected override void SetupTest()
        {
            var go = CreateGameObject("S7Interface");
            s7 = go.AddComponent<S7Interface>();
            s7.NoThreading = true;

            TestAreaParsing();
            TestTypeFromName();
            TestDBTypeParsing();
            TestFirstNumExtraction();
            TestDBMemPosParsing();
            TestNumberAfterPoint();
            TestDirectionParsing();
            TestLengthParsing();
        }

        private void TestAreaParsing()
        {
            // Input areas (PLC inputs)
            AssertArea("E0.0", S7Consts.S7AreaPE, "E (Eingang)");
            AssertArea("I0.0", S7Consts.S7AreaPE, "I (Input)");
            AssertArea("e0.0", S7Consts.S7AreaPE, "e lowercase");

            // Output areas (PLC outputs)
            AssertArea("A1.0", S7Consts.S7AreaPA, "A (Ausgang)");
            AssertArea("Q1.0", S7Consts.S7AreaPA, "Q (Output)");

            // Merker area
            AssertArea("M0.0", S7Consts.S7AreaMK, "M (Merker)");
            AssertArea("MW10", S7Consts.S7AreaMK, "MW (Merker Word)");
            AssertArea("MB5", S7Consts.S7AreaMK, "MB (Merker Byte)");
            AssertArea("MD20", S7Consts.S7AreaMK, "MD (Merker DWord)");

            // Data block area
            AssertArea("DB1.DBX0.0", S7Consts.S7AreaDB, "DB (Datenbaustein)");
            AssertArea("DB100.DBW4", S7Consts.S7AreaDB, "DB large number");
        }

        private void TestTypeFromName()
        {
            // Non-DB addresses: second character determines type
            AssertType("E0.3", S7InterfaceSignal.S7TYPE.BOOL, "E bool");
            AssertType("A1.0", S7InterfaceSignal.S7TYPE.BOOL, "A bool");
            AssertType("I2.5", S7InterfaceSignal.S7TYPE.BOOL, "I bool");
            AssertType("M0.7", S7InterfaceSignal.S7TYPE.BOOL, "M bool");
            AssertType("MW10", S7InterfaceSignal.S7TYPE.WORD, "MW word");
            AssertType("MB5", S7InterfaceSignal.S7TYPE.BYTE, "MB byte");
            AssertType("MD20", S7InterfaceSignal.S7TYPE.DWORD, "MD dword");
            AssertType("AW0", S7InterfaceSignal.S7TYPE.WORD, "AW word");
            AssertType("EW4", S7InterfaceSignal.S7TYPE.WORD, "EW word");

            // DB addresses: delegate to GetDBType
            AssertType("DB1.DBX0.0", S7InterfaceSignal.S7TYPE.BOOL, "DBX bool");
            AssertType("DB1.DBW4", S7InterfaceSignal.S7TYPE.WORD, "DBW word");
            AssertType("DB1.DBB6", S7InterfaceSignal.S7TYPE.BYTE, "DBB byte");
            AssertType("DB1.DBD8", S7InterfaceSignal.S7TYPE.DWORD, "DBD dword");
        }

        private void TestDBTypeParsing()
        {
            AssertDBType("DB1.DBX0.0", S7InterfaceSignal.S7TYPE.BOOL, "DBX");
            AssertDBType("DB1.DBW4", S7InterfaceSignal.S7TYPE.WORD, "DBW");
            AssertDBType("DB1.DBB6", S7InterfaceSignal.S7TYPE.BYTE, "DBB");
            AssertDBType("DB1.DBD8", S7InterfaceSignal.S7TYPE.DWORD, "DBD");
            AssertDBType("DB100.DBX127.7", S7InterfaceSignal.S7TYPE.BOOL, "DBX large offsets");
        }

        private void TestFirstNumExtraction()
        {
            AssertFirstNum("E0.3", 0, 0, "E mempos");
            AssertFirstNum("E10.3", 0, 10, "E10 mempos");
            AssertFirstNum("MW10", 0, 10, "MW10 mempos");
            AssertFirstNum("MB5", 0, 5, "MB5 mempos");
            AssertFirstNum("MD200", 0, 200, "MD200 mempos");
            AssertFirstNum("DB100.DBW4", 0, 100, "DB number");
        }

        private void TestDBMemPosParsing()
        {
            AssertDBMemPos("DB1.DBW4", 4, "DBW4");
            AssertDBMemPos("DB1.DBX0.0", 0, "DBX0.0");
            AssertDBMemPos("DB1.DBX127.7", 127, "DBX127.7");
            AssertDBMemPos("DB1.DBD0", 0, "DBD0");
            AssertDBMemPos("DB1.DBD100", 100, "DBD100");
            AssertDBMemPos("DB5.DBB10", 10, "DBB10");
        }

        private void TestNumberAfterPoint()
        {
            // GetNumberAfterPoint - bit number after first dot
            AssertNumberAfterPoint("E0.3", 3, "E0.3 bit");
            AssertNumberAfterPoint("E10.7", 7, "E10.7 bit");
            AssertNumberAfterPoint("MW10", 0, "MW10 no dot");
            AssertNumberAfterPoint("M0.0", 0, "M0.0 bit 0");

            // GetNumberAfterLastPoint - bit number after last dot
            AssertNumberAfterLastPoint("DB1.DBX0.3", 3, "DBX bit from last dot");
            AssertNumberAfterLastPoint("DB1.DBX127.7", 7, "DBX bit 7");
            AssertNumberAfterLastPoint("MW10", 0, "MW10 no dot");
        }

        private void TestDirectionParsing()
        {
            // E/I = INPUT (Unity writes to PLC inputs)
            AssertDirection("E0.0", InterfaceSignal.DIRECTION.INPUT, "E input");
            AssertDirection("I0.0", InterfaceSignal.DIRECTION.INPUT, "I input");

            // A/Q = OUTPUT (Unity reads from PLC outputs)
            AssertDirection("A1.0", InterfaceSignal.DIRECTION.OUTPUT, "A output");
            AssertDirection("Q1.0", InterfaceSignal.DIRECTION.OUTPUT, "Q output");

            // M = OUTPUT (default, when not in AreaReadWriteMode)
            AssertDirection("M0.0", InterfaceSignal.DIRECTION.OUTPUT, "M output");
            AssertDirection("MW10", InterfaceSignal.DIRECTION.OUTPUT, "MW output");

            // DB = INPUTOUTPUT
            AssertDirection("DB1.DBX0.0", InterfaceSignal.DIRECTION.INPUTOUTPUT, "DB inputoutput");
            AssertDirection("DB1.DBW4", InterfaceSignal.DIRECTION.INPUTOUTPUT, "DB word inputoutput");
        }

        private void TestLengthParsing()
        {
            // GetLenght returns S7 WordLength constants, not byte counts
            // S7WLBit=0x01, S7WLByte=0x02, S7WLWord=0x04, S7WLDWord=0x06
            AssertLength("E0.3", S7Consts.S7WLBit, "BOOL -> WLBit");
            AssertLength("M0.0", S7Consts.S7WLBit, "M BOOL -> WLBit");
            AssertLength("MW10", S7Consts.S7WLWord, "WORD -> WLWord");
            AssertLength("MB5", S7Consts.S7WLByte, "BYTE -> WLByte");
            AssertLength("MD20", S7Consts.S7WLDWord, "DWORD -> WLDWord");
            AssertLength("DB1.DBX0.0", S7Consts.S7WLBit, "DBX -> WLBit");
            AssertLength("DB1.DBW4", S7Consts.S7WLWord, "DBW -> WLWord");
            AssertLength("DB1.DBB6", S7Consts.S7WLByte, "DBB -> WLByte");
            AssertLength("DB1.DBD8", S7Consts.S7WLDWord, "DBD -> WLDWord");
        }

        // --- Assert helpers ---

        private void AssertArea(string name, int expected, string desc)
        {
            int result = s7.GetArea(name);
            if (result != expected)
                error += $"Area '{desc}' ({name}): expected 0x{expected:X2}, got 0x{result:X2}; ";
        }

        private void AssertType(string name, S7InterfaceSignal.S7TYPE expected, string desc)
        {
            var result = S7Interface.GetTypeFromName(name);
            if (result != expected)
                error += $"Type '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        private void AssertDBType(string name, S7InterfaceSignal.S7TYPE expected, string desc)
        {
            var result = S7Interface.GetDBType(name);
            if (result != expected)
                error += $"DBType '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        private void AssertFirstNum(string name, int start, int expected, string desc)
        {
            int result = S7Interface.GetFirstNum(name, start);
            if (result != expected)
                error += $"FirstNum '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        private void AssertDBMemPos(string name, int expected, string desc)
        {
            int result = S7Interface.GetDBMemPos(name);
            if (result != expected)
                error += $"DBMemPos '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        private void AssertNumberAfterPoint(string name, int expected, string desc)
        {
            int result = s7.GetNumberAfterPoint(name);
            if (result != expected)
                error += $"NumberAfterPoint '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        private void AssertNumberAfterLastPoint(string name, int expected, string desc)
        {
            int result = s7.GetNumberAfterLastPoint(name);
            if (result != expected)
                error += $"NumberAfterLastPoint '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        private void AssertDirection(string name, InterfaceSignal.DIRECTION expected, string desc)
        {
            var result = s7.GetDirection(name);
            if (result != expected)
                error += $"Direction '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        private void AssertLength(string name, int expected, string desc)
        {
            int result = S7Interface.GetLenght(name);
            if (result != expected)
                error += $"Length '{desc}' ({name}): expected {expected}, got {result}; ";
        }

        protected override string ValidateResults()
        {
            return error;
        }
    }
}
