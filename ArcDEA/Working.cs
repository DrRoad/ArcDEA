using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArcDEA
{
    internal class Working : Button
    {
        protected override async void OnClick()
        {
            //await QueuedTask.Run(() =>
            //{
                // Get raster path and filename and convert to URI
                //string file = @"C:\Users\262272G\Desktop";

                // Create a FileSystemConnectionPath using the folder path.
                //FileSystemConnectionPath connectionPath = new FileSystemConnectionPath(new System.Uri(file), FileSystemDatastoreType.Raster);

                // Create a new FileSystemDatastore using the FileSystemConnectionPath.
                //FileSystemDatastore dataStore = new FileSystemDatastore(connectionPath);

                // Open the raster dataset.
                //RasterDataset fileRasterDataset = dataStore.OpenDataset<RasterDataset>("my_tif.tif");

                // Get the RasterDatasetDefinition from the raster dataset.
                //RasterDatasetDefinition rasterDatasetDefinition = fileRasterDataset.GetDefinition();

            //    // Get the number of bands from the raster band definition.
            //    int bandCount = rasterDatasetDefinition.GetBandCount();

            //    // Get a RasterBand from the raster dataset.
            //    RasterBand rasterBand = fileRasterDataset.GetBand(6);

            //    // Get the RasterBandDefinition from the raster band.
            //    RasterBandDefinition rasterBandDefinition = rasterBand.GetDefinition();

            //    // Get the name of the raster band from the raster band.
            //    string bandName = rasterBandDefinition.GetName();

            //    // Create a full raster from the raster dataset.
            //    Raster raster = fileRasterDataset.CreateFullRaster();

            //    // Calculate size of pixel block to create
            //    int pixelBlockHeight = raster.GetHeight();
            //    int pixelBlockWidth = raster.GetWidth();

            //    // Create a new (blank) pixel block.
            //    PixelBlock currentPixelBlock = raster.CreatePixelBlock(pixelBlockWidth, pixelBlockHeight);

            //    // Read pixel values from the raster dataset into the pixel block starting from the given top left corner.
            //    raster.Read(0, 0, currentPixelBlock);

            //    // create a container to hold the pixel values
            //    Array pixelArray = new object[currentPixelBlock.GetWidth(), currentPixelBlock.GetHeight()];

            //    // retrieve the actual pixel values from the pixelblock representing the red raster band
            //    pixelArray = currentPixelBlock.GetPixelData(6, false);
            //});


            // // // //
            // Get available dates for current bbox
            // // // //

            // Set url.
            string url = "https://explorer.sandbox.dea.ga.gov.au/stac/search?collection=ga_ls8c_ard_3&time=2013-01-01/2015-12-13&bbox=[118.9665,-22.7786,119.1555,-22.6962]&limit=500";

            // Create a connection to the WMS server
            //var serverConnection = new CIMInternetServerConnection {URL = url};
            //var connection = new CIMWMSServiceConnection {ServerConnection = serverConnection};

            WebRequest req = WebRequest.Create(url);
            Stream resultStream = req.GetResponse().GetResponseStream();

            StreamReader resultReader = new StreamReader(resultStream);
            string output = resultReader.ReadToEnd();

            JsonDocument doc = JsonDocument.Parse(output);
            JsonElement root = doc.RootElement;
            JsonElement features = root.GetProperty("features");

            List<string> dates = new List<string>();

            foreach (var feat in features.EnumerateArray())
            {
                string id = feat.GetProperty("id").ToString();
                string datetime = feat.GetProperty("properties").GetProperty("datetime").ToString();
                string platform = feat.GetProperty("properties").GetProperty("platform").ToString();
                string geometry = feat.GetProperty("geometry").ToString();

                string date = datetime.Split("T")[0];

                if (!dates.Contains(date))
                {
                    dates.Add(date);
                }
            }


            // // // //
            // Reduce dates to only cloud-free dates
            // // // //

            double minPctValid = 0.9;
            string out_folder = Path.GetTempPath();

            List<string> cleanDates = new List<string>();

            foreach (string date in dates)
            {
                string wcsUrl = "";
                wcsUrl += "https://ows.dea.ga.gov.au/wcs?service=WCS";
                wcsUrl += "&VERSION=1.0.0";
                wcsUrl += "&REQUEST=GetCoverage";
                wcsUrl += "&COVERAGE=ga_ls8c_ard_3";
                wcsUrl += "&TIME=" + date;
                wcsUrl += "&MEASUREMENTS=oa_fmask";
                wcsUrl += "&BBOX=-1323766.7364,-2521531.7166,-1305606.1361,-2510422.3795";
                wcsUrl += "&CRS=EPSG:3577";
                wcsUrl += "&RESX=30.0";
                wcsUrl += "&RESY=30.0";
                wcsUrl += "&FORMAT=GeoTIFF";

                string out_file = "_" + date.Replace("-", "") + ".tif";  // todo do the image read in memory below to prevent saving to disk

                using (var client = new WebClient())
                {
                    System.Diagnostics.Debug.WriteLine("Downloading: " + Path.Join(out_folder, out_file));
                    client.DownloadFile(wcsUrl, Path.Join(out_folder, out_file));

                    Uri uri = new Uri(out_folder);

                    FileSystemConnectionPath conn = new FileSystemConnectionPath(uri, FileSystemDatastoreType.Raster);

                    var pctValid = await QueuedTask.Run(() => {
                        
                        FileSystemDatastore dstore = new FileSystemDatastore(conn);
                        RasterDataset rasterDataset = dstore.OpenDataset<RasterDataset>("_" + date.Replace("-", "") + ".tif");
                        Raster raster = rasterDataset.CreateFullRaster();

                        int width = raster.GetWidth();
                        int height = raster.GetHeight();
                        var block = raster.CreatePixelBlock(width, height);

                        raster.Read(0, 0, block);

                        Array array = block.GetPixelData(0, false);

                        List<int> validClasses = new List<int> { 1, 4, 5 };
                        int validCount = 0;

                        for (int i = 0; i < width; i++)
                        {
                            for (int j = 0; j < height; j++)
                            {
                                int pixelValue = Convert.ToInt16(block.GetValue(0, i, j));

                                if (validClasses.Contains(pixelValue))
                                {
                                    validCount += 1;
                                }
                            }
                        }

                        return validCount / array.Length;
                    });

                    if (pctValid > minPctValid)
                    {
                        cleanDates.Add(date);
                    }

                    File.Delete(out_file);
                }
            }

            System.Diagnostics.Debug.WriteLine("Finished getting cloud free dates.");


            // // // //
            // Download cloud-free scenes
            // // // //

            out_folder = @"C:\Users\262272G\Desktop\test";

            foreach (string date in cleanDates)
            {
                string wcsUrl = "";
                wcsUrl += "https://ows.dea.ga.gov.au/wcs?service=WCS";
                wcsUrl += "&VERSION=1.0.0";
                wcsUrl += "&REQUEST=GetCoverage";
                wcsUrl += "&COVERAGE=ga_ls8c_ard_3";
                wcsUrl += "&TIME=" + date;
                wcsUrl += "&MEASUREMENTS=nbart_blue,nbart_green,nbart_red,oa_fmask";
                wcsUrl += "&BBOX=-1323766.7364,-2521531.7166,-1305606.1361,-2510422.3795";
                wcsUrl += "&CRS=EPSG:3577";
                wcsUrl += "&RESX=30.0";
                wcsUrl += "&RESY=30.0";
                wcsUrl += "&FORMAT=GeoTIFF";

                string out_file = "_" + date.Replace("-", "") + ".tif";  // todo do the image read in memory below to prevent saving to disk

                using (var client = new WebClient())
                {
                    System.Diagnostics.Debug.WriteLine("Downloading: " + Path.Join(out_folder, out_file));
                    client.DownloadFile(wcsUrl, Path.Join(out_folder, out_file));
                }
            }
        }
    }
}
