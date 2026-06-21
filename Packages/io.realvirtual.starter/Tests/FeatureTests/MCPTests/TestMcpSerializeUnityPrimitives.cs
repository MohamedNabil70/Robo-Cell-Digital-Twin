#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Linq;
using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies ReflectionCache categorizes fields correctly and Unity primitive types serialize without circular reference errors
    public class TestMcpSerializeUnityPrimitives : FeatureTestBase
    {
        protected override string TestName => "MCP Serialize Unity Primitive Types";
        

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestDrive");
            var drive = go.AddComponent<Drive>();
            drive.TargetSpeed = 123.4f;

            // Serialize should not throw (circular reference protection)
            var json = ComponentSerializer.Serialize(drive);
            LogTest($"Serialized Drive JSON has {json.Properties().Count()} properties");
        }

        protected override string ValidateResults()
        {
            var reflData = ReflectionCache.GetReflectionData(typeof(Drive));
            if (reflData == null)
                return "ReflectionCache returned null for Drive type";
            if (reflData.SerializableFields.Length == 0)
                return "ReflectionCache found 0 serializable fields on Drive";

            bool hasPrimitive = reflData.SerializableFields.Any(f => f.Category == FieldCategory.Primitive);
            if (!hasPrimitive)
                return "No Primitive fields found on Drive";

            return "";
        }
    }
}

#endif
