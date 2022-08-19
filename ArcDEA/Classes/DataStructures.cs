using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcDEA.Classes
{
    internal class DataStructures
    {
        // TODO: comment
        public class CollectionItem
        {
            public string RawName { get; set; }
            public string CleanName { get; set; }

            public CollectionItem(string rawName, string cleanName)
            {
                RawName = rawName;
                CleanName = cleanName;
            }
        }

        // TODO: comment
        public class AssetItem
        {
            public string RawName { get; set; }
            public string CleanName { get; set; }
            public bool IsAssetSelected { get; set; }

            public AssetItem(string rawName, string cleanName, bool isAssetSelected)
            {
                RawName = rawName;
                CleanName = cleanName;
                IsAssetSelected = isAssetSelected;
            }
        }
    }
}
