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
using System.Windows.Media.Imaging;

namespace ArcDEA
{
    internal class Working : Button
    {
        protected override async void OnClick()
        {
            // QueuedTask.Run (() => {}); is used to run something in background to kepe UI responsive
            // For above, prepend await to ensure a QueuedTask finished before moving to next function
            // When you use await, you need to use the async modifier in function defitinition

            // update this to net6 https://www.hanselman.com/blog/parallelforeachasync-in-net-6


            // // // //
            // Setup globals
            // // // //

            string outFolder = Path.GetTempPath();
            string maskFolder = Path.Join(outFolder, Guid.NewGuid().ToString());

            Directory.CreateDirectory(maskFolder);

            string url = "https://explorer.sandbox.dea.ga.gov.au/stac/search?collection=ga_ls8c_ard_3&time=2014-01-01/2014-12-13&bbox=[119.7269,-22.9444,121.1253,-21.9084]&limit=500";


            // // // //
            // Get available dates for current bbox
            // // // //

            JsonElement features = await QueuedTask.Run(() =>
            {
                WebRequest req = WebRequest.Create(url);
                Stream resultStream = req.GetResponse().GetResponseStream();

                StreamReader resultReader = new StreamReader(resultStream);
                string output = resultReader.ReadToEnd();

                JsonDocument doc = JsonDocument.Parse(output);
                JsonElement root = doc.RootElement;

                //JsonElement features = root.GetProperty("features");
                return root.GetProperty("features");
            });

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
            // Create list of http wcs requests
            // // // //

            List<List<string>> wcsData = new List<List<string>>();
            foreach (string date in dates)
            {
                string outFile = "_" + date.Replace("-", "") + ".tif";

                string wcsUrl = "";
                wcsUrl += "https://ows.dea.ga.gov.au/wcs?service=WCS";
                wcsUrl += "&VERSION=1.0.0";
                wcsUrl += "&REQUEST=GetCoverage";
                wcsUrl += "&COVERAGE=ga_ls8c_ard_3";
                wcsUrl += "&TIME=" + date;
                wcsUrl += "&MEASUREMENTS=oa_fmask";
                wcsUrl += "&BBOX=-1244996.6303,-2532323.9596,-1113352.8067,-2404162.9812";
                wcsUrl += "&CRS=EPSG:3577";
                wcsUrl += "&RESX=30.0";
                wcsUrl += "&RESY=30.0";
                wcsUrl += "&FORMAT=GeoTIFF";

                wcsData.Add(new List<string> { date, wcsUrl, maskFolder, outFile });
            };


            // // // //
            // Download all fmask tifs to mask folder in temp directory
            // // // //

            Dictionary<string, string> downloadedMasks = new Dictionary<string, string>();

            var client = new HttpClient();

            async Task DownloadGeoTiffs(List<string> data)
            {
                System.Diagnostics.Debug.WriteLine(data[0]);

                string outFile = Path.Join(data[2], data[3]);

                var response = await client.GetAsync(data[1]);
                HttpContent content = response.Content;

                var stream = await content.ReadAsStreamAsync();
                var file = new FileStream(outFile, FileMode.CreateNew);

                await stream.CopyToAsync(file);

                downloadedMasks.Add(data[0], data[3]);
            };

            var tasks = wcsData.Select(e => DownloadGeoTiffs(e)).ToList();
            await Task.WhenAll(tasks);



            // // // //
            // Extract mask values for determine valid, cloud free dates
            // // // //

            float minPctValid = 0.9F;
            List<int> validClasses = new List<int> { 1, 4, 5 };

            Uri uri = new Uri(maskFolder);

            List<string> cleanDates = new List<string>();
            foreach (var item in downloadedMasks)
            {
                System.Diagnostics.Debug.WriteLine("Working on mask: " + item.Key);

                await QueuedTask.Run(() =>
                {
                    FileSystemConnectionPath conn = new FileSystemConnectionPath(uri, FileSystemDatastoreType.Raster);
                    FileSystemDatastore dstore = new FileSystemDatastore(conn);
                    RasterDataset rasterDataset = dstore.OpenDataset<RasterDataset>(item.Value);
                    Raster raster = rasterDataset.CreateFullRaster();

                    int width = raster.GetWidth();
                    int height = raster.GetHeight();

                    PixelBlock block = raster.CreatePixelBlock(width, height);
                    raster.Read(0, 0, block);

                    Array rawPixelArray = block.GetPixelData(0, false);

                    byte[,] bytesPixelArray2d = (byte[,])rawPixelArray;
                    byte[] bytesPixelArray1d = new byte[bytesPixelArray2d.Length];
                    Buffer.BlockCopy(bytesPixelArray2d, 0, bytesPixelArray1d, 0, bytesPixelArray2d.Length);

                    long fullSize = bytesPixelArray2d.Length;
                    long validSize = bytesPixelArray1d.Where(e => validClasses.Contains(e)).ToArray().Length;

                    if (((float)validSize / (float)fullSize) > minPctValid)
                    {
                        cleanDates.Add(item.Key);
                    }
                });
            };

            cleanDates.Sort();


            // // // //
            // Delete all mask files
            // // // //

            // todo
            //File.Delete(item.Value);


            // // // //
            // Create list of http wcs requests
            // // // //

            string testingFolder = @"C:\Users\262272G\Desktop\test";

            List<List<string>> wcsCleanData = new List<List<string>>();
            foreach (string date in cleanDates)
            {
                string outFile = "_" + date.Replace("-", "") + ".tif";

                string wcsUrl = "";
                wcsUrl += "https://ows.dea.ga.gov.au/wcs?service=WCS";
                wcsUrl += "&VERSION=1.0.0";
                wcsUrl += "&REQUEST=GetCoverage";
                wcsUrl += "&COVERAGE=ga_ls8c_ard_3";
                wcsUrl += "&TIME=" + date;
                wcsUrl += "&MEASUREMENTS=nbart_blue,nbart_green,nbart_red,nbart_nir,oa_fmask";
                wcsUrl += "&BBOX=-1244996.6303,-2532323.9596,-1113352.8067,-2404162.9812";
                wcsUrl += "&CRS=EPSG:3577";
                wcsUrl += "&RESX=30.0";
                wcsUrl += "&RESY=30.0";
                wcsUrl += "&FORMAT=GeoTIFF";

                wcsCleanData.Add(new List<string> { date, wcsUrl, testingFolder, outFile });
            };


            // // // //
            // Download all fmask tifs to mask folder in temp directory
            // // // //

            System.Diagnostics.Debug.WriteLine("Starting downloading of cloud free scenes.");

            //Dictionary<string, string> downloadedScenes = new Dictionary<string, string>();

            async Task DownloadScenes(List<string> data)
            {
                System.Diagnostics.Debug.WriteLine(data[0]);

                string outFile = Path.Join(data[2], data[3]);

                var response = await client.GetAsync(data[1]);
                HttpContent content = response.Content;

                var stream = await content.ReadAsStreamAsync();
                var file = new FileStream(outFile, FileMode.CreateNew);

                await stream.CopyToAsync(file);

                //downloadedScenes.Add(data[0], data[3]);
            };

            var tasksClean = wcsCleanData.Select(e => DownloadScenes(e)).ToList();
            await Task.WhenAll(tasksClean);

            System.Diagnostics.Debug.WriteLine("Process complete.");
        }
    }
}
