// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

// ZipHelper - Wrapper for Ionic.Zip functionality
// This class isolates Ionic.Zip usage to prevent the DLL from being loaded at Editor startup.
// REALVIRTUAL_ZIP must be defined for this code to compile. This allows the DLL to be packaged during UPM export.

#if REALVIRTUAL_ZIP
using Ionic.Zip;
#endif

namespace realvirtual
{
    /// <summary>
    /// Helper class for ZIP operations using Ionic.Zip library.
    /// Requires REALVIRTUAL_ZIP scripting define to be enabled.
    /// </summary>
    public static class ZipHelper
    {
        /// <summary>
        /// Creates a ZIP file from a project directory, excluding Library and Temp folders.
        /// </summary>
        /// <param name="projectPath">The root project path to zip</param>
        /// <param name="outputPath">The output ZIP file path</param>
        public static void CreateProjectZip(string projectPath, string outputPath)
        {
#if REALVIRTUAL_ZIP
            using (ZipFile zip = new ZipFile())
            {
                zip.AddDirectory(projectPath);
                zip.RemoveSelectedEntries("Library/*");
                zip.RemoveSelectedEntries("Temp/*");
                zip.Save(outputPath);
            }
#else
            throw new System.NotSupportedException("ZIP functionality requires REALVIRTUAL_ZIP scripting define.");
#endif
        }

        /// <summary>
        /// Creates a ZIP file from a directory.
        /// </summary>
        /// <param name="directoryPath">The directory to zip</param>
        /// <param name="outputPath">The output ZIP file path</param>
        public static void CreateZipFromDirectory(string directoryPath, string outputPath)
        {
#if REALVIRTUAL_ZIP
            using (ZipFile zip = new ZipFile())
            {
                zip.AddDirectory(directoryPath);
                zip.Save(outputPath);
            }
#else
            throw new System.NotSupportedException("ZIP functionality requires REALVIRTUAL_ZIP scripting define.");
#endif
        }

        /// <summary>
        /// Extracts a ZIP file to a specified location.
        /// </summary>
        /// <param name="zipFilePath">The ZIP file to extract</param>
        /// <param name="extractPath">The destination path</param>
        public static void ExtractZip(string zipFilePath, string extractPath)
        {
#if REALVIRTUAL_ZIP
            System.IO.Directory.CreateDirectory(extractPath);
            using (ZipFile zip = ZipFile.Read(zipFilePath))
            {
                zip.ExtractAll(extractPath, ExtractExistingFileAction.OverwriteSilently);
            }
#else
            throw new System.NotSupportedException("ZIP functionality requires REALVIRTUAL_ZIP scripting define.");
#endif
        }
    }
}
