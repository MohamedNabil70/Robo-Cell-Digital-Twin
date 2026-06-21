// ZipUtil - Legacy wrapper for backward compatibility
// Delegates to ZipHelper to keep Ionic.Zip references isolated

namespace NaughtyAttributes
{
	public static class ZipUtil
	{
		/// <summary>
		/// Extracts a ZIP file to the specified location.
		/// </summary>
		/// <param name="zipFilePath">Path to the ZIP file</param>
		/// <param name="location">Destination directory</param>
		public static void Unzip(string zipFilePath, string location)
		{
#if REALVIRTUAL_ZIP
			// Delegate to ZipHelper to keep Ionic.Zip references isolated
			realvirtual.ZipHelper.ExtractZip(zipFilePath, location);
#else
			throw new System.NotSupportedException("ZIP functionality requires REALVIRTUAL_ZIP scripting define.");
#endif
		}
	}
}
