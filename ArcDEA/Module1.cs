using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcDEA.Classes;

namespace ArcDEA
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("ArcDEA_Module");

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //return false to ~cancel~ Application close
            return true;
        }


        protected override bool Initialize()
        {
            // Register GDAL and OGR via custom initialiser
            Helpers.CustomGdalConfigure();

            //// TODO: this is likely easier to do some other way, but we need proj.db for osr to work either way...
            ///// todo: clean this up
            var installFolder = System.Reflection.Assembly.GetEntryAssembly().Location.ToString();
            installFolder = System.IO.Path.GetFullPath(System.IO.Path.Combine(installFolder, @"..\..\"));
            OSGeo.OSR.Osr.SetPROJSearchPath(System.IO.Path.Combine(installFolder, @"Resources\pedata\gdaldata"));

            //// Set optimal GDAL configurations
            OSGeo.GDAL.Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
            OSGeo.GDAL.Gdal.SetConfigOption("CPL_VSIL_CURL_ALLOWED_EXTENSIONS", "tif");
            OSGeo.GDAL.Gdal.SetConfigOption("GDAL_HTTP_MULTIRANGE", "YES");
            OSGeo.GDAL.Gdal.SetConfigOption("GDAL_HTTP_MERGE_CONSECUTIVE_RANGES", "YES");

            return true;
        }


        #endregion Overrides
    }
}
