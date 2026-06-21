// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

// Canonical MCP param attribute stub in realvirtual.base.
// Any assembly referencing realvirtual.base can use [McpParam] on method parameters.
// McpToolRegistry discovers them via string-based FullName matching.
// IMPORTANT: Assemblies must NOT also reference io.realvirtual.mcp to avoid CS0433.
// The MCP package keeps its own independent copy (it doesn't reference realvirtual.base).
using System;

namespace realvirtual.MCP
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class McpParamAttribute : Attribute
    {
        public string Description { get; }

        public McpParamAttribute(string description)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}
