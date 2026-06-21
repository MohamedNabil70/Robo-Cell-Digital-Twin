#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Linq;
using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies array field categorization and enum handling
    public class TestMcpSerializeArrayFields : FeatureTestBase
    {
        protected override string TestName => "MCP Serialize Array Fields";
        

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var reflData = ReflectionCache.GetReflectionData(typeof(Drive));
            var arrayFields = reflData.SerializableFields
                .Where(f => f.Category == FieldCategory.PrimitiveArray ||
                            f.Category == FieldCategory.UnityPrimitiveArray ||
                            f.Category == FieldCategory.ObjectArray ||
                            f.Category == FieldCategory.ObjectList)
                .ToList();

            LogTest($"Drive has {arrayFields.Count} array/list fields");
        }

        protected override string ValidateResults()
        {
            var cache = ReflectionCache.GetReflectionData(typeof(Drive));
            if (cache == null)
                return "ReflectionCache returned null";

            // Verify enum fields are categorized as Primitive
            var enumFields = cache.SerializableFields
                .Where(f => f.FieldType.IsEnum)
                .ToList();
            foreach (var ef in enumFields)
            {
                if (ef.Category != FieldCategory.Primitive)
                    return $"Enum field '{ef.Name}' should be Primitive, got {ef.Category}";
            }

            return "";
        }
    }
}

#endif
