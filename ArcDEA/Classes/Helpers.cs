using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
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
                { new AssetIndexItem("NDVI", "Normalised Difference Vegetation Index", new List<string> { "nbart_red", "nbart_nir" }, false) },
                { new AssetIndexItem("SLAVI", "Specific Leaf Area Vegetation Index", new List<string> { "nbart_red", "nbart_nir", "nbart_swir_2" }, false) }
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
    
        //
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
    }
}
