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
    }
}
