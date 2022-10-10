using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ArcDEA.Classes
{
    public static class Helpers
    {
        #region DockPane control data structures
        /// <summary>
        /// Class to hold collection information and track selections.
        /// </summary>
        public class CollectionItem
        {
            public string RawName { get; set; }
            public string CleanName { get; set; }
            public bool IsCollectionSelected { get; set; }

            public CollectionItem(string rawName, string cleanName, bool isCollectionSelected)
            {
                RawName = rawName;
                CleanName = cleanName;
                IsCollectionSelected = isCollectionSelected;
            }
        }

        /// <summary>
        /// Class to hold raw asset information and track selections.
        /// </summary>
        public class AssetRawItem
        {
            public string RawName { get; set; }
            public string CleanName { get; set; }
            public bool IsRawAssetSelected { get; set; }

            public AssetRawItem(string rawName, string cleanName, bool isRawAssetSelected)
            {
                RawName = rawName;
                CleanName = cleanName;
                IsRawAssetSelected = isRawAssetSelected;
            }
        }

        /// <summary>
        /// Class to hold index asset information and track selection.
        /// </summary>
        public class AssetIndexItem
        {
            public string ShortName { get; set; }
            public string LongName { get; set; }
            public List<string> Bands { get; set; }
            public bool IsIndexAssetSelected { get; set; }

            public AssetIndexItem(string shortName, string longName, List<string> bands, bool isIndexAssetSelected)
            {
                ShortName = shortName;
                LongName = longName;
                Bands = bands;
                IsIndexAssetSelected = isIndexAssetSelected;
            }
        }

        /// <summary>
        /// Class to hold quality fmask values and track selection.
        /// </summary>
        public class MaskValueItem
        {
            public string Label { get; set; }
            public int Value { get; set; }
            public bool IsMaskValueSelected { get; set; }
            
            public MaskValueItem(string label, int value, bool isMaskValueSelected)
            {
                Label = label;
                Value = value;
                IsMaskValueSelected = isMaskValueSelected;
            }
        }
        #endregion

        #region DockPane control populators
        /// <summary>
        /// Populate collection list items on DockPane UI control.
        /// </summary>
        public static List<CollectionItem> PopulateCollectionItems()
        {
            List<CollectionItem> items = new List<CollectionItem>()
            {
                { new CollectionItem("ga_ls5t_ard_3", "Landsat 5 TM", false) },
                { new CollectionItem("ga_ls7e_ard_3", "Landsat 7 ETM+", false) },
                { new CollectionItem("ga_ls8c_ard_3", "Landsat 8 OLI", true) }
            };

            return items;
        }

        /// <summary>
        /// Populate raw-based asset list items on DockPane UI control.
        /// </summary>
        public static List<AssetRawItem> PopulateRawAssetItems()
        {
            List<AssetRawItem> items = new List<AssetRawItem>()
            {
                { new AssetRawItem("nbart_blue",   "Blue",   false) },
                { new AssetRawItem("nbart_green",  "Green",  false) },
                { new AssetRawItem("nbart_red",    "Red",    false) },
                { new AssetRawItem("nbart_nir",    "NIR",    false) },
                { new AssetRawItem("nbart_swir_1", "SWIR 1", false) },
                { new AssetRawItem("nbart_swir_2", "SWIR 2", false) }
            };

            return items;

        }

        /// <summary>
        /// Populate index-based asset list items on DockPane UI control.
        /// </summary>
        public static List<AssetIndexItem> PopulateIndexAssetItems()
        {
            List<AssetIndexItem> items = new List<AssetIndexItem>()
            {
                { new AssetIndexItem("EVI", "Enhanced Vegetation Index", new List<string> { "nbart_blue", "nbart_red", "nbart_nir" }, false) },
                { new AssetIndexItem("LAI", "Leaf Area Index", new List<string> { "nbart_blue", "nbart_red", "nbart_nir" }, false) },
                { new AssetIndexItem("MSAVI", "Modified Soil Adjusted Vegetation Index", new List<string> { "nbart_red", "nbart_nir" }, false) },
                { new AssetIndexItem("NDVI", "Normalised Difference Vegetation Index", new List<string> { "nbart_red", "nbart_nir" }, false) },
                { new AssetIndexItem("kNDVI", "Non-linear Normalised Difference Vegation Index", new List<string> { "nbart_red", "nbart_nir" }, false) }
                //{ new AssetIndexItem("SLAVI", "Specific Leaf Area Vegetation Index", new List<string> { "nbart_red", "nbart_nir", "nbart_swir_2" }, false) }
            };

            return items;

        }

        /// <summary>
        /// Populate mask value list items on DockPane UI control.
        /// </summary>
        public static List<MaskValueItem> PopulateMaskValueItems()
        {
            List<MaskValueItem> items = new List<MaskValueItem>()
            {
                { new MaskValueItem("Unclassified", 0, false) },
                { new MaskValueItem("Clear", 1, true) },
                { new MaskValueItem("Cloud", 2, false) },
                { new MaskValueItem("Cloud Shadow", 3, false) },
                { new MaskValueItem("Snow", 4, true) },
                { new MaskValueItem("Water", 5, true) },
            };

            return items;

        }
        #endregion

        /// <summary>
        /// NuGet GDAL + ESRI ArcGIS Pro has some issues initialising GDAL.
        /// Namely, not all required DLLs are copied to assembly cache on run.
        /// To get around this, a gdal folder exists in project with latest dlls,
        /// (excluding four key dlls from nuget) which is copied to assembley cache.
        /// These dlls need to be in assembley root, so a move is done to take them
        /// out of that folder into root. GDAL and OGR are then registered.
        /// </summary>
        public static void CustomGdalConfigure()
        {
            // Get current folder of assembly cache
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Construct expected gdal folder that is copied on run
            string gdalFolder = Path.Join(assemblyFolder, "gdal");

            // Move all files within gdal folder to root of assembly cache
            foreach (var file in Directory.GetFiles(gdalFolder))
            {
                File.Move(file, Path.Combine(assemblyFolder, Path.GetFileName(file)));
            }

            // Register Gdal and Ogr now that DLLs are in assembly cache
            OSGeo.GDAL.Gdal.AllRegister();
            OSGeo.OGR.Ogr.RegisterAll();
        }
        
        /// <summary>
        /// Extract bounding box from a graphics layer and project to EPSG code.
        /// </summary>
        public static async Task<double[]> GraphicToBoundingBoxAsync(GraphicsLayer layer, int epsg)
        {
            // TODO: check epsg is not null

            Envelope envelope = await QueuedTask.Run(() =>
            {
                // Get the spatial envelope and reference of selected grahicsLayer
                Envelope envelopeIn = layer.QueryExtent();
                SpatialReference srsIn = envelopeIn.SpatialReference;

                // Project envelope into requested epsg
                SpatialReference srsOut = SpatialReferenceBuilder.CreateSpatialReference(epsg);
                ProjectionTransformation prjOut = ProjectionTransformation.Create(srsIn, srsOut);
                Envelope envelopeOut = GeometryEngine.Instance.ProjectEx(envelopeIn, prjOut) as Envelope;

                return envelopeOut;
            });

            // Unpack projected envelope to south-west, north-east points
            double[] bbox = new double[]
            {
                    envelope.XMin,
                    envelope.YMin,
                    envelope.XMax,
                    envelope.YMax
            };

            return bbox;
        }
    
        /// <summary>
        /// 
        /// </summary>
        /// <param name="inBoundingBox"></param>
        /// <param name="inEpsg"></param>
        /// <param name="outEpsg"></param>
        /// <returns></returns>
        public static double[] ReprojectBoundingBox(double[] inBoundingBox, int inEpsg, int outEpsg)
        {
            // Check bounding box
            if (inBoundingBox == null)
            {
                return null;
            }

            // Unpack bbox coordinates
            double minY = inBoundingBox[0];
            double minX = inBoundingBox[1];
            double maxY = inBoundingBox[2];
            double maxX = inBoundingBox[3];

            // Check epsg parameters
            if (inEpsg == null || outEpsg == null) {
                return null;
            }

            // Set input bbox epsg
            OSGeo.OSR.SpatialReference inSrs = new OSGeo.OSR.SpatialReference(null);
            inSrs.ImportFromEPSG(inEpsg);

            // Set output bbox epsg
            OSGeo.OSR.SpatialReference outSrs = new OSGeo.OSR.SpatialReference(null);
            outSrs.ImportFromEPSG(outEpsg);

            // Initialise transformer
            OSGeo.OSR.CoordinateTransformation transformer = new OSGeo.OSR.CoordinateTransformation(inSrs, outSrs);

            // Initialise output bbox and project coordinates to output
            double[] outBoundingBox = new double[4] { 0, 0, 0, 0 };
            transformer.TransformBounds(outBoundingBox, minX, minY, maxX, maxY, 0);

            return outBoundingBox;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static int GetMaskBandIndex(OSGeo.GDAL.Dataset ds)
        {
            int maskbandIndex = -1; // TODO: this isnt optimal
            for (int b = 1; b <= ds.RasterCount; b++)
            {
                string bandName = ds.GetRasterBand(b).GetDescription();
                if (bandName == "oa_fmask")
                {
                    maskbandIndex = b;
                }
            }
            
            return maskbandIndex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        public static Dictionary<string, int> GetBandIndexMap(OSGeo.GDAL.Dataset ds)
        {
            if (ds.RasterCount == 0)
            {
                return null;
            }

            Dictionary<string, int> bandIndexMap = new Dictionary<string, int>();
            for (int b = 1; b <= ds.RasterCount; b++)
            {
                string bandName = ds.GetRasterBand(b).GetDescription();
                if (bandName == null)
                {
                    bandName = $"unknown_{b}";
                }

                bandIndexMap.Add(bandName, b);
            }

            return bandIndexMap;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexBlock"></param>
        /// <param name="blueBlock"></param>
        /// <param name="redBlock"></param>
        /// <param name="nirBlock"></param>
        /// <param name="maskBlock"></param>
        /// <param name="validPixels"></param>
        /// <param name="noDataValue"></param>
        /// <returns></returns>
        public static float[] EVI(float[] indexBlock, short[] blueBlock, short[] redBlock, short[] nirBlock, short[] maskBlock, List<int> validPixels, float noDataValue)
        {
            for (int i = 0; i < maskBlock.Length; i++)
            {
                if (!validPixels.Contains(maskBlock[i]))
                {
                    indexBlock[i] = float.Epsilon; // TODO take user value
                }
                else if (blueBlock[i] < 0F || redBlock[i] < 0F || nirBlock[i] < 0F)
                {
                    indexBlock[i] = float.Epsilon;
                }
                else
                {
                    float blue = (float)blueBlock[i];
                    float red = (float)redBlock[i];
                    float nir = (float)nirBlock[i];
                    indexBlock[i] = (2.5F * (nir - red)) / (nir + 6F * red - 7.5F * blue + 1F);
                }
            }

            return indexBlock;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexBlock"></param>
        /// <param name="blueBlock"></param>
        /// <param name="redBlock"></param>
        /// <param name="nirBlock"></param>
        /// <param name="maskBlock"></param>
        /// <param name="validPixels"></param>
        /// <param name="noDataValue"></param>
        /// <returns></returns>
        public static float[] LAI(float[] indexBlock, short[] blueBlock, short[] redBlock, short[] nirBlock, short[] maskBlock, List<int> validPixels, float noDataValue)
        {
            for (int i = 0; i < maskBlock.Length; i++)
            {
                if (!validPixels.Contains(maskBlock[i]))
                {
                    indexBlock[i] = float.Epsilon; // TODO take user value
                }
                else if (blueBlock[i] < 0F || redBlock[i] < 0F || nirBlock[i] < 0F)
                {
                    indexBlock[i] = float.Epsilon;
                }
                else
                {
                    float blue = (float)blueBlock[i];
                    float red = (float)redBlock[i];
                    float nir = (float)nirBlock[i];
                    indexBlock[i] = 3.618F * ((2.5F * (nir - red)) / (nir + 6F * red - 7.5F * blue + 1F)) - 0.118F;
                }
            }

            return indexBlock;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexBlock"></param>
        /// <param name="redBlock"></param>
        /// <param name="nirBlock"></param>
        /// <param name="maskBlock"></param>
        /// <param name="validPixels"></param>
        /// <param name="noDataValue"></param>
        /// <returns></returns>
        public static float[] MSAVI(float[] indexBlock, short[] redBlock, short[] nirBlock, short[] maskBlock, List<int> validPixels, float noDataValue)
        {
            for (int i = 0; i < maskBlock.Length; i++)
            {
                if (!validPixels.Contains(maskBlock[i]))
                {
                    indexBlock[i] = float.Epsilon; // TODO take user value
                }
                else if (redBlock[i] < 0F || nirBlock[i] < 0F)
                {
                    indexBlock[i] = float.Epsilon;
                }
                else
                {
                    float red = (float)redBlock[i];
                    float nir = (float)nirBlock[i];
                    indexBlock[i] =  (2F * nir + 1F - (float)Math.Pow(((float)Math.Pow(2F * nir + 1F, 2) - 8F * (nir - red)), 0.5F)) / 2F;
                }
            }

            return indexBlock;
        }

        /// <summary>
        /// Calculate NDVI index provided a red and nir array obtained from a ratser band.
        /// </summary>
        /// <param name="indexBlock"></param>
        /// <param name="redBlock"></param>
        /// <param name="nirBlock"></param>
        /// <param name="maskBlock"></param>
        /// <param name="validPixels"></param>
        /// <param name="noDataValue"></param>
        /// <returns></returns>
        public static float[] NDVI(float[] indexBlock, short[] redBlock, short[] nirBlock, short[] maskBlock, List<int> validPixels, float noDataValue)
        {
            for (int i = 0; i < maskBlock.Length; i++)
            {
                if (!validPixels.Contains(maskBlock[i]))
                {
                    indexBlock[i] = float.Epsilon; // TODO take user value
                }
                else if (redBlock[i] < 0F || nirBlock[i] < 0F)
                {
                    indexBlock[i] = float.Epsilon;
                }
                else
                {
                    float red = (float)redBlock[i];
                    float nir = (float)nirBlock[i];
                    indexBlock[i] = (nir - red) / (nir + red);
                }
            }

            return indexBlock;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indexBlock"></param>
        /// <param name="redBlock"></param>
        /// <param name="nirBlock"></param>
        /// <param name="maskBlock"></param>
        /// <param name="validPixels"></param>
        /// <param name="noDataValue"></param>
        /// <returns></returns>
        public static float[] kNDVI(float[] indexBlock, short[] redBlock, short[] nirBlock, short[] maskBlock, List<int> validPixels, float noDataValue)
        {
            for (int i = 0; i < maskBlock.Length; i++)
            {
                if (!validPixels.Contains(maskBlock[i]))
                {
                    indexBlock[i] = float.Epsilon; // TODO take user value
                }
                else if (redBlock[i] < 0F || nirBlock[i] < 0F)
                {
                    indexBlock[i] = float.Epsilon;
                }
                else
                {
                    float red = (float)redBlock[i];
                    float nir = (float)nirBlock[i];                   
                    indexBlock[i] = (float)Math.Tanh((float)Math.Pow((nir - red) / (nir + red), 2F));
                }
            }

            return indexBlock;
        }
    }
}
