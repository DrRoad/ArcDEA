using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Data;
using ArcGIS.Core.Threading.Tasks;

namespace ArcDEA.Classes
{
    public class Stac
    {
        public string QueryEndpoint { get; set; }
        public string QueryCollection { get; set; }
        public string QueryStartDate { get; set; }
        public string QueryEndDate { get; set; }
        public double[] QueryBoundingBoxWgs84 { get; set; }
        public double[] QueryBoundingBoxAlbers { get; set; }
        public int QueryLimit { get; set; }
        public Root Result { get; set; }
        public Dictionary<string, string[]> Wcs { get; set; }

        public Stac(string collection, string startDate, string endDate, double[] boundingBoxWgs84, double[] boundingBoxAlbers, int limit)
        {
            QueryEndpoint = "https://explorer.sandbox.dea.ga.gov.au/stac/search?";
            QueryCollection = collection;
            QueryStartDate = startDate;
            QueryEndDate = endDate;
            QueryBoundingBoxWgs84 = boundingBoxWgs84;
            QueryBoundingBoxAlbers = boundingBoxAlbers;
            QueryLimit = limit;
            Result = null;
            Wcs = null;
        }

        public async Task QueryStacAsync(int timeout)
        {
            // Convert bounding box array into double array sw, ne
            string bbox = string.Join(",", QueryBoundingBoxWgs84);

            // Construct STAC query url
            string url = QueryEndpoint;
            url += $"collection={QueryCollection}";
            url += $"&time={QueryStartDate}/{QueryEndDate}";
            url += $"&bbox=[{bbox}]";
            url += $"&limit={QueryLimit}";

            // Prepare http client
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(timeout);

            // TODO: try catches

            // Recursively parse all STAC features based on user request
            bool more = true;
            while (more)
            {
                // Get initial list of STAC results for user request
                var response = await QueuedTask.Run(() => { return client.GetAsync(url); });
                response.EnsureSuccessStatusCode();

                // Parse content and deserialize json into STAC class
                var content = await QueuedTask.Run(() => { return response.Content.ReadAsStringAsync(); });
                Root data = JsonSerializer.Deserialize<Root>(content);

                // On first pass just add, on others, append
                if (Result == null)
                {
                    Result = data;
                }
                else
                {
                    Result.Features.AddRange(data.Features);
                }

                // Check if a "next" link exists, iterate and append, else leave
                var next = data.Links.Where(e => e.Relative == "next").FirstOrDefault();
                if (next != null)
                {
                    url = next.Href;
                }
                else
                {
                    more = false;
                }
            }
        }

        public void GroupBySolarDay()
        {
            // Sort by datetime
            Result.Features.Sort((a, b) => a.Properties.DateTime.CompareTo(b.Properties.DateTime));

            // Group by date (without time) and select first date in each group
            List<Feature> groupedFeatures = Result.Features.GroupBy(e => e.Properties.DateTime.ToString("yyyy-MM-dd")).Select(e => e.First()).ToList();
            Result.Features = groupedFeatures;
        
        }

        public async Task DropInvalidFeaturesAsync(List<int> validClasses, double minValid)
        {
            // TODO: check inputs
            // TODO: group by solar day functonality
            // TODO: move wcs code out to helpers, we use it again elsewhere

            // Set up dictionary to hold raw and clean date
            Dictionary<string, string[]> items = new Dictionary<string, string[]>();

            // Convert raw dates to STAC-friendly format
            var dates = Result.Features.Select(e => e.Properties.DateTime.ToString("yyyy-MM-dd"));  //yyyy-MM-ddThh:mm:ss
            dates = dates.Distinct().ToList();

            // Get Albers bounding box
            string bbox = string.Join(",", QueryBoundingBoxAlbers);

            foreach (string date in dates)
            {
                // Create WCS query url
                string url = "";
                url += "https://ows.dea.ga.gov.au/wcs?service=WCS";
                url += "&VERSION=1.0.0";
                url += "&REQUEST=GetCoverage";
                url += "&COVERAGE=ga_ls8c_ard_3";
                url += "&TIME=" + date;
                url += "&MEASUREMENTS=oa_fmask";
                url += "&BBOX=" + bbox;
                url += "&CRS=EPSG:3577";
                url += "&RESX=30.0";
                url += "&RESY=30.0";
                url += "&FORMAT=GeoTIFF";

                // Create new item data structure (url, filename) and add
                string[] item = new string[] {url, null};
                items.Add(date, item);
            }

            // Get the current temp folder path
            string folder = Path.GetTempPath();

            // Open a HTTP client with 60 minute timeout
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(60);

            // Create a list of mask downloader tasks
            var tasks = new List<Task>();
            foreach (var item in items)
            {
                string date = item.Key;
                string url = item.Value[0];

                tasks.Add(Task.Run(async () => 
                {
                    // Notify
                    System.Diagnostics.Debug.WriteLine($"Started download: {date}");

                    // Create temporary file path and set Uri
                    string filename = Guid.NewGuid().ToString() + ".tif";
                    string filepath = Path.Join(folder, filename);

                    // Query URL and ensure success
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    // Download temporary file to temporary folder
                    using (FileStream fs = new FileStream(filepath, FileMode.CreateNew))
                    {
                        await response.Content.CopyToAsync(fs);
                    }

                    // Update filename in current items
                    item.Value[1] = filename;

                    // Notify
                    System.Diagnostics.Debug.WriteLine($"Finished download: {date}");
                }));
            }

            // Run all download tasks
            await Task.WhenAll(tasks);

            // Iterate items, count percent valid pixels, return invalid dates, all on background threads
            List<string> invalidDates = await BackgroundTask.Run(() => {

                // todo make max degree parallel dynamic
                ParallelOptions options = new ParallelOptions();
                options.MaxDegreeOfParallelism = 3;  

                List<string> invalidDates = new List<string>();
                Parallel.ForEach(items, options, item => 
                { 
                    string date = item.Key;
                    string url = item.Value[0];
                    string filename = item.Value[1];
                    string filepath = Path.Join(folder, filename);

                    // Notify
                    System.Diagnostics.Debug.WriteLine($"Started validity check: {date}");

                    // Create connection to file
                    Uri folderUri = new Uri(folder);
                    FileSystemConnectionPath conn = new FileSystemConnectionPath(folderUri, FileSystemDatastoreType.Raster);
                    FileSystemDatastore store = new FileSystemDatastore(conn);

                    // Read raster from store
                    RasterDataset rasterDataset = store.OpenDataset<RasterDataset>(filename);
                    Raster raster = rasterDataset.CreateFullRaster();

                    // Get a pixel block for quicker reading and read from pixel top left pixel
                    PixelBlock block = raster.CreatePixelBlock(raster.GetWidth(), raster.GetHeight());
                    raster.Read(0, 0, block);

                    // Read 2-dimensional pixel values into 1-dimensional byte array
                    Array pixels2D = block.GetPixelData(0, false);
                    byte[] pixels1D = new byte[pixels2D.Length];
                    Buffer.BlockCopy(pixels2D, 0, pixels1D, 0, pixels2D.Length);

                    // Get distinct pixel values and their counts
                    var uniqueCounts = pixels1D.GroupBy(e => e).Select(x => new { key = x.Key, val = x.Count() }).ToList();

                    // Get the total of all pixels excluding unclassified (i.e., overlap boundary areas, or 0)
                    double totalPixels = uniqueCounts.Where(e => e.key != 0).Sum(e => e.val);

                    //Unclassified-> 0.
                    //Clear-> 1.
                    //Cloud-> 2.
                    //Cloud Shadow -> 3.
                    //Snow-> 4.
                    //Water-> 5.

                    // Calculate percentage of each fmask class



                    // Count percentage of valid pixels and keep if > minimum allowed
                    //double totalPixels = pixels2D.Length;
                    double validPixels = pixels1D.Where(e => validClasses.Contains(e)).ToArray().Length;

                    if ((validPixels / totalPixels) < minValid)
                    {
                        invalidDates.Add(date);
                    }

                    // TODO: Delete image for current item

                    // Notify
                    System.Diagnostics.Debug.WriteLine($"Ended validity check: {date}");
                });

                return invalidDates;
            }, BackgroundProgressor.None);

            // Remove any date from Result that are valid and sort ascending (inplace)
            Result.Features.RemoveAll(e => invalidDates.Contains(e.Properties.DateTime.ToString("yyyy-MM-dd")));  //yyyy-MM-ddThh:mm:ss
            Result.Features.Sort((a, b) => a.Properties.DateTime.CompareTo(b.Properties.DateTime));
        }
    
        public void GetWcs(List<string> assetNames)
        {
            // TODO: check inputs
            // TODO: check Result items
            // TODO: move this code to helpers, reference that
            // TODO: implement group by solar day

            // Set up dictionary to hold raw and clean date
            Wcs = new Dictionary<string, string[]>();

            // Convert assetNames to comma-seperated string
            string assets = string.Join(",", assetNames);

            // Convert raw dates to WCS-friendly format
            var dates = Result.Features.Select(e => e.Properties.DateTime.ToString("yyyy-MM-dd"));  //yyyy-MM-ddThh:mm:ss
            dates = dates.Distinct().ToList();

            // Get Albers bounding box
            string bbox = string.Join(",", QueryBoundingBoxAlbers);

            foreach (string date in dates)
            {
                // Create WCS query url
                string url = "";
                url += "https://ows.dea.ga.gov.au/wcs?service=WCS";
                url += "&VERSION=1.0.0";
                url += "&REQUEST=GetCoverage";
                url += "&COVERAGE=ga_ls8c_ard_3";
                url += "&TIME=" + date;
                url += "&MEASUREMENTS=" + assets;
                url += "&BBOX=" + bbox;
                url += "&CRS=EPSG:3577";
                url += "&RESX=30.0";
                url += "&RESY=30.0";
                url += "&FORMAT=GeoTIFF";

                // Prepare filename
                string filename = date.Replace("-", "").Replace(":", "") + ".tif";

                // Create new item data structure (url, filename) and add
                string[] item = new string[] { url, filename };
                Wcs.Add(date, item); 
            }
        }
    }

    public class Root
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("features")]
        public List<Feature> Features { get; set; }

        [JsonPropertyName("links")]
        public List<Link> Links { get; set; }

        [JsonPropertyName("numberReturned")]
        public int ReturnedCount { get; set; }

        [JsonPropertyName("context")]
        public Context Context { get; set; }

        [JsonPropertyName("numberMatched")]
        public int MatchedCount { get; set; }

        [JsonPropertyName("stac_version")]
        public string StacVersion { get; set; }
    
    }

    public class Context
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("returned")]
        public int Returned { get; set; }

        [JsonPropertyName("matched")]
        public int Matched { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }
    }

    public class Feature
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("properties")]
        public Properties Properties { get; set; }

        [JsonPropertyName("geometry")]
        public Geometry Geometry { get; set; }

        [JsonPropertyName("assets")]
        public Dictionary<string, Asset> Assets { get; set; }

        [JsonPropertyName("stac_version")]
        public string StacVersion { get; set; }
    }

    public class Asset
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("href")]
        public string Href { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("proj:epsg")]
        public int Epsg { get; set; }

        [JsonPropertyName("proj:shape")]
        public int[] Shape { get; set; }

        [JsonPropertyName("proj:transform")]
        public float[] Transform { get; set; }

        [JsonPropertyName("roles")]
        public string[] Roles { get; set; }
    }

    public class Properties
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("datetime")]
        public DateTime DateTime { get; set; }

        [JsonPropertyName("proj:epsg")]
        public int Epsg { get; set; }
        
        [JsonPropertyName("proj:shape")]
        public int[] Shape { get; set; }
        
        [JsonPropertyName("proj:transform")]
        public float[] Transform { get; set; }
        
        [JsonPropertyName("platform")]
        public string Platform { get; set; }
        
        [JsonPropertyName("odc:product")]
        public string Product { get; set; }
        
        [JsonPropertyName("odc:producer")]
        public string Producer { get; set; }
        
        [JsonPropertyName("odc:product_family")]
        public string ProductFamily { get; set; }
        
        [JsonPropertyName("odc:dataset_version")]
        public string DatasetVersion { get; set; }
        
        [JsonPropertyName("dea:dataset_maturity")]
        public string DatasetMaturity { get; set; }
        
        [JsonPropertyName("instruments")]
        public string[] Instruments { get; set; }
        
        [JsonPropertyName("eo:cloud_cover")]
        public float CloudCover { get; set; }
        
        [JsonPropertyName("view:sun_azimuth")]
        public float SunAzimuth { get; set; }
        
        [JsonPropertyName("view:sun_elevation")]
        public float SunElevation { get; set; }
        
        [JsonPropertyName("odc:region_code")]
        public string RegionCode { get; set; }
        
        [JsonPropertyName("odc:file_format")]
        public string FileFormat { get; set; }
        
        [JsonPropertyName("landsat:landsat_scene_id")]
        public string LandsatSceneId { get; set; }

        [JsonPropertyName("landsat:wrs_row")]
        public int LandsatWrsRow { get; set; }

        [JsonPropertyName("landsat:wrs_path")]
        public int LandsatWrsPath { get; set; }

        [JsonPropertyName("landsat:collection_number")]
        public int LandsatCollectionNumber { get; set; }

        [JsonPropertyName("landsat:landsat_product_id")]
        public string LandsatProductId { get; set; }

        [JsonPropertyName("landsat:collection_category")]
        public string LandsatCollectionCategory { get; set; }
    }

    public class Geometry
    {
        [JsonPropertyName("coordinates")]
        public float[][][] Coordinates { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class Link
    {
        [JsonPropertyName("href")]
        public string Href { get; set; }

        [JsonPropertyName("rel")]
        public string Relative { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; }
    }
}
