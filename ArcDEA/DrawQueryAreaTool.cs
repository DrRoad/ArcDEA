using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcDEA
{
    internal class DrawQueryAreaTool : MapTool
    {
        public DrawQueryAreaTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Rectangle;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override Task OnToolActivateAsync(bool active)
        {
            // TODO: change mouse cursor to box selection icon
            return base.OnToolActivateAsync(active);
        }

        protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            // Get active mapview
            MapView map = MapView.Active;

            if (map != null)
            {
                // Prepare unique graphic name
                string name = DateTime.Now.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss");

                // Create and set graphics layer parameters
                GraphicsLayerCreationParams graphicParams = new GraphicsLayerCreationParams();
                graphicParams.Name = "ArcDEA Query Area" + " " + "(" + name + ")";

                // Set graphic stroke symbology
                CIMStroke stroke = SymbolFactory.Instance.ConstructStroke(
                    color: ColorFactory.Instance.RedRGB,
                    width: 1.5,
                    lineStyle: SimpleLineStyle.Dash
                    );

                // Set polygon symbology with stroke from above
                CIMPolygonSymbol symbology = SymbolFactory.Instance.ConstructPolygonSymbol(
                    color: ColorFactory.Instance.RedRGB,
                    fillStyle: SimpleFillStyle.Null,
                    outline: stroke
                    );

                await QueuedTask.Run(() =>
                {
                    // Create graphics layer and set geometry extent to graphic extent with symbology
                    GraphicsLayer graphicLayer = LayerFactory.Instance.CreateLayer<GraphicsLayer>(graphicParams, map.Map);
                    graphicLayer.AddElement(geometry.Extent, symbology);

                    // On completion, change to navigation tool
                    FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
                });
            }

            //return base.OnSketchCompleteAsync(geometry);
            return true;
        }

        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            return base.OnToolDeactivateAsync(hasMapViewChanged);
        }
    }
}
