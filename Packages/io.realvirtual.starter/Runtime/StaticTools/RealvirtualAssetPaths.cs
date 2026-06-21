// realvirtual.io (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019-2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/license-conditions/

namespace realvirtual
{
    /// <summary>
    /// Provides package path constants and helper methods for UPM packages.
    /// All paths use direct Packages/io.realvirtual.* paths which work both in development
    /// and when installed via Package Manager.
    /// </summary>
    public static class RealvirtualAssetPaths
    {
        public const string StarterPackage = "Packages/io.realvirtual.starter";
        public const string ProfessionalPackage = "Packages/io.realvirtual.professional";

        // Helper methods for Starter package paths
        public static string StarterEditor(string relativePath)
            => $"{StarterPackage}/Editor/{relativePath}";

        public static string StarterEditorAssets(string relativePath)
            => $"{StarterPackage}/Editor/EditorAssets/{relativePath}";

        public static string StarterRuntime(string relativePath)
            => $"{StarterPackage}/Runtime/{relativePath}";

        public static string StarterResources(string relativePath)
            => $"{StarterPackage}/Runtime/Resources/{relativePath}";

        // Helper methods for Professional package paths
        public static string ProfessionalEditor(string relativePath)
            => $"{ProfessionalPackage}/Editor/{relativePath}";

        public static string ProfessionalRuntime(string relativePath)
            => $"{ProfessionalPackage}/Runtime/{relativePath}";

        public static string ProfessionalResources(string relativePath)
            => $"{ProfessionalPackage}/Runtime/Resources/{relativePath}";
    }
}
