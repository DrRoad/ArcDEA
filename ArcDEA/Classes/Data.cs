using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
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
            public string FullWcsUrl { get; set; }
            public string MaskWcsUrl { get; set; }
            public string FullFilename { get; set; }
            public string MaskFilename { get; set; }
            public string FinalFilename { get; set; }
            public bool Valid { get; set; }
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
            while(next)
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
                item.FinalFilename = item.Date.Replace("-", "") + ".tif";

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
    }
}
