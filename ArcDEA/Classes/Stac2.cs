using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArcDEA.Classes
{
    #region STAC metadata classes
    /// <summary>
    /// Root of STAC object. Contains top-level metadata for STAC 
    /// query result.
    /// </summary>
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

    /// <summary>
    /// STAC link object. Contains metadata on various page links
    /// returned from STAC query.
    /// </summary>
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

    /// <summary>
    /// Context of STAC object. Contains metadata for STAC query
    /// result regarding number of pages returned vs matched.
    /// </summary>
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

    /// <summary>
    /// A STAC feature object. Contains metadata for a single STAC
    /// feature (e.g., Landsat scene), which itself may contain one 
    /// or more assets (e.g., bands).
    /// </summary>
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

        [JsonPropertyName("bbox")]
        public double[] BoundingBox { get; set; }

        [JsonPropertyName("assets")]
        public Dictionary<string, Asset> Assets { get; set; }

        [JsonPropertyName("stac_version")]
        public string StacVersion { get; set; }
    }

    /// <summary>
    /// A STAC featyre geometry object. Contains metadata for 
    /// a feature's geometry (e.g., bounding box).
    /// </summary>
    public class Geometry
    {
        [JsonPropertyName("coordinates")]
        public float[][][] Coordinates { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    /// <summary>
    /// A STAC asset object. Contains metadata for a single STAC
    /// asset (e.g., Landsat band).
    /// </summary>
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

    /// <summary>
    /// A STAC asset properties object. Contains metadata for 
    /// various STAC asset properties (e.g., date, time, band name).
    /// </summary>
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
    #endregion

    #region Download class
    /// <summary>
    /// 
    /// </summary>
    public class Download
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public Dictionary<string, string> Urls { get; set; }
        public bool IsValid { get; set; } = false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="urls"></param>
        public Download(string id, DateTime date, Dictionary<string, string> urls)
        {
            Id = id;
            Date = date;
            Urls = urls;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="validPixels"></param>
        /// <param name="minValid"></param>
        public async Task CheckValidityViaMaskAsync(List<int> validPixels, double minValid)
        {
            // Fmask classes:
            //Unclassified -> 0.
            //Clear        -> 1.
            //Cloud        -> 2.
            //Cloud Shadow -> 3.
            //Snow         -> 4.
            //Water        -> 5.

            // Always set to invalid to begin
            IsValid = false;

            // Extract and check fmask WCS url
            string url = Urls.GetValueOrDefault("Mask");
            if (url == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Open (download) mask WCS url
                    using (OSGeo.GDAL.Dataset ds = OSGeo.GDAL.Gdal.Open(url, OSGeo.GDAL.Access.GA_ReadOnly))
                    {
                        // Isolate raster fmask band
                        OSGeo.GDAL.Band band = ds.GetRasterBand(1);

                        // Get required raster band dimensions
                        int width = band.XSize;
                        int height = band.YSize;
                        int size = width * height;

                        // Read byte values into block array
                        Byte[] block = new Byte[size];
                        band.ReadRaster(0, 0, width, height, block, width, height, 0, 0);

                        // Get distinct pixel values and counts, non-zero (overlap) pixel total and number valid
                        var counts = block.GroupBy(e => e).Select(x => new { key = x.Key, val = x.Count() }).ToList();
                        long valid = block.Where(e => validPixels.Contains(e)).ToArray().Length;
                        long total = counts.Where(e => e.key != 0).Sum(e => e.val);

                        // Calculate percentage of valid pixels,  keep if >= minimum allowed
                        if (((double)valid / (double)total) >= minValid)
                        {
                            IsValid = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error during mask download!");
                }
            });
        }

        public async Task DownloadAndProcessFullAsync(string outputFolder, List<int> validPixels, bool dropMaskBand)
        {
            // TODO: some pixel values come back as -999, is -999 no data or 0 for dea landsat/sentinel?
            // on closer look, these -999 pixels are occurring under fmask class 5 (water)
            // do we want to set any value < 0 to 0?

            // Create full output filename and combine with output folder
            string outputFilename = $"{Date.Year}-{Date.Month}-{Date.Day}.tif";
            string outputFile = Path.Combine(outputFolder, outputFilename);

            // Extract and check fmask WCS url
            string url = Urls.GetValueOrDefault("Full");
            if (url == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Open (download) full WCS url
                    using (OSGeo.GDAL.Dataset inDS = OSGeo.GDAL.Gdal.Open(url, OSGeo.GDAL.Access.GA_Update))
                    {
                        // Get required dataset dimensions (all bands will be uniform)
                        int width = inDS.RasterXSize;
                        int height = inDS.RasterYSize;
                        int size = width * height;

                        // Create a dict of band names and a "value tuple" of index, band, block data
                        var bands = new Dictionary<string, (int Index, OSGeo.GDAL.Band Band, short[] Block)>();
                        for (int i = 1; i <= inDS.RasterCount; i++)
                        {
                            OSGeo.GDAL.Band band = inDS.GetRasterBand(i);
                            string bandName = band.GetDescription();

                            short[] block = new short[size];
                            band.ReadRaster(0, 0, width, height, block, width, height, 0, 0);

                            bands.Add(bandName, (i, band, block));
                        }

                        // Iterate each band block and set pixels to 0 where mask value invalid
                        for (int i = 0; i < bands["oa_fmask"].Block.Length; i++)
                        {
                            if (!validPixels.Contains(bands["oa_fmask"].Block[i]))
                            {
                                foreach (var band in bands)
                                {
                                    if (band.Key != "oa_fmask")
                                    {
                                        band.Value.Block[i] = (short)0;
                                    }
                                }
                            }
                        }

                        // TODO: remove anything < 0

                        // Iterate each band and block and write changes
                        foreach (var band in bands)
                        {
                            if (band.Key != "oa_fmask")
                            {
                                band.Value.Band.WriteRaster(0, 0, width, height, band.Value.Block, width, height, 0, 0);
                            }
                        }

                        // Save changed to dataset
                        inDS.FlushCache();

                        // Drop mask band, if requested...
                        if (dropMaskBand == true)
                        {
                            // Get non-mask band indexes for translate options
                            List<string> keepBands = new List<string>();
                            foreach(var band in bands)
                            {
                                if (band.Key != "oa_fmask")
                                {
                                    keepBands.Add("-b");
                                    keepBands.Add(band.Value.Index.ToString());
                                }
                            }

                            // Setup translate options, perform translate to subset bands, export as GeoTiff
                            OSGeo.GDAL.GDALTranslateOptions options = new OSGeo.GDAL.GDALTranslateOptions(keepBands.ToArray());
                            OSGeo.GDAL.Gdal.wrapper_GDALTranslate(outputFile, inDS, options, null, null);
                        }
                        else
                        {
                            // Export as GeoTiff if no modifications needed
                            OSGeo.GDAL.Driver driver = OSGeo.GDAL.Gdal.GetDriverByName("GTiff");
                            OSGeo.GDAL.Dataset outDS = driver.CreateCopy(outputFile, inDS, 0, null, null, null);
                        }

                        // Notify
                        Debug.WriteLine($"Ended download: {Id}");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error during full download!");
                }
            });
        }

        public async Task DownloadAndProcessIndexAsync(string outputFolder, string index, List<int> validPixels, bool dropMaskBand)
        {
            // TODO: take user nodatavalue
            float noDataValue = -999;

            // Create full output filename and combine with output folder
            string outputFilename = $"{Date.Year}-{Date.Month}-{Date.Day}.tif";
            string outputFile = Path.Combine(outputFolder, outputFilename);

            // Extract and check fmask WCS url
            string url = Urls.GetValueOrDefault("Full");
            if (url == null)
            {
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Open (download) full WCS url
                    using (OSGeo.GDAL.Dataset inDS = OSGeo.GDAL.Gdal.Open(url, OSGeo.GDAL.Access.GA_Update))
                    {
                        // Get required dataset dimensions (all bands will be uniform)
                        int width = inDS.RasterXSize;
                        int height = inDS.RasterYSize;
                        int size = width * height;

                        // Create a dict of band names and a "value tuple" of index, band, block data
                        var bands = new Dictionary<string, (int Index, OSGeo.GDAL.Band Band, short[] Block)>();
                        for (int i = 1; i <= inDS.RasterCount; i++)
                        {
                            OSGeo.GDAL.Band band = inDS.GetRasterBand(i);
                            string bandName = band.GetDescription();

                            short[] block = new short[size];
                            band.ReadRaster(0, 0, width, height, block, width, height, 0, 0);

                            bands.Add(bandName, (i, band, block));
                        }

                        // TODO: use using

                        // Copy dataset to new in-memory dataset so we can add index band
                        OSGeo.GDAL.Driver memDriver = OSGeo.GDAL.Gdal.GetDriverByName("MEM");
                        OSGeo.GDAL.Dataset tmpDS = memDriver.CreateCopy("", inDS, 0, null, null, null);

                        // Add new empty index band to dataset
                        tmpDS.AddBand(OSGeo.GDAL.DataType.GDT_Float32, null);

                        // Get the new band for index values (will be last index)
                        OSGeo.GDAL.Band indexBand = tmpDS.GetRasterBand(tmpDS.RasterCount);
                        indexBand.SetDescription(index.ToLower());

                        // Read index values into index block (there will be none)
                        float[] indexBlock = new float[size];
                        indexBand.ReadRaster(0, 0, width, height, indexBlock, width, height, 0, 0);

                        // Process block depending on user selection
                        // https://github.com/GeoscienceAustralia/dea-notebooks/blob/develop/Tools/dea_tools/bandindices.py
                        if (index == "EVI")
                        {
                            indexBlock = Helpers.EVI(indexBlock, bands["nbart_blue"].Block, bands["nbart_red"].Block, bands["nbart_nir"].Block, bands["oa_fmask"].Block, validPixels, noDataValue);
                        }
                        else if (index == "LAI")
                        {
                            indexBlock = Helpers.LAI(indexBlock, bands["nbart_blue"].Block, bands["nbart_red"].Block, bands["nbart_nir"].Block, bands["oa_fmask"].Block, validPixels, noDataValue);
                        }
                        else if (index == "MSAVI")
                        {
                            indexBlock = Helpers.MSAVI(indexBlock, bands["nbart_red"].Block, bands["nbart_nir"].Block, bands["oa_fmask"].Block, validPixels, noDataValue);
                        }
                        else if (index == "NDVI")
                        {
                            indexBlock = Helpers.NDVI(indexBlock, bands["nbart_red"].Block, bands["nbart_nir"].Block, bands["oa_fmask"].Block, validPixels, noDataValue);
                        }
                        else if (index == "kNDVI")
                        {
                            indexBlock = Helpers.kNDVI(indexBlock, bands["nbart_red"].Block, bands["nbart_nir"].Block, bands["oa_fmask"].Block, validPixels, noDataValue);
                        }

                        // Write changes to index band
                        indexBand.WriteRaster(0, 0, width, height, indexBlock, width, height, 0, 0);

                        // Set nodata
                        indexBand.SetNoDataValue(float.Epsilon);

                        // Save changed to temporary dataset
                        tmpDS.FlushCache();

                        // Prepare output band parameters for translate, add mask to end if requested
                        List<string> keepBands = new List<string>() { "-b", tmpDS.RasterCount.ToString()};
                        if (dropMaskBand == false)
                        {
                            int maskBandIndex = Helpers.GetMaskBandIndex(tmpDS);
                            keepBands.Add("-b");
                            keepBands.Add(maskBandIndex.ToString());
                        }

                        // Setup translate options, perform translate to subset bands, export as GeoTiff
                        OSGeo.GDAL.GDALTranslateOptions options = new OSGeo.GDAL.GDALTranslateOptions(keepBands.ToArray());
                        OSGeo.GDAL.Gdal.wrapper_GDALTranslate(outputFile, tmpDS, options, null, null);

                        // Notify
                        Debug.WriteLine($"Ended download: {Id}");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error during full download!");
                }
            });
        }

    }
    #endregion


    /// <summary>
    /// 
    /// </summary>
    public class Stac2
    {
        public string Endpoint { get; set; } = "https://explorer.sandbox.dea.ga.gov.au/stac/search?";
        public string[] Collections { get; set; }
        public string[] Assets { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public double[] BoundingBox { get; set; }
        public int Epsg { get; set; }
        public double Resolution { get; set; }
        public int Limit { get; set; } = 250;
        public List<Feature> Features { get; set; }

        public Stac2(string[] collections, string[] assets, string startDate, string endDate, double[] boundingBox, int epsg, double resolution)
        {
            Collections = collections;
            Assets = assets;
            StartDate = startDate;
            EndDate = endDate;
            BoundingBox = boundingBox;
            Epsg = epsg;
            Resolution = resolution;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public async Task GetFeaturesAsync(HttpClient client)
        {
            // Initialise a list of features
            Features = new List<Feature>();

            // Iterate each collection, fetch features, append to features list
            foreach (string collection in Collections)
            {
                // Construct STAC URL
                string url = Endpoint;
                url += $"collection={collection}";
                url += $"&time={StartDate}/{EndDate}";
                url += $"&bbox=[{string.Join(",", BoundingBox)}]";
                url += $"&limit={Limit}";

                try
                {
                    bool more = true;
                    while (more)
                    {
                        // Get content from STAC endpoint, deserialise and add to list
                        string content = await client.GetStringAsync(url);
                        Root root = JsonSerializer.Deserialize<Root>(content);

                        // Add returned features to feature list
                        Features.AddRange(root.Features);

                        // Check if a "next" link exists, iterate and append, else leave
                        var next = root.Links.Where(e => e.Relative == "next").FirstOrDefault();
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
                catch (HttpRequestException e)
                {
                    Console.WriteLine("Message :{0} ", e.Message);
                    Features = null;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void RemoveSlcOffFeatures()
        {
            if (Features != null && Features.Count > 0)
            {
                // Set SLC-off start date (2003 May 31)
                DateTime startDateSlcOff = new DateTime(2003, 5, 31);

                // Iterate features and exclude slcoff features
                List<Feature> result = new List<Feature>();
                foreach (var feat in Features)
                {
                    // Get current platform name and date and add to result if valid
                    string featPlatform = feat.Properties.Platform;
                    DateTime featDateTime = feat.Properties.DateTime;
                    if (featPlatform == "landsat-7" && featDateTime.Date >= startDateSlcOff.Date)
                    {
                        continue;
                    }
                    else
                    {
                        result.Add(feat);
                    }
                }

                // Override existing features
                Features = result;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void GroupBySolarDay()
        {
            // Sort by datetime in ascending order
            if (Features != null && Features.Count > 1)
            {
                Features = Features.OrderBy(e => e.Properties.DateTime).ToList();
            }

            // Group by date (without time), select first date in each group, overwrite
            List<Feature> groups = Features.GroupBy(e => e.Properties.DateTime.ToString("yyyy-MM-dd")).Select(e => e.First()).ToList();
            Features = groups;
        }

        /// <summary>
        /// 
        /// </summary>
        public void SortFeaturesByDate()
        {
            if (Features != null && Features.Count > 1)
            {
                Features = Features.OrderBy(e => e.Properties.DateTime).ToList();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public List<Download> ConvertFeaturesToDownloads()
        {
            // Create a list of Download
            List<Download> downloads = new List<Download>();

            if (Features != null && Features.Count > 0)
            {
                // Convert raw query parameters to string formats
                string assets = string.Join(",", Assets);

                // Project input bbox to output bbox coordinates and set as string
                double[] outBoundingBox = Helpers.ReprojectBoundingBox(BoundingBox, 4326, Epsg);
                string bbox = string.Join(",", outBoundingBox);

                // Convert epsg and resolution to strings
                string epsg = $"EPSG:{Epsg}";
                string resolution = Resolution.ToString();

                foreach (var feature in Features)
                {
                    // Convert collection, datetime to date strings
                    string collection = feature.Properties.Product.ToString();
                    string date = feature.Properties.DateTime.ToString("yyyy-MM-dd");

                    // Create WCS query url (without assets)
                    string url = "";
                    url += "https://ows.dea.ga.gov.au/wcs?service=WCS";
                    url += "&VERSION=1.0.0";
                    url += "&REQUEST=GetCoverage";
                    url += "&COVERAGE=" + collection;
                    url += "&TIME=" + date;
                    url += "&MEASUREMENTS={*}";
                    url += "&BBOX=" + bbox;
                    url += "&CRS=" + epsg;
                    url += "&RESX=" + resolution;
                    url += "&RESY=" + resolution;
                    url += "&FORMAT=GeoTIFF";

                    // Create a WCS url for mask and user assets, seperately
                    Dictionary<string, string> urls = new Dictionary<string, string>()
                    {
                        {"Mask", url.Replace("{*}", "oa_fmask") },
                        {"Full", url.Replace("{*}", assets) },
                    };

                    // Create new download item and add to downloads
                    Download download = new Download(feature.Id, feature.Properties.DateTime, urls);
                    downloads.Add(download);
                }
            }

            return downloads;
        }
    }
}
