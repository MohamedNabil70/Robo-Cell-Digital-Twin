#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies McpTypeResolver resolves component type names correctly across assemblies
    public class TestMcpTypeResolver : FeatureTestBase
    {
        protected override string TestName => "MCP TypeResolver Resolves Component Types";

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
        }

        protected override string ValidateResults()
        {
            // Known type should resolve
            var driveType = McpTypeResolver.Resolve("Drive");
            if (driveType == null)
                return "McpTypeResolver could not resolve 'Drive'";
            if (!typeof(MonoBehaviour).IsAssignableFrom(driveType))
                return $"Resolved type is not MonoBehaviour: {driveType.FullName}";

            // Sensor should resolve
            var sensorType = McpTypeResolver.Resolve("Sensor");
            if (sensorType == null)
                return "McpTypeResolver could not resolve 'Sensor'";

            // Unknown type should return null (not throw)
            var unknownType = McpTypeResolver.Resolve("NonExistentComponent12345");
            if (unknownType != null)
                return "McpTypeResolver should return null for unknown types";

            return "";
        }
    }
}

#endif
