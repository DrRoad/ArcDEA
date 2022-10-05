using ArcGIS.Core.Data.Raster;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArcDEA.Classes
{
    internal class Data
    {
        public class DownloadItem
        {
            public string Id { get; set; }
            public DateTime DateTime { get; set; }
            public string Date { get; set; }
            public string Time { get; set; }
            public string Platform { get; set; }
            public string Product { get; set; }
            public List<string> AvailableAssets { get; set; }
            public List<string> RequestedAssets { get; set; }
            public Dictionary<string, string> Urls { get; set; }
            public Dictionary<string, string> Files { get; set; }
            public string FullWcsUrl { get; set; }
            public string MaskWcsUrl { get; set; }
            public string FullFilename { get; set; }
            public string MaskFilename { get; set; }
            public string FinalFilename { get; set; }
            public bool Valid { get; set; } = false;
            public bool Error { get; set; } = false;

            public async Task SetValidViaWcsMaskAsync(float minValid, List<int> validPixels, HttpClient client)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        // Open mask WCS url and extract first band
                        OSGeo.GDAL.Dataset dataset = OSGeo.GDAL.Gdal.Open(MaskWcsUrl, OSGeo.GDAL.Access.GA_ReadOnly);
                        OSGeo.GDAL.Band band = dataset.GetRasterBand(1);

                        // Extract band dimensions
                        int width = band.XSize;
                        int height = band.YSize;
                        int size = width * height;

                        // Read pixel values into "block" array
                        Int16[] bits = new Int16[size];
                        band.ReadRaster(0, 0, width, height, bits, width, height, 0, 0);

                        // Get distinct values and their counts
                        var pixelCounts = bits.GroupBy(e => e).Select(x => new { Key = x.Key, Value = x.Count() });

                        // Get number of valid, overlap and total pixels
                        float numValid = pixelCounts.Where(e => validPixels.Contains(e.Key)).Sum(e => e.Value);
                        float numOverlap = pixelCounts.Where(e => e.Key == 0).Sum(e => e.Value);
                        float numTotal = size;

                        // Calculate percentages
                        float pctValid = numValid / (numTotal - numOverlap);
                        float pctOverlap = numOverlap / numTotal;

                        // Clean up percentages in case of nan
                        pctValid = pctValid >= 0 ? pctValid : 0;
                        pctOverlap = pctOverlap >= 0 ? pctOverlap : 0;

                        // Flag item as valid (default is false)
                        if (pctOverlap < 1.0 && pctValid >= minValid)
                        {
                            Valid = true;
                        }

                        // Close band and dataset
                        band.Dispose();
                        dataset.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "HTTP error code : 400")
                        {
                            Error = true;
                        }
                        else if (ex.Message == "HTTP error code : 502")
                        {
                            Error = true;
                        }
                        else
                        {
                            string huh = null;
                        };
                    }

                    // Take a breath...
                    Task.Delay(100);
                });
            }

            public async Task DownloadAndProcessViaFullAsync(string outputFolder, List<int> validPixels, Int16 noDataValue, HttpClient client)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        // Create full output path and filename
                        string outTmp = Path.Combine(outputFolder, "tmp", "_", FinalFilename);
                        string outputFile = Path.Combine(outputFolder, FinalFilename);

                        // Download and read WCS full source raster (including mask band)
                        OSGeo.GDAL.Dataset sourceDS = OSGeo.GDAL.Gdal.Open(FullWcsUrl, OSGeo.GDAL.Access.GA_ReadOnly);

                        // TESTING: we cna use vrts to do warps, projection, resize, posisbly nodata values, possibly calculations!
                        // May want to save geotiff to temp folder, create vrt, warp, then save to final output folder to prevent x2 download
                        OSGeo.GDAL.GDALWarpAppOptions options = new OSGeo.GDAL.GDALWarpAppOptions(new[] 
                        { 
                            "-t_srs", "EPSG:4326",
                            //"-tr", "120", "120",
                            //"-r", "near",
                            //"-multi"
                        });
                        OSGeo.GDAL.Dataset outputDS = OSGeo.GDAL.Gdal.Warp("tmp.tif", new OSGeo.GDAL.Dataset[] { sourceDS }, options, null, null);


                        //OSGeo.GDAL.Driver driverTmp = OSGeo.GDAL.Gdal.GetDriverByName("GTiff");
                        //OSGeo.GDAL.Dataset sourceTmp = driverTmp.CreateCopy(outTmp, sourceDS, 0, null, null, null);

                        // Get source raster dimensions
                        int width = sourceDS.RasterXSize;
                        int height = sourceDS.RasterYSize;

                        // Get source raster transforms
                        double[] geoTransform = new double[6];
                        sourceDS.GetGeoTransform(geoTransform);

                        // Get source raster fmask band and all other source bands as list
                        OSGeo.GDAL.Band sourceMaskBand = null;
                        var sourceBands = new List<OSGeo.GDAL.Band>();
                        for (int b = 1; b <= sourceDS.RasterCount; b++)
                        {
                            if (sourceDS.GetRasterBand(b).GetDescription() == "oa_fmask")
                            {
                                sourceMaskBand = sourceDS.GetRasterBand(b);
                            }
                            else
                            {
                                sourceBands.Add(sourceDS.GetRasterBand(b));
                            }
                        }

                        // Setup destination dataset
                        OSGeo.GDAL.Driver driver = OSGeo.GDAL.Gdal.GetDriverByName("GTiff");
                        OSGeo.GDAL.Dataset destDS = driver.Create(outputFile, width, height, sourceBands.Count, OSGeo.GDAL.DataType.GDT_Int16, null);

                        // Set desintation transforms
                        destDS.SetGeoTransform(geoTransform);
                        destDS.SetProjection(sourceDS.GetProjection());

                        // Get destination bands as list
                        var destBands = new List<OSGeo.GDAL.Band>();
                        for (int b = 1; b <= destDS.RasterCount; b++)
                        {
                            destBands.Add(destDS.GetRasterBand(b));
                        }

                        // Iterate through source pixels (per row) and fill invalid pixels with nodata value
                        for (int h = 0; h < height; h++)
                        {
                            Int16[] maskBits = new Int16[width];
                            sourceMaskBand.ReadRaster(0, h, width, 1, maskBits, width, 1, 0, 0);

                            var sourceBandBits = new List<Int16[]>();
                            for (int i = 0; i < sourceBands.Count; i++)
                            {
                                var bits = new Int16[width];
                                sourceBands[i].ReadRaster(0, h, width, 1, bits, width, 1, 0, 0);
                                sourceBandBits.Add(bits);
                            }

                            for (int w = 0; w < width; w++)
                            {
                                if (!validPixels.Contains(maskBits[w]))
                                {
                                    foreach (var arr in sourceBandBits)
                                    {
                                        arr[w] = Convert.ToInt16(noDataValue);
                                    }
                                }
                            }

                            for (int i = 0; i < sourceBands.Count; i++)
                            {
                                destBands[i].WriteRaster(0, h, width, 1, sourceBandBits[i], width, 1, 0, 0);
                                destBands[i].SetNoDataValue(Convert.ToInt16(noDataValue));
                            }
                        }

                        // flush (i.e., save) the destintion geotiff
                        destDS.FlushCache();

                        // Close band and dataset
                        destDS.Dispose();
                        sourceDS.Dispose();

                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "HTTP error code : 400")
                        {
                            Error = true;
                        }
                        else if (ex.Message == "HTTP error code : 502")
                        {
                            Error = true;
                        }
                        else 
                        {
                            string huh = null;
                        };
                    }

                    // Take a breath...
                    Task.Delay(100);
                });
            }

            public async Task DownloadAndProcessViaIndexAsync(string index, string outputFolder, List<int> validPixels, Int16 noDataValue, HttpClient client)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        // Create full output path and filename
                        string outputFile = Path.Combine(outputFolder, FinalFilename);

                        // Download and read WCS full source raster (including mask band)
                        OSGeo.GDAL.Dataset sourceDS = OSGeo.GDAL.Gdal.Open(FullWcsUrl, OSGeo.GDAL.Access.GA_ReadOnly);

                        // Get source raster dimensions
                        int width = sourceDS.RasterXSize;
                        int height = sourceDS.RasterYSize;

                        // Get source raster transforms
                        double[] geoTransform = new double[6];
                        sourceDS.GetGeoTransform(geoTransform);

                        // Setup destination dataset
                        OSGeo.GDAL.Driver driver = OSGeo.GDAL.Gdal.GetDriverByName("GTiff");
                        OSGeo.GDAL.Dataset destDS = driver.Create(outputFile, width, height, 1, OSGeo.GDAL.DataType.GDT_Float32, null);

                        // Set desintation transforms
                        destDS.SetGeoTransform(geoTransform);
                        destDS.SetProjection(sourceDS.GetProjection());

                        // Get destination single index band
                        OSGeo.GDAL.Band destBand = destDS.GetRasterBand(1);

                        // TODO: make this more dynamic
                        //

                        // TODO: slc-off included results in rgb swir bands (but not nir)
                        // being -999, resulting in odd ndvi outputs. Might need extra 
                        // check, i.e. if < 0 set to 0

                        // Process index based on user selection
                        if (index.ToLower() == "ndvi")
                        {
                            OSGeo.GDAL.Band sourceRedBand = null;
                            OSGeo.GDAL.Band sourceNirBand = null;
                            
                            for (int b = 1; b <= sourceDS.RasterCount; b++)
                            {
                                if (sourceDS.GetRasterBand(b).GetDescription() == "nbart_red")
                                {
                                    sourceRedBand = sourceDS.GetRasterBand(b);
                                }
                                else if (sourceDS.GetRasterBand(b).GetDescription() == "nbart_nir")
                                {
                                    sourceNirBand = sourceDS.GetRasterBand(b);
                                }
                            }

                            for (int h = 0; h < height; h++)
                            {
                                Single[] idxBits = new Single[width];
                                Single[] redBits = new Single[width];
                                Single[] nirBits = new Single[width];

                                sourceRedBand.ReadRaster(0, h, width, 1, redBits, width, 1, 0, 0);
                                sourceNirBand.ReadRaster(0, h, width, 1, nirBits, width, 1, 0, 0);

                                for (int w = 0; w < width; w++)
                                {
                                    Single value = (nirBits[w] - redBits[w]) / (nirBits[w] + redBits[w]);
                                    idxBits[w] = Convert.ToSingle(value);
                                }

                                destBand.WriteRaster(0, h, width, 1, idxBits, width, 1, 0, 0);
                            }
                        }
                        else if (index.ToLower() == "slavi")
                        {
                            OSGeo.GDAL.Band sourceRedBand = null;
                            OSGeo.GDAL.Band sourceNirBand = null;
                            OSGeo.GDAL.Band sourceSwir2Band = null;
                            for (int b = 1; b <= sourceDS.RasterCount; b++)
                            {
                                if (sourceDS.GetRasterBand(b).GetDescription() == "nbart_red")
                                {
                                    sourceRedBand = sourceDS.GetRasterBand(b);
                                }
                                else if (sourceDS.GetRasterBand(b).GetDescription() == "nbart_nir")
                                {
                                    sourceNirBand = sourceDS.GetRasterBand(b);
                                }
                                else if (sourceDS.GetRasterBand(b).GetDescription() == "nbart_swir_2")
                                {
                                    sourceSwir2Band = sourceDS.GetRasterBand(b);
                                }
                            }

                            for (int h = 0; h < height; h++)
                            {
                                Single[] idxBits = new Single[width];
                                Single[] redBits = new Single[width];
                                Single[] nirBits = new Single[width];
                                Single[] swir2Bits = new Single[width];

                                sourceRedBand.ReadRaster(0, h, width, 1, redBits, width, 1, 0, 0);
                                sourceNirBand.ReadRaster(0, h, width, 1, nirBits, width, 1, 0, 0);
                                sourceNirBand.ReadRaster(0, h, width, 1, swir2Bits, width, 1, 0, 0);

                                for (int w = 0; w < width; w++)
                                {
                                    Single value = nirBits[w] / (redBits[w] + swir2Bits[w]);
                                    idxBits[w] = Convert.ToSingle(value);
                                }

                                destBand.WriteRaster(0, h, width, 1, idxBits, width, 1, 0, 0);
                            }
                        }

                        // Extract mask band
                        OSGeo.GDAL.Band sourceMaskBand = null;
                        for (int b = 1; b <= sourceDS.RasterCount; b++)
                        {
                            if (sourceDS.GetRasterBand(b).GetDescription() == "oa_fmask")
                            {
                                sourceMaskBand = sourceDS.GetRasterBand(b);
                            }
                        }

                        // Iterate through source pixels (per row) and mask invalid pixels 
                        for (int h = 0; h < height; h++)
                        {
                            Single[] idxBits = new Single[width];
                            Int16[] maskBits = new Int16[width];

                            sourceMaskBand.ReadRaster(0, h, width, 1, maskBits, width, 1, 0, 0);
                            destBand.ReadRaster(0, h, width, 1, idxBits, width, 1, 0, 0);

                            for (int w = 0; w < width; w++)
                            {
                                if (!validPixels.Contains(maskBits[w]))
                                {
                                    idxBits[w] = Convert.ToSingle(noDataValue);
                                }
                            }

                            destBand.WriteRaster(0, h, width, 1, idxBits, width, 1, 0, 0);
                            destBand.SetNoDataValue(Convert.ToSingle(noDataValue));
                        }

                        // flush (i.e., save) the destintion geotiff
                        destDS.FlushCache();

                        // Close band and dataset
                        destDS.Dispose();
                        sourceDS.Dispose();

                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "HTTP error code : 400")
                        {
                            Error = true;
                        }
                        else if (ex.Message == "HTTP error code : 502")
                        {
                            Error = true;
                        }
                        else
                        {
                            string huh = null;
                        };
                    }

                    // Take a breath...
                    Task.Delay(100);
                });
            }
        }

        /// <summary>
        /// Constructs a STAC query url from user parameters.
        /// </summary>
        public static string ConstructStacUrl(string collection, string startDate, string endDate, double[] boundingBox, int limit)
        {
            // TODO: check inputs are valid
            //

            // Convert bounding box array into string of comma seperated SW, NE coords
            string bbox = string.Join(",", boundingBox);

            // Construct STAC query url
            string url = "";
            url += "https://explorer.sandbox.dea.ga.gov.au/stac/search?";
            url += $"collection={collection}";
            url += $"&time={startDate}/{endDate}";
            url += $"&bbox=[{bbox}]";
            url += $"&limit={limit}";

            return url;
        }

        /// <summary>
        /// Constructs a WCS query url from user parameters.
        /// </summary>
        public static string ConstructWcsUrl(string collection, List<string> assets, string date, double[] boundingBox)
        {
            // TODO: check inputs are valid
            //

            // Convert assets to string of comma seperated measurement names
            string measurements = string.Join(",", assets);

            // Convert bounding box array into string of comma seperated SW, NE coords
            string bbox = string.Join(",", boundingBox);

            // Create WCS query url
            string url = "";
            url += "https://ows.dea.ga.gov.au/wcs?service=WCS";
            url += "&VERSION=1.0.0";
            url += "&REQUEST=GetCoverage";
            url += "&COVERAGE=" + collection;
            url += "&TIME=" + date;
            url += "&MEASUREMENTS=" + measurements;
            url += "&BBOX=" + bbox;
            url += "&CRS=EPSG:3577";
            url += "&RESX=30.0";
            url += "&RESY=30.0";
            url += "&FORMAT=GeoTIFF";

            return url;
        }

        /// <summary>
        /// Constructs a STAC query url from user parameters.
        /// </summary>
        public static async Task<Root> GetStacFromUrlAsync(string url)
        {
            // TODO: try catchs
            //

            // TODO: check url is satisifactory
            // 

            // Prepare http client with 5 minute timeout
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            // Move through STAC pages and populate STAC Root object list
            List<Root> roots = new List<Root>();
            bool next = true;
            while (next)
            {
                // Get STAC response and check valid
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Deserialize json response and add to STAC roots list
                var content = await response.Content.ReadAsStringAsync();
                Root root = JsonSerializer.Deserialize<Root>(content);
                roots.Add(root);

                // Get url of next result page if exists, else stop loop
                var page = root.Links.Where(e => e.Relative == "next").FirstOrDefault();
                if (page != null)
                {
                    url = page.Href;
                }
                else
                {
                    next = false;
                }
            }

            // Combine STAC results into one if exist, else return empty root
            Root output = new Root();
            if (roots.Count > 0)
            {
                // Set first result page as output
                output = roots[0];

                // Append extra result pages to output, if exist
                if (roots.Count > 1)
                {
                    for (int i = 1; i < roots.Count; i++)
                    {
                        output.Features.AddRange(roots[i].Features);
                    }
                }

                // Modify STAC metadata to avoid confusion
                output.ReturnedCount = output.Features.Count;
                output.Context = null;
                output.Links = null;
            }

            return output;
        }

        /// <summary>
        /// Flattens multiple STAC Root objects into one.
        /// </summary>
        public static Root FlattenStacRoots(List<Root> roots)
        {
            // Init new root 
            Root flatRoot = new Root();
            flatRoot.Features = new List<Feature>();
            flatRoot.MatchedCount = 0;
            flatRoot.ReturnedCount = 0;

            foreach (Root root in roots)
            {
                flatRoot.Type = root.Type;
                flatRoot.Features.AddRange(root.Features);
                flatRoot.Links = flatRoot.Links;
                flatRoot.MatchedCount += root.MatchedCount;
                flatRoot.ReturnedCount += root.ReturnedCount;
                flatRoot.Context = root.Context;
                flatRoot.StacVersion = root.StacVersion;
            }

            return flatRoot;
        }

        /// <summary>
        /// Convert a STAC root object to a friendlier format.
        /// </summary>
        public static List<DownloadItem> ConvertStacToDownloads(Root root, List<string> assets, double[] boundingBox)
        {
            // Create new item list
            List<DownloadItem> items = new List<DownloadItem>();

            // Iterate and populate item list
            foreach (var feature in root.Features)
            {
                // Create and populate new item
                DownloadItem item = new DownloadItem();
                item.Id = feature.Id;
                item.DateTime = feature.Properties.DateTime;
                item.Date = feature.Properties.DateTime.ToString("yyyy-MM-dd");
                item.Time = feature.Properties.DateTime.ToString("HH:mm:ss");
                item.Platform = feature.Properties.Platform;
                item.Product = feature.Properties.Product;

                // Unpack all and requested asset names
                item.AvailableAssets = feature.Assets.Select(e => e.Key).ToList();
                item.RequestedAssets = assets;

                // Construct full and mask WCS urls
                item.FullWcsUrl = ConstructWcsUrl(item.Product, assets, item.Date, boundingBox);
                item.MaskWcsUrl = ConstructWcsUrl(item.Product, new List<string> { "oa_fmask" }, item.Date, boundingBox);

                // Set random generated output filenames
                string guid = Guid.NewGuid().ToString();
                item.FullFilename = "full" + "_" + guid + ".tif";
                item.MaskFilename = "mask" + "_" + guid + ".tif";
                item.FinalFilename = item.Date.Replace("-", "") + "T" + item.Time.Replace(":", "") + ".tif";

                // Set valid flag (start with false)
                item.Valid = false;

                // Add to list
                items.Add(item);
            }

            return items;
        }

        /// <summary>
        /// Sort a list of DownloadItems via datetime property.
        /// </summary>
        public static List<DownloadItem> SortByDateTime(List<DownloadItem> items)
        {
            // Create new list of simple items
            List<DownloadItem> sorted = new List<DownloadItem>();

            // Sort by date time ascending
            sorted = items.OrderBy(e => e.DateTime).ToList();

            return sorted;
        }

        /// <summary>
        /// Group a list of DownloadItems into solar day (i.e., strip time and get first date).
        /// </summary>
        public static List<DownloadItem> GroupBySolarDay(List<DownloadItem> items)
        {
            // Create new list of simple items
            List<DownloadItem> grouped = new List<DownloadItem>();

            // Group by date and choose first of each group
            grouped = items.GroupBy(e => e.Date).Select(e => e.First()).ToList();

            return grouped;
        }

        /// <summary>
        /// Removes any item where Landsat 7 and date > 31/5/2003 which is when SLC
        /// sensor started failing.
        /// </summary>
        public static List<DownloadItem> RemoveSlcOffData(List<DownloadItem> items)
        {
            // SLC-off start date
            DateTime startDateSlcOff = new DateTime(2003, 5, 31);

            // Create new list of items and remove invalid landsat 7 dates
            List<DownloadItem> cleaned = new List<DownloadItem>();
            foreach (var item in items)
            {
                if (item.Platform == "landsat-7") 
                {
                    if (item.DateTime.Date < startDateSlcOff.Date)
                    {
                        cleaned.Add(item);
                    }
                }
                else
                {
                    cleaned.Add(item);
                }
            }

            return cleaned;
        }


        /// <summary>
        /// Given a raster and a list of integer pixel classes, calculates the number of
        /// valid pixels (those which exist in the classes list) excluding overlap pixels
        /// (class 0). Returns the total number of valid, total number of overlaps, and
        /// total number of pixels within the raster.
        /// </summary>
        public static Dictionary<string, double> GetPercentValidPixels(Raster raster, List<int> classes)
        {
            // Get a pixel block for quicker reading and read from pixel top left pixel
            PixelBlock block = raster.CreatePixelBlock(raster.GetWidth(), raster.GetHeight());
            raster.Read(0, 0, block);

            // Read 2-dimensional pixel values into 1-dimensional byte array
            Array pixels2D = block.GetPixelData(0, false);
            byte[] pixels1D = new byte[pixels2D.Length];
            Buffer.BlockCopy(pixels2D, 0, pixels1D, 0, pixels2D.Length);

            // Get distinct pixel values and their counts
            var currentCounts = pixels1D.GroupBy(e => e).Select(x => new { Key = x.Key, Value = x.Count() });

            // Get number of valid, overlap and total pixels
            double numValid = currentCounts.Where(e => classes.Contains(e.Key)).Sum(e => e.Value);
            double numOverlap = currentCounts.Where(e => e.Key == 0).Sum(e => e.Value);
            double numTotal = pixels1D.Length;

            // Calculate percentages
            double pctValid = numValid / (numTotal - numOverlap);
            double pctOverlap = numOverlap / numTotal;

            // Store percentages in dictionary
            var percentages = new Dictionary<string, double>
            {
                {"pctValid", pctValid >= 0 ? pctValid : 0},
                {"pctOverlap", pctOverlap >= 0 ? pctOverlap : 0}
            };

            return percentages;
        }

        public static Raster MaskRasterInvalidPixels(Raster raster, int maskIndex, List<int> classes, Int16 noDataValue)
        {
            // Get a pixel block for quicker reading and read from pixel top left pixel
            PixelBlock block = raster.CreatePixelBlock(raster.GetWidth(), raster.GetHeight());
            raster.Read(0, 0, block);

            // Get a list of bands (including mask band)
            var bands = new List<Array>();
            for (int b = 0; b < block.GetPlaneCount(); b++)
            {
                bands.Add(block.GetPixelData(b, true));
            }

            // Extract fmask band via provided index
            Array maskPixels = block.GetPixelData(maskIndex, false);

            // Iterate each pixel and set as NoData if invalid
            for (int i = 0; i < block.GetHeight(); i++)
            {
                for (int j = 0; j < block.GetWidth(); j++)
                {
                    Int16 pixel = Convert.ToInt16(maskPixels.GetValue(j, i));
                    if (!classes.Contains(pixel))
                    {
                        foreach (var band in bands)
                        {
                            band.SetValue(noDataValue, j, i);
                        }
                    }
                }
            }

            // Iter bands and update in block
            for (int b = 0; b < bands.Count; b++)
            {
                block.SetPixelData(b, bands[b]);
            }

            // Update raster with changes and set nodata value
            raster.Write(0, 0, block);
            raster.SetNoDataValue(noDataValue);
            raster.Refresh();

            return raster;
        }

        public static Raster CalculateIndex(Raster raster, RasterDataset ds, string indexName)
        {
            //var a = ds.CreateRaster(new List<int> { 0 });
            //var b = ds.CreateRaster(new List<int> { 1 });
            //var c = ds.CreateRaster(new List<int> { 2 });

            //var indexRaster = a;

            // Setup new band for index values
            //indexRaster.SetPixelType(RasterPixelType.FLOAT);
            //var pixeltype = indexRaster.GetPixelType();

            //PixelBlock indexBlock = indexRaster.CreatePixelBlock(indexRaster.GetWidth(), indexRaster.GetHeight());
            //indexRaster.Read(0, 0, indexBlock);
            //Array indexArr = indexBlock.GetPixelData(0, true);

            // TODO: erase pixels via block to clear them?

            // Get required bands for index
            //raster.SetPixelType(RasterPixelType.DOUBLE);
            PixelBlock block = raster.CreatePixelBlock(raster.GetWidth(), raster.GetHeight());
            raster.Read(0, 0, block);

            Array redArr = block.GetPixelData(0, false);
            Array nirArr = block.GetPixelData(1, false);
            Array idxArr = block.GetPixelData(2, true);

            for (int i = 0; i < block.GetHeight(); i++)
            {
                for (int j = 0; j < block.GetWidth(); j++)
                {
                    float redValue = Convert.ToSingle(redArr.GetValue(j, i));
                    float nirValue = Convert.ToSingle(nirArr.GetValue(j, i));

                    float value = (nirValue - redValue) / (nirValue + redValue);

                    // TEMP: this is how we get around floating points...
                    byte temp = Convert.ToByte(value * 1000);

                    idxArr.SetValue(temp, j, i);
                }
            }

            block.SetPixelData(2, idxArr);
            raster.Write(0, 0, block);
            raster.Refresh();

            return raster;
        }
    }
}
