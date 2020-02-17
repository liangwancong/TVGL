﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 03-05-2015
//
// Last Modified By : Matt Campbell
// Last Modified On : 05-28-2016
// ***********************************************************************
// <copyright file="RefineTessellation.cs" company="Design Engineering Lab">
//     Copyright ©  2014
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using TVGL.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace TVGL
{
    /// <summary>
    ///  This portion of ModifyTessellation includes the functions to refine a solid, which means 
    ///  adding more elements to it. invoked during the opening of a tessellated solid from "disk", but the repair function
    ///  may be called on its own.
    /// </summary>
    public static partial class ModifyTessellation
    {


        /// <summary>
        ///     Adjusts the position of kept vertex.
        /// </summary>
        /// <param name="vertexA">The keep vertex.</param>
        /// <param name="vertexB">The other vertex.</param>
        internal static Vector2 DetermineIntermediateVertexPosition(Vertex vertexA, Vertex vertexB)
        {
            //average positions
            var newPosition = vertexA.Position + vertexB.Position;
            return newPosition.Divide(2);
        }

        /// <summary>
        ///     Adjusts the position of kept vertex experimental.
        /// </summary>
        /// <param name="keepVertex">The keep vertex.</param>
        /// <param name="removedVertex">The removed vertex.</param>
        /// <param name="removeFace1">The remove face1.</param>
        /// <param name="removeFace2">The remove face2.</param>
        internal static void AdjustPositionOfKeptVertexExperimental(Vertex keepVertex, Vertex removedVertex,
            PolygonalFace removeFace1, PolygonalFace removeFace2)
        {
            //average positions
            var newPosition = keepVertex.Position + removedVertex.Position;
            var radius = keepVertex.Position.Subtract(removedVertex.Position).norm2() / 2.0;
            keepVertex.Position = newPosition.Divide(2);
            var avgNormal = (removeFace1.Normal + removeFace2.Normal).Normalize();
            var otherVertexAvgDistanceToEdgePlane =
                keepVertex.Edges.Select(e => e.OtherVertex(keepVertex).Position.Dot(avgNormal)).Sum() /
                (keepVertex.Edges.Count - 1);
            var distanceOfEdgePlane = keepVertex.Position.Dot(avgNormal);

            // use a sigmoid function to determine how far out to move the vertex
            var x = 0.05 * (distanceOfEdgePlane - otherVertexAvgDistanceToEdgePlane) / radius;
            var length = 2 * radius * x / Math.Sqrt(1 + x * x) - radius;
            keepVertex.Position = keepVertex.Position + (avgNormal * length);
        }
    }
}