#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Linq;
using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies that fields inherited from base classes are included in serialization
    public class TestMcpInheritedFields : FeatureTestBase
    {
        protected override string TestName => "MCP Inherited Fields Included in Serialization";
        

        private Drive testDrive;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestDrive");
            testDrive = go.AddComponent<Drive>();
            testDrive.TargetSpeed = 300f;
        }

        protected override string ValidateResults()
        {
            var reflData = ReflectionCache.GetReflectionData(typeof(Drive));
            if (reflData == null)
                return "ReflectionCache returned null for Drive";

            // TargetSpeed should be found (may be inherited from BaseDrive or direct)
            bool hasTargetSpeed = reflData.SerializableFields.Any(f => f.Name == "TargetSpeed");
            if (!hasTargetSpeed)
                return "TargetSpeed field not found in ReflectionCache";

            // Serialize and verify it appears in JSON
            var json = ComponentSerializer.Serialize(testDrive);
            if (json["TargetSpeed"] == null)
                return "TargetSpeed missing from serialized JSON";

            return "";
        }
    }
}

#endif
