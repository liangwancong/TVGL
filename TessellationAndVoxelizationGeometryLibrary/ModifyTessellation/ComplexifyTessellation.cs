﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 03-05-2015
//
// Last Modified By : Matt Campbell
// Last Modified On : 05-28-2016
// ***********************************************************************
// <copyright file="ComplexifyTessellation.cs" company="Design Engineering Lab">
//     Copyright ©  2014
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Linq;

namespace TVGL
{
    /// <summary>
    ///  This portion of ModifyTessellation includes the functions to complexify a solid, which means 
    ///  adding more elements to it. 
    /// </summary>
    public static partial class ModifyTessellation
    {
        /// <summary>
        /// Complexifies the model by splitting the any edges that are half or more than the longest edge. 
        /// </summary>
        /// <param name="ts">The ts.</param>
        public static void Complexify(this TessellatedSolid ts, bool usePrimitives = true)
        {
            Complexify(ts, ts.NumberOfFaces / 2, ts.Edges.Max(x => x.Length) * 0.5, usePrimitives);
        }
        /// <summary>
        /// Complexifies the tessellation by adding more faces of the provided number.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="numberOfNewFaces">The number of faces.</param>
        public static void Complexify(this TessellatedSolid ts, int numberOfNewFaces, bool usePrimitives = true)
        {
            Complexify(ts, numberOfNewFaces, 0.0, usePrimitives);
        }

        /// <summary>
        /// Complexifies the tessellation so that no edge is longer than provided the maximum edge length.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="maxLength">The tolerance.</param>
        public static void Complexify(this TessellatedSolid ts, double maxLength, bool usePrimitives = true)
        {
            Complexify(ts, -1, maxLength, usePrimitives);
        }


        /// <summary>
        /// Complexifies the tessellation so that no edge is longer than provided the maximum edge length
        /// or for adding the provided number of faces - whichever comes first
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="numberOfFaces">The number of new faces to add.</param>
        /// <param name="maxLength">The maximum length.</param>
        public static void Complexify(TessellatedSolid ts, int numberOfFaces, double maxLength, bool usePrimitives = true)
        {
            var sortedEdges = new SortedSet<Edge>(ts.Edges, new SortByLength(false));
            var addedEdges = new List<Edge>();
            var addedVertices = new List<Vertex>();
            var addedFaces = new List<PolygonalFace>();
            List<PrimitiveSurface> primitives = new List<PrimitiveSurface>();
            if (usePrimitives && (ts.Primitives == null || !ts.Primitives.Any()))
                ts.ClassifyPrimitiveSurfaces();
            usePrimitives = usePrimitives && ts.Primitives.Any();
            var edge = sortedEdges.First();
            var iterations = numberOfFaces > 0 ? (int)Math.Ceiling(numberOfFaces / 2.0) : numberOfFaces;
            while (iterations != 0 && edge.Length >= maxLength)
            {
                sortedEdges.Remove(edge);
                var origLeftFace = edge.OtherFace;
                var origRightFace = edge.OwnedFace;
                var leftFarVertex = origLeftFace.OtherVertex(edge);
                var rightFarVertex = origRightFace.OtherVertex(edge);
                var fromVertex = edge.From;
                var toVertex = edge.To;
                double[] position;
                if (usePrimitives)
                {
                    primitives.Clear();
                    if (origLeftFace.BelongsToPrimitive != null &&
                        origLeftFace.BelongsToPrimitive.Type != PrimitiveSurfaceType.Dense)
                        primitives.Add(origLeftFace.BelongsToPrimitive);
                    if (origRightFace.BelongsToPrimitive != null &&
                        origRightFace.BelongsToPrimitive.Type != PrimitiveSurfaceType.Dense
                        && origRightFace.BelongsToPrimitive != origLeftFace.BelongsToPrimitive)
                        primitives.Add(origRightFace.BelongsToPrimitive);
                }
                if (DetermineIntermediateVertexPosition(edge, out position, primitives))
                {
                    iterations--;
                    var addedVertex = new Vertex(position);
                    // modify original faces with new intermediate vertex
                    var index = origLeftFace.VertexIndex(toVertex);
                    origLeftFace.Vertices[index] = addedVertex;
                    origLeftFace.Update();
                    addedVertex.Faces.Add(origLeftFace);
                    index = origRightFace.VertexIndex(toVertex);
                    origRightFace.Vertices[index] = addedVertex;
                    origRightFace.Update();
                    addedVertex.Faces.Add(origRightFace);
                    var newLeftFace =
                        new PolygonalFace(new[] { toVertex, addedVertex, leftFarVertex }, true)
                        {
                            BelongsToPrimitive = origLeftFace.BelongsToPrimitive,
                            Color = new Color(origLeftFace.Color.ToString())
                        };
                    var newRightFace = new PolygonalFace(new[] { addedVertex, toVertex, rightFarVertex }, true)
                    {
                        BelongsToPrimitive = origRightFace.BelongsToPrimitive,
                        Color = new Color(origRightFace.Color.ToString())
                    };
                    if (origLeftFace.BelongsToPrimitive != null)
                    {
                        origLeftFace.BelongsToPrimitive.RemoveFace(origLeftFace);
                        origLeftFace.BelongsToPrimitive.UpdateWith(origLeftFace);
                        newLeftFace.BelongsToPrimitive.UpdateWith(newLeftFace);
                    }
                    if (origRightFace.BelongsToPrimitive != null)
                    {
                        origRightFace.BelongsToPrimitive.RemoveFace(origRightFace);
                        origRightFace.BelongsToPrimitive.UpdateWith(origRightFace);
                        newRightFace.BelongsToPrimitive.UpdateWith(newRightFace);
                    }
                    toVertex.Faces.Remove(origLeftFace);
                    toVertex.Faces.Remove(origRightFace);

                    var inlineEdge = new Edge(addedVertex, toVertex, newRightFace, newLeftFace, true);
                    toVertex.Edges.Remove(edge);
                    edge.To = addedVertex;
                    addedVertex.Edges.Add(edge);
                    edge.Update();
                    var newLeftEdge = new Edge(leftFarVertex, addedVertex, origLeftFace, newLeftFace, true);
                    var newRightEdge = new Edge(rightFarVertex, addedVertex, newRightFace, origRightFace, true);
                    origLeftFace.AddEdge(newLeftEdge);
                    origRightFace.AddEdge(newRightEdge);
                    var bottomEdge = toVertex.Edges.First(e => e.OtherVertex(toVertex) == leftFarVertex);
                    if (bottomEdge.OwnedFace == origLeftFace)
                        bottomEdge.OwnedFace = newLeftFace;
                    else bottomEdge.OtherFace = newLeftFace;
                    newLeftFace.AddEdge(bottomEdge);
                    bottomEdge.Update();

                    bottomEdge = toVertex.Edges.First(e => e.OtherVertex(toVertex) == rightFarVertex);
                    if (bottomEdge.OwnedFace == origRightFace)
                        bottomEdge.OwnedFace = newRightFace;
                    else bottomEdge.OtherFace = newRightFace;
                    newRightFace.AddEdge(bottomEdge);
                    bottomEdge.Update();


                    // need to re-add the edge. It was modified in the SplitEdge function (now, half the lenght), but
                    // it may still be met by this criteria
                    sortedEdges.Add(edge);
                    sortedEdges.Add(inlineEdge);
                    addedEdges.Add(inlineEdge);
                    sortedEdges.Add(newLeftEdge);
                    addedEdges.Add(newLeftEdge);
                    sortedEdges.Add(newRightEdge);
                    addedEdges.Add(newRightEdge);
                    addedFaces.Add(newLeftFace);
                    addedFaces.Add(newRightFace);
                    addedVertices.Add(addedVertex);
                }
                edge = sortedEdges.First();
            }
            ts.AddVertices(addedVertices);
            ts.AddEdges(addedEdges);
            ts.AddFaces(addedFaces);
        }
    }

}