﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 04-18-2016
//
// Last Modified By : Design Engineering Lab
// Last Modified On : 04-26-2016
// ***********************************************************************
// <copyright file="MiscFunctions.cs" company="Design Engineering Lab">
//     Copyright ©  2014
// </copyright>
// <summary></summary>
// ***********************************************************************

using MIConvexHull;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TVGL.Numerics;
using TVGL.TwoDimensional;
using TVGL.Voxelization;

namespace TVGL
{
    /// <summary>
    ///     Miscellaneous Functions for TVGL
    /// </summary>
    public static class MiscFunctions
    {
        #region Sort Along Direction

        /// <summary>
        ///     Returns a list of sorted locations along a set direction. 
        /// </summary>
        /// <param name="direction">The directions.</param>
        /// <param name="vertices">The locations.</param>
        /// <param name="sortedVertices">The sorted locations.</param>
        public static void SortAlongDirection(Vector3 direction, IEnumerable<Vertex> vertices,
            out List<(Vertex, double)> sortedVertices)
        {
            //Get integer values for every vertex as distance along direction
            //Split positive and negative numbers into seperate lists. 0 is 
            //considered positive.
            //This is an O(n) preprocessing step
            var vertexDistances = GetVertexDistances(direction, vertices);

            //Unsure what time domain this sort function uses. Note, however, rounding allows using the same
            //toleranceForCombiningPoints as the "isNeglible" star math function 
            sortedVertices = vertexDistances.OrderBy(p => p.Item2).ToList();
        }


        /// <summary>
        ///     Returns a list of sorted locations along a set direction. 
        /// </summary>
        /// <param name="direction">The directions.</param>
        /// <param name="vertices">The locations.</param>
        /// <param name="sortedVertices">The sorted locations.</param>
        public static void SortAlongDirection(Vector3 direction, IEnumerable<Vector3> vertices,
            out List<(Vector3, double)> sortedVertices)
        {
            //Get integer values for every vertex as distance along direction
            //Split positive and negative numbers into seperate lists. 0 is 
            //considered positive.
            //This is an O(n) preprocessing step
            var vertexDistances = GetVertexDistances(direction, vertices);

            //Unsure what time domain this sort function uses. Note, however, rounding allows using the same
            //toleranceForCombiningPoints as the "isNeglible" star math function 
            sortedVertices = vertexDistances.OrderBy(p => p.Item2).ToList();
        }

        private static IEnumerable<(Vertex, double)> GetVertexDistances(Vector3 direction, IEnumerable<Vertex> vertices,
            double sameTolerance = Constants.BaseTolerance)
        {
            var vertexDistances = new List<(Vertex, double)>(vertices.Count());
            var numDecimalPoints = 0;
            while (Math.Round(sameTolerance, numDecimalPoints).IsPracticallySame(0.0)) numDecimalPoints++;
            foreach (var vertex in vertices)
            {
                //Get distance along the search direction
                var d = Math.Round(direction.Dot(vertex.Coordinates), numDecimalPoints);
                vertexDistances.Add((vertex, d));
            }
            return vertexDistances;
        }

        private static IEnumerable<(Vector3, double)> GetVertexDistances(Vector3 direction, IEnumerable<Vector3> vertices,
            double sameTolerance = Constants.BaseTolerance)
        {
            var vertexDistances = new List<(Vector3, double)>(vertices.Count());
            var numDecimalPoints = 0;
            while (Math.Round(sameTolerance, numDecimalPoints).IsPracticallySame(0.0)) numDecimalPoints++;
            foreach (var vertex in vertices)
            {
                //Get distance along the search direction
                var d = Math.Round(direction.Dot(vertex), numDecimalPoints);
                vertexDistances.Add((vertex, d));
            }
            return vertexDistances;
        }

        /// <summary>
        ///     Returns a list of sorted Vector2s along a set direction. 
        /// </summary>
        /// <param name="direction">The directions.</param>
        /// <param name="points"></param>
        /// <param name="sortedPoints"></param>
        public static void SortAlongDirection(Vector2 direction, IList<Vector2> points,
               out List<(Vector2, double)> sortedPoints, int numDecimals = -1)
        {
            var distances = GetPointDistances(direction, points, numDecimals);
            sortedPoints = distances.OrderBy(p => p.Item2).ToList();
        }

        /// <summary>
        ///     Returns a list of sorted Vector2s along a set direction. 
        /// </summary>
        /// <param name="direction">The directions.</param>
        /// <param name="points"></param>
        /// <param name="sortedPoints"></param>
        public static void SortAlongDirection(Vector2 direction, IList<Vector2> points,
               out List<Vector2> sortedPoints, int numDecimals = -1)
        {
            var distances = GetPointDistances(direction, points, numDecimals);
            sortedPoints = distances.OrderBy(p => p.Item2).Select(p => p.Item1).ToList();
        }

        private static (Vector2, double)[] GetPointDistances(Vector2 direction,
            IList<Vector2> points, int numDecimals = -1)
        {
            var distances = new (Vector2, double)[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                //Get distance along the search direction with accuracy to the 15th decimal place
                var d = direction.Dot(point);
                if (numDecimals > 0)
                    d = Math.Round(d, numDecimals); //2D dot product
                distances[i] = (point, d);
            }
            return distances;
        }


        #endregion

        #region Perimeter

        /// <summary>
        /// Gets the Perimeter (length of a locations) of a 3D set of Vertices.
        /// </summary>
        /// <param name="polygon3D"></param>
        /// <returns></returns>
        public static double Perimeter(IList<Vertex> polygon3D)
        {
            double perimeter = Vector3.Distance(polygon3D.Last().Coordinates, polygon3D[0].Coordinates);
            for (var i = 1; i < polygon3D.Count; i++)
                perimeter += Vector3.Distance(polygon3D[i - 1].Coordinates, polygon3D[i].Coordinates);
            return perimeter;
        }
        #endregion

        #region Dealing with Flat Patches
        /// <summary>
        /// Gets a collection of faces with distinct normals. These are the largest faces within the set with common normal. 
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="tolerance">The toleranceForCombiningPoints.</param>
        /// <param name="removeOpposites">if set to <c>true</c> [remove opposites].</param>
        /// <returns>List&lt;PolygonalFace&gt;.</returns>
        public static List<PolygonalFace> FacesWithDistinctNormals(IEnumerable<PolygonalFace> faces,
            double tolerance = Constants.SameFaceNormalDotTolerance, bool removeOpposites = true)
        {
            // This is done by sorting the normals first by the x-component, then by the y and then the z. 
            // This is to avoid the O(n^2) and be more like O(n). It is a necessary but not sufficient
            // condition that faces with similar x-values in the normal (and then y and then z) will
            // likely be the same normal. So, in this manner we can then check adjacent faces in a sorted
            // set. However, sorting just in x alone may not be sufficient as the list may jump around. 
            // For example, a portion of the list may look like: { ... [0 .3 .4], [0 -.3 .4], [0, .29999, .4] }
            // comparing adjacent pairs will miss the fact that 1 and 3 and similar. But - since they have the
            // same x-component as 2, then they are not compared. Here, the chance to catch such cases by sorting
            // about all 3 cardinal directions. One could continue sorting by a dot-product with an arbitrary normal,
            // but cases where this function have failed have not been observed.
            var distinctList = faces.ToList();
            for (int k = 0; k < 3; k++)
            {
                distinctList = distinctList.OrderBy(f => f.Normal[k]).ToList();
                for (var i = distinctList.Count - 1; i > 0; i--)
                {
                    if (distinctList[i].Normal.Dot(distinctList[i - 1].Normal).IsPracticallySame(1.0, tolerance) ||
                        (removeOpposites && distinctList[i].Normal.Dot(distinctList[i - 1].Normal).IsPracticallySame(-1, tolerance)))
                    {
                        if (distinctList[i].Area <= distinctList[i - 1].Area) distinctList.RemoveAt(i);
                        else distinctList.RemoveAt(i - 1);
                    }
                }
            }
            return distinctList;
        }

        /// <summary>
        ///     Gets a list of flats for a given list of faces.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="tolerance">The toleranceForCombiningPoints.</param>
        /// <param name="minSurfaceArea">The minimum surface area.</param>
        /// <returns>List&lt;Flat&gt;.</returns>
        public static List<Flat> FindFlats(IList<PolygonalFace> faces, double tolerance = Constants.ErrorForFaceInSurface,
               int minNumberOfFacesPerFlat = 2)
        {
            //Note: This function has been optimized to run very fast for large amount of faces
            //Used hashset for "Contains" function calls 
            var usedFaces = new HashSet<PolygonalFace>();
            var listFlats = new List<Flat>();

            //Use an IEnumerable class (List) for iterating through each part, and then the 
            //"Contains" function to see if it was already used. This is actually much faster
            //than using a while locations with a ".Any" and ".First" call on the Hashset.
            foreach (var startFace in faces)
            {
                //If this faces has already been used, continue to the next face
                if (usedFaces.Contains(startFace)) continue;
                //Get all the faces that should be used on this flat
                //Use a hashset so we can use the ".Contains" function
                var flatHashSet = new HashSet<PolygonalFace> { startFace };
                var flat = new Flat(flatHashSet) { Tolerance = tolerance };
                //Stacks are fast for "Push" and "Pop".
                //Add all the adjecent faces from the first face to the stack for 
                //consideration in the while locations below.
                var stack = new Stack<PolygonalFace>(flatHashSet);
                var reDefineFlat = 3;
                while (stack.Any())
                {
                    var newFace = stack.Pop();
                    //Add new adjacent faces to the stack for consideration
                    //if the faces are already listed in the flat faces, the first
                    //"if" statement in the while locations will ignore them.
                    foreach (var adjacentFace in newFace.AdjacentFaces)
                    {
                        if (adjacentFace == null) continue;
                        if (!flatHashSet.Contains(adjacentFace) && !usedFaces.Contains(adjacentFace) &&
                            !stack.Contains(adjacentFace) && flat.IsNewMemberOf(adjacentFace))
                        {
                            // flat.UpdateWith(adjacentFace);
                            flatHashSet.Add(newFace);
                            if (flatHashSet.Count >= reDefineFlat)
                            {
                                flat = new Flat(flatHashSet);
                                reDefineFlat *= 3;
                            }
                            stack.Push(adjacentFace);
                        }
                    }
                }
                flat = new Flat(flatHashSet);
                //Criteria of whether it should be a flat should be inserted here.
                if (flat.Faces.Count >= minNumberOfFacesPerFlat)
                    listFlats.Add(flat);
                foreach (var polygonalFace in flat.Faces)
                    usedFaces.Add(polygonalFace);
            }
            return listFlats;
        }
        #endregion


        /// <summary>
        ///     Calculate the area of any non-intersecting polygon in 3D space (loops)
        ///     This is faster than projecting to a 2D surface first in a seperate function.
        /// </summary>
        /// <param name="loop"></param>
        /// <param name="normal">The normal.</param>
        /// <returns>System.Double.</returns>
        /// <references>http://geomalgorithms.com/a01-_area.html </references>
        public static double AreaOf3DPolygon(IEnumerable<Vertex> loop, Vector3 normal)
        {
            var ax = Math.Abs(normal.X);
            var ay = Math.Abs(normal.Y);
            var az = Math.Abs(normal.Z);

            //Make a new list from the locations
            var vertices = new List<Vertex>(loop);
            //Add the first vertex to the end
            vertices.Add(vertices.First());

            //Choose the largest abs coordinate to ignore for projections
            var coord = 3; //ignore z-coord
            if (ax > az && (ax > ay || ax.IsPracticallySame(ay))) coord = 1; //ignore x-coord
            else if (ay > az && ay > ax) coord = 2; //ignore y-coord
                                                    //These are the results for eqaul directions
                                                    //if az == ax, then ignore z-coord.
                                                    //if az == ax == ay, then ignore z-coord.
                                                    //if ax == ay and both are greater than az, ignore the x-coord

            // compute area of the 2D projection
            // -1 so as to not include the vertex that was added to the end of the list
            var n = vertices.Count - 1;
            var area = 0.0;
            int i;
            switch (coord)
            {
                case 1:
                    for (i = 1; i < n; i++)
                        area += vertices[i].Y * (vertices[i + 1].Z - vertices[i - 1].Z);
                    break;
                case 2:
                    for (i = 1; i < n; i++)
                        area += vertices[i].Z * (vertices[i + 1].X - vertices[i - 1].X);
                    break;
                case 3:
                    for (i = 1; i < n; i++)
                        area += vertices[i].X * (vertices[i + 1].Y - vertices[i - 1].Y);
                    break;
            }
            switch (coord)
            {
                case 1:
                    area += vertices[n].Y * (vertices[1].Z - vertices[n - 1].Z);
                    break;
                case 2:
                    area += vertices[n].Z * (vertices[1].X - vertices[n - 1].X);
                    break;
                case 3:
                    area += vertices[n].X * (vertices[1].Y - vertices[n - 1].Y);
                    break;
            }

            // scale to get area before projection
            var an = Math.Sqrt(ax * ax + ay * ay + az * az); // length of normal vector
            switch (coord)
            {
                case 1:
                    area *= an / (2 * normal.X);
                    break;
                case 2:
                    area *= an / (2 * normal.Y);
                    break;
                case 3:
                    area *= an / (2 * normal.Z);
                    break;
            }
            return area;
        }


        /// <summary>
        ///     Calculate the area of any non-intersecting polygon in 3D space (loops)
        ///     This is faster than projecting to a 2D surface first in a seperate function.
        /// </summary>
        /// <param name="loop"></param>
        /// <param name="normal">The normal.</param>
        /// <returns>System.Double.</returns>
        /// <references>http://geomalgorithms.com/a01-_area.html </references>
        public static double AreaOf3DPolygon(IEnumerable<Vector3> loop, Vector3 normal)
        {
            var ax = Math.Abs(normal.X);
            var ay = Math.Abs(normal.Y);
            var az = Math.Abs(normal.Z);

            //Make a new list from the locations
            var vertices = new List<Vector3>(loop);
            //Add the first vertex to the end
            vertices.Add(vertices.First());

            //Choose the largest abs coordinate to ignore for projections
            var coord = 3; //ignore z-coord
            if (ax > az && (ax > ay || ax.IsPracticallySame(ay))) coord = 1; //ignore x-coord
            else if (ay > az && ay > ax) coord = 2; //ignore y-coord
                                                    //These are the results for eqaul directions
                                                    //if az == ax, then ignore z-coord.
                                                    //if az == ax == ay, then ignore z-coord.
                                                    //if ax == ay and both are greater than az, ignore the x-coord

            // compute area of the 2D projection
            // -1 so as to not include the vertex that was added to the end of the list
            var n = vertices.Count - 1;
            var area = 0.0;
            int i;
            switch (coord)
            {
                case 1:
                    for (i = 1; i < n; i++)
                        area += vertices[i].Y * (vertices[i + 1].Z - vertices[i - 1].Z);
                    break;
                case 2:
                    for (i = 1; i < n; i++)
                        area += vertices[i].Z * (vertices[i + 1].X - vertices[i - 1].X);
                    break;
                case 3:
                    for (i = 1; i < n; i++)
                        area += vertices[i].X * (vertices[i + 1].Y - vertices[i - 1].Y);
                    break;
            }
            switch (coord)
            {
                case 1:
                    area += vertices[n].Y * (vertices[1].Z - vertices[n - 1].Z);
                    break;
                case 2:
                    area += vertices[n].Z * (vertices[1].X - vertices[n - 1].X);
                    break;
                case 3:
                    area += vertices[n].X * (vertices[1].Y - vertices[n - 1].Y);
                    break;
            }

            // scale to get area before projection
            var an = Math.Sqrt(ax * ax + ay * ay + az * az); // length of normal vector
            switch (coord)
            {
                case 1:
                    area *= an / (2 * normal.X);
                    break;
                case 2:
                    area *= an / (2 * normal.Y);
                    break;
                case 3:
                    area *= an / (2 * normal.Z);
                    break;
            }
            return area;
        }

        #region Split Tesselated Solid into multiple solids if faces are disconnected 
        /// <summary>
        ///     Gets all the individual solids from a tesselated solid.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <returns>List&lt;TessellatedSolid&gt;.</returns>
        /// <exception cref="Exception"></exception>
        public static List<TessellatedSolid> GetMultipleSolids(TessellatedSolid ts)
        {
            var solids = new List<TessellatedSolid>();
            var seperateSolids = new List<List<PolygonalFace>>();
            var unusedFaces = ts.Faces.ToDictionary(face => face.IndexInList);
            while (unusedFaces.Any())
            {
                var faces = new HashSet<PolygonalFace>();
                var stack = new Stack<PolygonalFace>(new[] { unusedFaces.ElementAt(0).Value });
                while (stack.Any())
                {
                    var face = stack.Pop();
                    if (faces.Contains(face)) continue;
                    faces.Add(face);
                    unusedFaces.Remove(face.IndexInList);
                    foreach (var adjacentFace in face.AdjacentFaces)
                    {
                        if (adjacentFace == null) continue; //This is an error. Handle it in the error function.
                        stack.Push(adjacentFace);
                    }
                }
                seperateSolids.Add(faces.ToList());
            }
            var count = 0;
            if (seperateSolids.Count == 1)
            {
                solids.Add(ts);
                return solids;
            }
            foreach (var seperateSolid in seperateSolids)
            {
                solids.Add(new TessellatedSolid(seperateSolid));
                count += seperateSolid.Count;
            }
            return solids;
        }
        #endregion

        #region change 3D locations into 2D coordinates (e.g. Vector2's)
        /// <summary>
        /// Returns newly created 2D coordinates (of type Vector2) projected using the given transform.
        /// These coordinates do not contain references back to the original vertices but are lighter and
        /// quicker. This does not destructively alter the vertices.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="backTransform">The back transform.</param>
        /// <param name="toleranceForCombiningPoints">The tolerance for combining multiple vertices under a single point.
        /// If not, provided, then one point will be made for each vertex. If zero, then the coordinates will match at
        /// the 15 decimal place. Use a small positive number like 1e-9 to set a wider toleranceForCombiningPoints.</param>
        /// 
        /// <returns>System.Collections.Generic.IEnumerable&lt;TVGL.Numerics.Vector2&gt;.</returns>
        public static Dictionary<Vector2, List<T>> ProjectTo2DCoordinatesReturnDictionary<T>(this IEnumerable<T> vertices, Vector3 direction,
                    out Matrix4x4 backTransform, double toleranceForCombiningPoints = Constants.BaseTolerance) where T : IVertex3D
        {
            var transform = TransformToXYPlane(direction, out backTransform);
            return ProjectTo2DCoordinatesReturnDictionary(vertices, transform, toleranceForCombiningPoints);
        }

        /// <summary>
        /// Returns newly created 2D coordinates (of type Vector2) projected using the given transform.
        /// These coordinates do not contain references back to the original vertices but are lighter and
        /// quicker. This does not destructively alter the vertices.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="transform">The transform matrix.</param>
        /// <param name="toleranceForCombiningPoints">The tolerance for combining multiple vertices under a single point.
        /// If not, provided, then one point will be made for each vertex. If zero, then the coordinates will match at
        /// the 15 decimal place. Use a small positive number like 1e-9 to set a wider toleranceForCombiningPoints.</param>
        /// <param name="duplicateEntriesToMaintainPolygonalOrdering">Output is in the same order as input except when
        /// they are combined from the aforementioned tolerance. If this boolean is true then the output point may appear
        /// multiple times in the output collection to maintain the same order. This is useful if the original data is
        /// to define some polygon with order dictating the definition of edges.</param>
        /// 
        /// <returns>System.Collections.Generic.IEnumerable&lt;TVGL.Numerics.Vector2&gt;.</returns>
        public static Dictionary<Vector2, List<T>> ProjectTo2DCoordinatesReturnDictionary<T>(this IEnumerable<T> vertices, Matrix4x4 transform,
            double toleranceForCombiningPoints = Constants.BaseTolerance) where T : IVertex3D
        {
            var resultsDict = new Dictionary<Vector2, List<T>>();
            var numDecimalPoints = 0;
            while (numDecimalPoints <= 15 && Math.Round(toleranceForCombiningPoints, numDecimalPoints).IsPracticallySame(0.0))
                numDecimalPoints++;
            foreach (var vertex in vertices)
            {
                var coordinates = ConvertTo2DCoordinates(vertex, transform);
                coordinates = new Vector2(Math.Round(coordinates.X, numDecimalPoints), Math.Round(coordinates.Y, numDecimalPoints));
                if (resultsDict.ContainsKey(coordinates))
                    resultsDict[coordinates].Add(vertex);
                else
                    /* else, add a new vertex to the list, and a new entry to simpleCompareDict.  */
                    resultsDict.Add(coordinates, new List<T> { vertex });
            }
            return resultsDict;
        }

        /// <summary>
        /// Returns newly created 2D coordinates (of type Vector2) projected using the given transform.
        /// These coordinates do not contain references back to the original locations but are lighter and
        /// quicker. 
        /// </summary>
        /// <param name="vertices">The locations.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="backTransform">The back transform.</param>
        /// <param name="toleranceForCombiningPoints">The tolerance for combining multiple locations under a single point.
        /// If not, provided, then one point will be made for each vertex. If zero, then the coordinates will match at
        /// the 15 decimal place. Use a small positive number like 1e-9 to set a wider toleranceForCombiningPoints.</param>
        /// <param name="duplicateEntriesToMaintainPolygonalOrdering">Output is in the same order as input except when
        /// they are combined from the aforementioned tolerance. If this boolean is true then the output point may appear
        /// multiple times in the output collection to maintain the same order. This is useful if the original data is
        /// to define some polygon with order dictating the definition of edges.</param>
        /// 
        /// <returns>System.Collections.Generic.IEnumerable&lt;TVGL.Numerics.Vector2&gt;.</returns>
        public static IEnumerable<Vector2> ProjectTo2DCoordinates<T>(this IEnumerable<T> locations, Vector3 direction,
                    out Matrix4x4 backTransform, double toleranceForCombiningPoints = double.NaN, bool duplicateEntriesToMaintainPolygonalOrdering = false)
            where T : IVertex3D
        {
            var transform = TransformToXYPlane(direction, out backTransform);
            return ProjectTo2DCoordinates<T>(locations, transform, toleranceForCombiningPoints, duplicateEntriesToMaintainPolygonalOrdering);
        }

        /// <summary>
        /// Returns newly created 2D coordinates (of type Vector2) projected using the given transform.
        /// These coordinates do not contain references back to the original locations but are lighter and
        /// quicker. This does not destructively alter the locations.
        /// </summary>
        /// <param name="locations">The locations.</param>
        /// <param name="transform">The transform matrix.</param>
        /// <param name="toleranceForCombiningPoints">The tolerance for combining multiple locations under a single point.
        /// If not, provided, then one point will be made for each vertex. If zero, then the coordinates will match at
        /// the 15 decimal place. Use a small positive number like 1e-9 to set a wider toleranceForCombiningPoints.</param>
        /// <param name="duplicateEntriesToMaintainPolygonalOrdering">Output is in the same order as input except when
        /// they are combined from the aforementioned tolerance. If this boolean is true then the output point may appear
        /// multiple times in the output collection to maintain the same order. This is useful if the original data is
        /// to define some polygon with order dictating the definition of edges.</param>
        /// 
        /// <returns>System.Collections.Generic.IEnumerable&lt;TVGL.Numerics.Vector2&gt;.</returns>
        public static IEnumerable<Vector2> ProjectTo2DCoordinates<T>(this IEnumerable<T> locations, Matrix4x4 transform,
            double toleranceForCombiningPoints = double.NaN, bool duplicateEntriesToMaintainPolygonalOrdering = false) where T : IVertex3D
        {
            if (double.IsNaN(toleranceForCombiningPoints) || toleranceForCombiningPoints < 0.0)
            {
                foreach (var location in locations)
                    yield return ConvertTo2DCoordinates(location, transform);
            }
            else
            {
                var numDecimalPoints = 0;
                var simpleCompareDict = new HashSet<Vector2>();
                while (numDecimalPoints <= 15 && Math.Round(toleranceForCombiningPoints, numDecimalPoints).IsPracticallySame(0.0))
                    numDecimalPoints++;
                foreach (var location in locations)
                {
                    var coordinates = ConvertTo2DCoordinates(location, transform);
                    coordinates = new Vector2(Math.Round(coordinates.X, numDecimalPoints), Math.Round(coordinates.Y, numDecimalPoints));
                    if (simpleCompareDict.Contains(coordinates))
                    {
                        if (duplicateEntriesToMaintainPolygonalOrdering)
                            yield return coordinates;
                    }
                    else
                    {
                        /* else, add a new vertex to the list, and a new entry to simpleCompareDict.  */
                        simpleCompareDict.Add(coordinates);
                        yield return coordinates;
                    }
                }
            }
        }

        /// <summary>
        /// Converts the 3D location (e.g. Vector3) to 2D coordinate (e.g. Vector2).
        /// </summary>
        /// <param name="location3D">The location3 d.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="backTransform">The back transform.</param>
        /// <returns>Vector2.</returns>
        public static Vector2 ConvertTo2DCoordinates(this IVertex3D location3D, in Vector3 direction, out Matrix4x4 backTransform)
        {
            var transform = TransformToXYPlane(direction, out backTransform);
            return ConvertTo2DCoordinates(location3D, transform);
        }

        /// <summary>
        /// Converts the 3D location (e.g. Vector3) to 2D coordinate (e.g. Vector2).
        /// </summary>
        /// <param name="location3D">The location as a Vector3.</param>
        /// <param name="transform">The transform matrix.</param>
        /// <returns>TVGL.Numerics.Vector2.</returns>
        public static Vector2 ConvertTo2DCoordinates(this IVertex3D location3D, in Matrix4x4 matrix)
        {
            var x3D = location3D.X;
            var y3D = location3D.Y;
            var z3D = location3D.Z;

            var x2D = x3D * matrix.M11 + y3D * matrix.M21 + z3D * matrix.M31 + matrix.M41;
            var y2D = x3D * matrix.M12 + y3D * matrix.M22 + z3D * matrix.M32 + matrix.M42;
            if (matrix.IsProjectiveTransform)
            {
                var factor = 1 / (x3D * matrix.M14 + y3D * matrix.M24 + z3D * matrix.M34 + matrix.M44);
                x2D *= factor;
                y2D *= factor;
            }
            return new Vector2(x2D, y2D);
        }
        #endregion

        #region change 2D coordinates (e.g. Vector2's) into 3D locations (e.g. Vector3's) 

        /// <summary>
        /// Converts the 2D coordinates into 3D locations in a plane defined by normal direction and distance.
        /// </summary>
        /// <param name="coordinates">The coordinates.</param>
        /// <param name="normalDirection">The normal direction of the new plane.</param>
        /// <param name="distanceAlongDirection">The distance of the plane from the origin.</param>
        /// <returns>System.Collections.Generic.IEnumerable&lt;TVGL.Numerics.Vector3&gt;.</returns>
        public static IEnumerable<Vector3> ConvertTo3DLocations(this IEnumerable<Vector2> coordinates, Vector3 normalDirection,
                    double distanceAlongDirection)
        {
            TransformToXYPlane(normalDirection, out var backTransform);
            var transform = backTransform * Matrix4x4.CreateTranslation(normalDirection * distanceAlongDirection);
            return ConvertTo3DLocations(coordinates, transform);
        }

        /// <summary>
        /// Converts the 2D coordinates into 3D locations in a plane defined by normal direction and distance.
        /// </summary>
        /// <param name="coordinates">The coordinates.</param>
        /// <param name="normalDirection">The normal direction of the new plane.</param>
        /// <param name="distanceAlongDirection">The distance of the plane from the origin.</param>
        /// <param name="transform">The transform matrix.</param>
        /// <returns>System.Collections.Generic.IEnumerable&lt;TVGL.Numerics.Vector3&gt;.</returns>
        private static IEnumerable<Vector3> ConvertTo3DLocations(this IEnumerable<Vector2> coordinates, Matrix4x4 transform)
        {
            foreach (var point2D in coordinates)
                yield return Vector3.Transform(new Vector3(point2D, 0), transform);
        }

        /// <summary>
        /// Converts the 3D location (e.g. Vector3) to 2D coordinate (e.g. Vector2).
        /// </summary>
        /// <param name="location3D">The location as a Vector3.</param>
        /// <param name="transform">The transform matrix.</param>
        /// <returns>TVGL.Numerics.Vector2.</returns>
        public static Vector3 ConvertTo3DLocation(in Vector2 coordinates2D, in Matrix4x4 transform)
        {
            return Vector3.Transform(new Vector3(coordinates2D, 0), transform);
        }
        #endregion

        /// <summary>
        ///     Transforms to xy plane.
        /// </summary>
        /// <param name="direction">The direction.</param>
        /// <param name="backTransform">The back transform.</param>
        /// <returns>System.Vector2.</returns>
        public static Matrix4x4 TransformToXYPlane(Vector3 direction, out Matrix4x4 backTransform)
        {
            if (direction.X.IsNegligible() && direction.Y.IsNegligible())
            {
                if (direction.Z > 0)
                {
                    backTransform = Matrix4x4.Identity;
                    return Matrix4x4.Identity;
                }
                backTransform = new Matrix4x4(1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0);
                return backTransform;
            }
            else if (direction.X.IsNegligible() && direction.Z.IsNegligible())
            {
                if (direction.Y > 0)
                {
                    backTransform = new Matrix4x4(1, 0, 0, 0, 0, -1, 0, 1, 0, 0, 0, 0);
                    return new Matrix4x4(1, 0, 0, 0, 0, 1, 0, -1, 0, 0, 0, 0);
                }
                backTransform = new Matrix4x4(1, 0, 0, 0, 0, 1, 0, -1, 0, 0, 0, 0);
                return new Matrix4x4(1, 0, 0, 0, 0, -1, 0, 1, 0, 0, 0, 0);
            }
            else if (direction.Y.IsNegligible() && direction.Z.IsNegligible())
            {
                if (direction.X > 0)
                {
                    backTransform = new Matrix4x4(0, 0, -1, 0, 1, 0, 1, 0, 0, 0, 0, 0);
                    return new Matrix4x4(0, 0, 1, 0, 1, 0, -1, 0, 0, 0, 0, 0);
                }
                backTransform = new Matrix4x4(0, 0, 1, 0, 1, 0, -1, 0, 0, 0, 0, 0);
                return new Matrix4x4(0, 0, -1, 0, 1, 0, 1, 0, 0, 0, 0, 0);
            }
            var zDir = direction.Normalize();
            var xDir = zDir.GetPerpendicularDirection();
            var yDir = zDir.Cross(xDir);
            backTransform = new Matrix4x4(xDir, yDir, zDir, Vector3.Zero);
            Matrix4x4.Invert(backTransform, out var forwardTransform);
            return forwardTransform;
        }

        public static Vector3 GetPerpendicularDirection(this Vector3 direction)
        {
            //If the vector is only in the y-direction, then return the x direction
            if (direction.X.IsNegligible() && direction.Z.IsNegligible())
                return Vector3.UnitX;
            // otherwise we will return something in the x-z plane, which is created by
            // taking the cross product of the Y-direction with this vector.
            // The thinking is that - since this is used in the function above (to translate
            // to the x-y plane) - the provided direction, is the new z-direction, so
            // we find something in the x-z plane through this cross-product, so that the
            // third direction has strong component in positive y-direction - like 
            // camera up position.
            return Vector3.UnitY.Cross(direction).Normalize();
        }

        #region Angle between Edges/Lines

        /// <summary>
        ///     Gets the smaller of the two angles between edges.
        /// </summary>
        /// <param name="edge1">The edge1.</param>
        /// <param name="edge2">The edge2.</param>
        /// <returns>System.Double.</returns>
        internal static double SmallerAngleBetweenEdges(Edge edge1, Edge edge2)
        {
            var axis = edge1.Vector.Cross(edge2.Vector);
            var twoDEdges = (new[] { edge1.Vector, edge2.Vector }).ProjectTo2DCoordinates(axis, out _).ToArray();
            var extAngle = ExteriorAngleBetweenEdgesInCCWList(twoDEdges[0], twoDEdges[1]);
            return (extAngle > Math.PI) ? Constants.TwoPi - extAngle : extAngle;
        }


        /// <summary>
        ///     Smallers the angle between edges.
        /// </summary>
        /// <param name="v0">The v0.</param>
        /// <param name="v1">The v1.</param>
        /// <returns>System.Double.</returns>
        internal static double SmallerAngleBetweenEdges(Vector2 v0, Vector2 v1)
        {
            var extAngle = ExteriorAngleBetweenEdgesInCCWList(v0, v1);
            return (extAngle > Math.PI) ? Constants.TwoPi - extAngle : extAngle;
        }

        /// <summary>
        ///     Angles the between edges cw.
        /// </summary>
        /// <param name="edge1">The edge1.</param>
        /// <param name="edge2">The edge2.</param>
        /// <param name="axis">The axis.</param>
        /// <returns>System.Double.</returns>
        internal static double ExteriorAngleBetweenEdgesInCCWList(Edge edge1, Edge edge2, Vector3 axis)
        {
            var twoDEdges = (new[] { edge1.Vector, edge2.Vector }).ProjectTo2DCoordinates(axis, out _).ToArray();
            return ExteriorAngleBetweenEdgesInCCWList(twoDEdges[0], twoDEdges[1]);
        }

        /// <summary>
        ///     Angles the between edges CCW.
        /// </summary>
        /// <param name="edge1">The edge1.</param>
        /// <param name="edge2">The edge2.</param>
        /// <param name="axis">The axis.</param>
        /// <returns>System.Double.</returns>
        internal static double InteriorAngleBetweenEdgesInCCWList(Edge edge1, Edge edge2, Vector3 axis)
        {
            var twoDEdges = (new[] { edge1.Vector, edge2.Vector }).ProjectTo2DCoordinates(axis, out _).ToArray();
            return InteriorAngleBetweenEdgesInCCWList(twoDEdges[0], twoDEdges[1]);
        }

        /// <summary>
        ///     Angles the between edges cw.
        /// </summary>
        /// <param name="edge1">The edge1.</param>
        /// <param name="edge2">The edge2.</param>
        /// <param name="axis">The axis.</param>
        /// <returns>System.Double.</returns>
        internal static double ExteriorAngleBetweenEdgesInCCWList(Vector3 edge1, Vector3 edge2, Vector3 axis)
        {
            var twoDEdges = (new[] { edge1, edge2 }).ProjectTo2DCoordinates(axis, out _).ToArray();
            return ExteriorAngleBetweenEdgesInCCWList(twoDEdges[0], twoDEdges[1]);
        }

        /// <summary>
        ///     Angles the between edges CCW.
        /// </summary>
        /// <param name="edge1">The edge1.</param>
        /// <param name="edge2">The edge2.</param>
        /// <param name="axis">The axis.</param>
        /// <returns>System.Double.</returns>
        internal static double InteriorAngleBetweenEdgesInCCWList(Vector3 edge1, Vector3 edge2, Vector3 axis)
        {
            var twoDEdges = (new[] { edge1, edge2 }).ProjectTo2DCoordinates(axis, out _).ToArray();
            return InteriorAngleBetweenEdgesInCCWList(twoDEdges[0], twoDEdges[1]);
        }

        internal static double ExteriorAngleBetweenEdgesInCCWList(Vector2 a, Vector2 b, Vector2 c)
        {
            return Constants.TwoPi - InteriorAngleBetweenEdgesInCCWList(new Vector2(b.X - a.X, b.Y - a.Y), new Vector2(c.X - b.X, c.Y - b.Y));
        }
        internal static double InteriorAngleBetweenEdgesInCCWList(Vector2 a, Vector2 b, Vector2 c)
        {
            return InteriorAngleBetweenEdgesInCCWList(new Vector2(b.X - a.X, b.Y - a.Y), new Vector2(c.X - b.X, c.Y - b.Y));
        }


        public static double ProjectedExteriorAngleBetweenVerticesCCW(Vertex a, Vertex b, Vertex c, Vector3 positiveNormal)
        {
            return Constants.TwoPi - ProjectedInteriorAngleBetweenVerticesCCW(a, b, c, positiveNormal);
        }
        public static double ProjectedInteriorAngleBetweenVerticesCCW(Vertex a, Vertex b, Vertex c, Vector3 positiveNormal)
        {
            var flattenTransform = TransformToXYPlane(positiveNormal, out _);
            return ProjectedInteriorAngleBetweenVerticesCCW(a, b, c, flattenTransform);
        }

        internal static double ProjectedExteriorAngleBetweenVerticesCCW(Vertex a, Vertex b, Vertex c, Matrix4x4 flattenTransform)
        {
            return Constants.TwoPi - ProjectedInteriorAngleBetweenVerticesCCW(a, b, c, flattenTransform);
        }
        internal static double ProjectedInteriorAngleBetweenVerticesCCW(Vertex a, Vertex b, Vertex c, Matrix4x4 flattenTransform)
        {
            var points = (new List<Vertex> { a, b, c }).ProjectTo2DCoordinates(flattenTransform).ToArray();
            return InteriorAngleBetweenEdgesInCCWList(new Vector2(points[1].X - points[0].X, points[1].Y - points[0].Y),
                new Vector2(points[2].X - points[1].X, points[2].Y - points[1].Y));
        }

        /// <summary>
        ///     Gets the exterior angle between two edges, assuming the edges are listed in CCW order.
        /// </summary>
        /// <param name="v0">The v0.</param>
        /// <param name="v1">The v1.</param>
        /// <returns>System.Double.</returns>
        public static double ExteriorAngleBetweenEdgesInCCWList(Vector2 v0, Vector2 v1)
        {
            return 2 * Math.PI - InteriorAngleBetweenEdgesInCCWList(v0, v1);
        }

        /// <summary>
        ///     Gets the interior angle between two edges, assuming the edges are listed in CCW order.
        ///     NOTE: This is opposite from getting the CCW angle from v0 and v1.
        /// </summary>
        /// <param name="v0">The v0.</param>
        /// <param name="v1">The v1.</param>
        /// <returns>System.Double.</returns>
        public static double InteriorAngleBetweenEdgesInCCWList(Vector2 v0, Vector2 v1)
        {
            #region Law of Cosines Approach (Commented Out)

            ////This is an alternative approach to the one that is not commented out
            ////Use law of cosines to find smaller angle between two vectors
            //var aSq = v0.X * v0.X + v0.Y * v0.Y;
            //var bSq = v1.X * v1.X + v1.Y * v1.Y;
            //var cSq = (v0.X + v1.X) * (v0.X + v1.X) + (v0.Y + v1.Y) * (v0.Y + v1.Y);
            //var angle = Math.Acos((aSq + bSq - cSq) / (2 * Math.Sqrt(aSq) * Math.Sqrt(bSq)));
            ////Use cross product sign to determine if smaller angle is CCW from v0
            //var cross = v0.X * v1.Y - v0.Y * v1.X;
            //if (Math.Sign(cross) < 0) angle = 2 * Math.PI - angle;

            #endregion

            var angleV0 = Math.Atan2(v0.Y, v0.X);
            var angleV1 = Math.Atan2(v1.Y, v1.X);
            var angleChange = Math.PI - (angleV1 - angleV0);
            if (angleChange > 2 * Math.PI) return angleChange - 2 * Math.PI;
            if (angleChange < 0) return angleChange + 2 * Math.PI;
            return angleChange;
        }

        #endregion

        #region Intersection Method (between lines, planes, solids, etc.)

        /// <summary>
        /// Detemines if Two Lines intersect. Outputs intersection point if they do.
        /// If two lines are colinear, they are not considered intersecting.
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <param name="intersectionPoint"></param>
        /// <param name="considerCollinearOverlapAsIntersect"></param>
        /// <returns></returns>
        internal static bool PolygonSegmentIntersection(this PolygonSegment line1, PolygonSegment line2, out Vector2 intersectionPoint,
            bool considerCollinearOverlapAsIntersect = false)
        {
            return
                SegmentSegment2DIntersection(line1.FromPoint.Coordinates, line1.ToPoint.Coordinates,
                line2.FromPoint.Coordinates, line2.ToPoint.Coordinates,
                out intersectionPoint, considerCollinearOverlapAsIntersect);
        }

        /// <summary>
        /// Determines if Two Lines intersect. Outputs intersection point if they do.
        /// If two lines are collinear, they are not considered intersecting.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="p2">The p2.</param>
        /// <param name="q1">The q1.</param>
        /// <param name="q2">The q2.</param>
        /// <param name="intersectionPoint">The intersection point.</param>
        /// <param name="considerCollinearOverlapAsIntersect">The consider collinear overlap as intersect.</param>
        /// <returns>System.Boolean.</returns>
        public static bool SegmentSegment2DIntersection(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2,
            out Vector2 intersectionPoint, bool considerCollinearOverlapAsIntersect = false)
        {
            intersectionPoint = Vector2.Null;
            // first check if bounding boxes overlap. If they don't then return false here
            if (Math.Max(p1.X, p2.X) < Math.Min(q1.X, q2.X) || Math.Max(q1.X, q2.X) < Math.Min(p1.X, p2.X) ||
                Math.Max(p1.Y, p2.Y) < Math.Min(q1.Y, q2.Y) || Math.Max(p1.Y, p2.Y) < Math.Min(q1.Y, q2.Y))
                return false;
            // okay, so bounding boxes overlap
            //first a quick check to see if points are the same
            if (p1.IsPracticallySame(q1) || p1.IsPracticallySame(q2))
            {
                intersectionPoint = p1;
                return true;
            }
            if (p2.IsPracticallySame(q1) || p2.IsPracticallySame(q2))
            {
                intersectionPoint = p2;
                return true;
            }

            var vp = p2 - p1; //vector along p-line
            var vq = q2 - q1; //vector along q-line
            var vCross = vp.Cross(vq); //2D cross product, determines if parallel
            var vStarts = q1 - p1; // the vector connecting starts

            if (vCross.IsNegligible(Constants.BaseTolerance))
            {
                // if this is also parallel with the vector direction then there is overlap
                // (since bounding boxes overlap). But we cannot set intersectionPoint
                // to a single value since it is infinite points!
                if (vStarts.Cross(vp).IsNegligible(Constants.BaseTolerance))
                    return considerCollinearOverlapAsIntersect;
                return false;
            }
            // solve for the t scalar values for the two lines.
            // the line is define as all values of t from 0 to 1 in the equations
            // p-line(t_p) = (1 - t_p)*p1 + t_p*p2
            // q-line(t_q) = (1 - t_q)*q1 + t_q*q2
            // solve as a system of two equations
            //   |   vp_x      vq_x   | |  t_p  |    | vStarts_x  |
            //   |                    |*|       | =  |            |
            //   |   vp_y      vq_y   | |  t_q  |    | vStarts_y  |
            var oneOverdeterminnant = 1 / vCross;
            var aInv11 = vq.Y * oneOverdeterminnant;
            var aInv12 = -vq.X * oneOverdeterminnant;
            var aInv21 = -vp.Y * oneOverdeterminnant;
            var aInv22 = vp.X * oneOverdeterminnant;
            var t_p = aInv11 * vStarts.X + aInv12 * vStarts.Y;
            var t_q = aInv21 * vStarts.X + aInv22 * vStarts.Y;
            if (t_p < 0 || t_p > 1 || t_q < 0 || t_q > 1) return false;
            intersectionPoint = 0.5 * ((1 - t_p) * p1 + t_p * p2 + (1 - t_q) * q1 + t_q * q2);
            return true;
        }
        /// <summary>
        /// Determines if Two Lines intersect. Outputs intersection point if they do.
        /// If two lines are collinear, they are not considered intersecting.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="p2">The p2.</param>
        /// <param name="qAnchor">The q1.</param>
        /// <param name="q2">The q2.</param>
        /// <param name="intersectionPoint">The intersection point.</param>
        /// <param name="considerCollinearOverlapAsIntersect">The consider collinear overlap as intersect.</param>
        /// <returns>System.Boolean.</returns>
        public static bool SegmentLine2DIntersection(Vector2 p1, Vector2 p2, Vector2 qAnchor, Vector2 qDirection,
            out Vector2 intersectionPoint, bool considerCollinearOverlapAsIntersect = false)
        {
            intersectionPoint = Vector2.Null;
            // okay, so bounding boxes overlap
            //first a quick check to see if points are the same
            if (p1.IsPracticallySame(qAnchor))
            {
                intersectionPoint = p1;
                return true;
            }
            if (p2.IsPracticallySame(qAnchor))
            {
                intersectionPoint = p2;
                return true;
            }

            var vp = p2 - p1; //vector along p-line
            var vCross = vp.Cross(qDirection); //2D cross product, determines if parallel
            var vStarts = qAnchor - p1; // the vector connecting starts

            if (vCross.IsNegligible(Constants.BaseTolerance))
            {
                // if this is also parallel with the vector direction then there is overlap
                // (since bounding boxes overlap). But we cannot set intersectionPoint
                // to a single value since it is infinite points!
                if (vStarts.Cross(vp).IsNegligible(Constants.BaseTolerance))
                    return considerCollinearOverlapAsIntersect;
                return false;
            }
            // solve for the t scalar values for the two lines.
            // the line is define as all values of t from 0 to 1 in the equations
            // p-line(t_p) = (1 - t_p)*p1 + t_p*p2
            // q-line(t_q) = (1 - t_q)*q1 + t_q*q2
            // solve as a system of two equations
            //   |   vp_x      vq_x   | |  t_p  |    | vStarts_x  |
            //   |                    |*|       | =  |            |
            //   |   vp_y      vq_y   | |  t_q  |    | vStarts_y  |
            var oneOverdeterminnant = 1 / vCross;
            var aInv11 = qDirection.Y * oneOverdeterminnant;
            var aInv12 = -qDirection.X * oneOverdeterminnant;
            //var aInv21 = -vp.Y * oneOverdeterminnant;
            //var aInv22 = vp.X * oneOverdeterminnant;
            var t_p = aInv11 * vStarts.X + aInv12 * vStarts.Y;
            if (t_p < 0 || t_p > 1) return false;
            intersectionPoint = (1 - t_p) * p1 + t_p * p2;
            return true;
        }

        /// <summary>
        ///     Find the point common to three planes.
        /// </summary>
        /// <param name="n1">The n1.</param>
        /// <param name="d1">The d1.</param>
        /// <param name="n2">The n2.</param>
        /// <param name="d2">The d2.</param>
        /// <param name="n3">The n3.</param>
        /// <param name="d3">The d3.</param>
        /// <returns>System.Vector2.</returns>
        public static Vector3 PointCommonToThreePlanes(Vector3 n1, double d1, Vector3 n2, double d2, Vector3 n3,
            double d3)
        {
            var matrixOfNormals = new Matrix3x3(n1.X, n1.Y, n1.Z, n2.X, n2.Y, n2.Z, n3.X, n3.Y, n3.Z);
            var distances = new Vector3(d1, d2, d3);
            if (!Matrix3x3.Invert(matrixOfNormals, out var mInv))
                return Vector3.Null;
            return distances.Transform(mInv);
        }

        public static Flat GetPlaneFromThreePoints(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var a1 = p2.X - p1.X;
            var b1 = p2.Y - p1.Y;
            var c1 = p2.Z - p1.Z;
            var a2 = p3.X - p1.X;
            var b2 = p3.Y - p1.Y;
            var c2 = p3.Z - p1.Z;
            var a = b1 * c2 - b2 * c1;
            var b = a2 * c1 - a1 * c2;
            var c = a1 * b2 - b1 * a2;
            var normal = new Vector3(a, b, c).Normalize();
            var flat2 = new Flat(p1, normal);
            return flat2;
        }

        /// <summary>
        ///     Lines the intersecting two planes.
        /// </summary>
        /// <param name="n1">The n1.</param>
        /// <param name="d1">The d1.</param>
        /// <param name="n2">The n2.</param>
        /// <param name="d2">The d2.</param>
        /// <param name="directionOfLine">The direction of line.</param>
        /// <param name="pointOnLine">The point on line.</param>
        public static void LineIntersectingTwoPlanes(Vector3 n1, double d1, Vector3 n2, double d2,
            out Vector3 directionOfLine, out Vector3 pointOnLine)
        {
            directionOfLine = n1.Cross(n2).Normalize();
            LineIntersectingTwoPlanes(n1, d1, n2, d2, directionOfLine, out pointOnLine);
        }

        /// <summary>
        ///     Lines the intersecting two planes.
        /// </summary>
        /// <param name="n1">The n1.</param>
        /// <param name="d1">The d1.</param>
        /// <param name="n2">The n2.</param>
        /// <param name="d2">The d2.</param>
        /// <param name="directionOfLine">The direction of line.</param>
        /// <param name="pointOnLine">The point on line.</param>
        internal static void LineIntersectingTwoPlanes(Vector3 n1, double d1, Vector3 n2, double d2,
            Vector3 directionOfLine, out Vector3 pointOnLine)
        {
            /* to find the point on the line...well a point on the line, it turns out that one has three unknowns (px, py, pz)
             * and only two equations. Let's put the point on the plane going through the origin. So this plane would have a normal 
             * of v (or DirectionOfLine). */
            var a = new Matrix3x3(n1.X, n1.Y, n1.Z, n2.X, n2.Y, n2.Z, directionOfLine.X, directionOfLine.Y, directionOfLine.Z);
            var b = new Vector3(d1, d2, 0);
            if (!Matrix3x3.Invert(a, out var aInv))
                pointOnLine = Vector3.Null;
            pointOnLine = b.Transform(aInv);
        }

        /// <summary>
        ///     Skeweds the line intersection.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="n1">The n1.</param>
        /// <param name="p2">The p2.</param>
        /// <param name="n2">The n2.</param>
        /// <returns>System.Double.</returns>
        internal static double SkewedLineIntersection(Vector3 p1, Vector3 n1, Vector3 p2, Vector3 n2)
        {
            return SkewedLineIntersection(p1, n1, p2, n2, out _, out _, out _, out _, out _);
        }

        /// <summary>
        ///     Skeweds the line intersection.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="n1">The n1.</param>
        /// <param name="p2">The p2.</param>
        /// <param name="n2">The n2.</param>
        /// <param name="center">The center.</param>
        /// <returns>System.Double.</returns>
        internal static double SkewedLineIntersection(Vector3 p1, Vector3 n1, Vector3 p2, Vector3 n2,
            out Vector3 center)
        {
            return SkewedLineIntersection(p1, n1, p2, n2, out center, out _, out _, out _, out _);
        }

        /// <summary>
        ///     Skeweds the line intersection.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="n1">The n1.</param>
        /// <param name="p2">The p2.</param>
        /// <param name="n2">The n2.</param>
        /// <param name="interSect1">The inter sect1.</param>
        /// <param name="interSect2">The inter sect2.</param>
        /// <returns>System.Double.</returns>
        internal static double SkewedLineIntersection(Vector3 p1, Vector3 n1, Vector3 p2, Vector3 n2,
            out Vector3 interSect1, out Vector3 interSect2)
        {
            return SkewedLineIntersection(p1, n1, p2, n2, out _, out interSect1, out interSect2, out _, out _);
        }

        /// <summary>
        ///     Skeweds the line intersection.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="n1">The n1.</param>
        /// <param name="p2">The p2.</param>
        /// <param name="n2">The n2.</param>
        /// <param name="center">The center.</param>
        /// <param name="t1">The t1.</param>
        /// <param name="t2">The t2.</param>
        /// <returns>System.Double.</returns>
        internal static double SkewedLineIntersection(Vector3 p1, Vector3 n1, Vector3 p2, Vector3 n2,
            out Vector3 center, out double t1, out double t2)
        {
            return SkewedLineIntersection(p1, n1, p2, n2, out center, out _, out _, out t1, out t2);
        }

        /// <summary>
        ///     Skeweds the line intersection.
        /// </summary>
        /// <param name="p1">The p1.</param>
        /// <param name="n1">The n1.</param>
        /// <param name="p2">The p2.</param>
        /// <param name="n2">The n2.</param>
        /// <param name="center">The center.</param>
        /// <param name="interSect1">The inter sect1.</param>
        /// <param name="interSect2">The inter sect2.</param>
        /// <param name="t1">The t1.</param>
        /// <param name="t2">The t2.</param>
        /// <returns>System.Double.</returns>
        internal static double SkewedLineIntersection(Vector3 p1, Vector3 n1, Vector3 p2, Vector3 n2,
            out Vector3 center,
            out Vector3 interSect1, out Vector3 interSect2, out double t1, out double t2)
        {
            var a11 = n1.X * n1.X + n1.Y * n1.Y + n1.Z * n1.Z;
            var a12 = -n1.X * n2.X - n1.Y * n2.Y - n1.Z * n2.Z;
            var a21 = n1.X * n2.X + n1.Y * n2.Y + n1.Z * n2.Z;
            var a22 = -n2.X * n2.X - n2.Y * n2.Y - n2.Z * n2.Z;
            var b1 = n1.X * (p2.X - p1.X) + n1.Y * (p2.Y - p1.Y) + n1.Z * (p2.Z - p1.Z);
            var b2 = n2.X * (p2.X - p1.X) + n2.Y * (p2.Y - p1.Y) + n2.Z * (p2.Z - p1.Z);
            //var a = new[,] { { a11, a12 }, { a21, a22 } };
            var aDetInverse = 1 / (a11 * a22 - a21 * a12);
            //var aInv = new[,] { { a22, -a12 }, {-a21,a11 } };
            var b = new[] { b1, b2 };
            //var t = solve(a, b);
            t1 = (a22 * b1 - a12 * b2) * aDetInverse;
            t2 = (-a21 * b1 + a11 * b2) * aDetInverse;
            interSect1 = new Vector3(p1.X + n1.X * t1, p1.Y + n1.Y * t1, p1.Z + n1.Z * t1);
            interSect2 = new Vector3(p2.X + n2.X * t2, p2.Y + n2.Y * t2, p2.Z + n2.Z * t2);
            center = new Vector3((interSect1.X + interSect2.X) / 2, (interSect1.Y + interSect2.Y) / 2,
                (interSect1.Z + interSect2.Z) / 2);
            return interSect1.Distance(interSect2);
        }

        #endregion

        #region Distance Methods (between point, line, and plane)

        /// <summary>
        ///     Returns the distance the point on an infinite line.
        /// </summary>
        /// <param name="qPoint">The q point that is off of the line.</param>
        /// <param name="lineRefPt">The line reference point on the line.</param>
        /// <param name="lineVector">The line direction vector.</param>
        /// <returns>System.Double.</returns>
        public static double DistancePointToLine(Vector3 qPoint, Vector3 lineRefPt, Vector3 lineVector)
        {
            return DistancePointToLine(qPoint, lineRefPt, lineVector, out _);
        }

        /// <summary>
        ///     Returns the distance the point on an infinite line.
        /// </summary>
        /// <param name="qPoint">q is the point that is off of the line.</param>
        /// <param name="lineRefPt">p is a reference point on the line.</param>
        /// <param name="lineVector">n is the vector of the line direction.</param>
        /// <param name="pointOnLine">The point on line closest to point, q.</param>
        /// <returns>System.Double.</returns>
        public static double DistancePointToLine(Vector3 qPoint, Vector3 lineRefPt, Vector3 lineVector,
            out Vector3 pointOnLine)
        {
            double t;
            /* pointOnLine is found by setting the dot-product of the lineVector and the vector formed by (pointOnLine-p) 
             * set equal to zero. This is really just solving to "t" the distance along the line from the lineRefPt. */
            t = (lineVector.X * (qPoint.X - lineRefPt.X) + lineVector.Y * (qPoint.Y - lineRefPt.Y) +
                     lineVector.Z * (qPoint.Z - lineRefPt.Z))
                    / (lineVector.X * lineVector.X + lineVector.Y * lineVector.Y + lineVector.Z * lineVector.Z);
            pointOnLine = new Vector3(
            lineRefPt.X + lineVector.X * t, lineRefPt.Y + lineVector.Y * t, lineRefPt.Z + lineVector.Z * t);
            return qPoint.Distance(pointOnLine);
        }

        public static double DistancePointToLine(Vector2 qPoint, Vector2 lineRefPt, Vector2 lineVector,
    out Vector2 pointOnLine)
        {
            double t;
            /* pointOnLine is found by setting the dot-product of the lineVector and the vector formed by (pointOnLine-p) 
            * set equal to zero. This is really just solving to "t" the distance along the line from the lineRefPt. */
            t = (lineVector.X * (qPoint.X - lineRefPt.X) + lineVector.Y * (qPoint.Y - lineRefPt.Y))
                    / (lineVector.X * lineVector.X + lineVector.Y * lineVector.Y);
            pointOnLine = new Vector2(lineRefPt.X + lineVector.X * t, lineRefPt.Y + lineVector.Y * t);
            return qPoint.Distance(pointOnLine);
        }

        /// <summary>
        ///     Distances the point to point.
        /// </summary>
        /// <param name="p1">point, p1.</param>
        /// <param name="p2">point, p2.</param>
        /// <returns>the distance between the two 3D points.</returns>
        public static double DistancePointToPoint(Vertex v1, Vertex v2)
        {
            return v1.Coordinates.Distance(v2.Coordinates);
        }

        /// <summary>
        ///     Returns the signed distance of the point to the plane.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="normalOfPlane">The normal of plane.</param>
        /// <param name="positionOnPlane">The position on plane.</param>
        /// <returns>the distance between the two 3D points.</returns>
        public static double DistancePointToPlane(Vector3 point, Vector3 normalOfPlane, Vector3 positionOnPlane)
        {
            return DistancePointToPlane(point, normalOfPlane, positionOnPlane.Dot(normalOfPlane));
        }

        /// <summary>
        ///     Returns the signed distance of the point to the plane. If the point is "above" the plane, then a positive
        ///     distance is return - if "below" then negative. This "above" means that the point is on the side of the
        ///     plane that the normal points towards.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="normalOfPlane">The normal of plane.</param>
        /// <param name="signedDistanceToPlane">The signed distance to plane.</param>
        /// <returns>the distance between the two 3D points.</returns>
        public static double DistancePointToPlane(Vector3 point, Vector3 normalOfPlane, double signedDistanceToPlane)
        {
            return normalOfPlane.Dot(point) - signedDistanceToPlane;
        }

        /// <summary>
        ///     Finds the point on the face made by a line (which is described by connecting point1 and point2) intersecting
        ///     with that face. If not intersection exists, then function returns null. Points must be on either side 
        ///     of triangle to return a valid intersection.
        /// </summary>
        /// <param name="face"></param>
        /// <param name="point1">The point1.</param>
        /// <param name="point2">The point2.</param>
        /// <returns>Vertex.</returns>
        /// <exception cref="Exception">This should never occur. Prevent this from happening</exception>
        public static Vector3 PointOnFaceFromIntersectingLine(this PolygonalFace face, Vector3 point1, Vector3 point2)
        {
            var positions = face.Vertices.Select(vertex => vertex.Coordinates).ToList();
            return PointOnFaceFromIntersectingLine(positions, face.Normal, point1, point2);
        }

        /// <summary>
        ///     Finds the point on the face made by a line (which is described by connecting point1 and point2) intersecting
        ///     with that face. If not intersection exists, then function returns a Vector3 with NaN's. Points must
        ///     be on either side of triangle to return a valid intersection.
        /// </summary>
        /// <param name="normal"></param>
        /// <param name="point1">The point1.</param>
        /// <param name="point2">The point2.</param>
        /// <param name="vertices"></param>
        /// <returns>Vertex.</returns>
        public static Vector3 PointOnFaceFromIntersectingLine(List<Vector3> vertices, Vector3 normal, Vector3 point1,
            Vector3 point2)
        {
            var planeDistance = normal.Dot(vertices[0]);
            var d1 = normal.Dot(point1);
            var d2 = normal.Dot(point2);
            if (Math.Sign(planeDistance - d1) == Math.Sign(planeDistance - d2)) return Vector3.Null; //Points must be on either side of triangle
            var denominator = d2 - d1;
            if (denominator == 0) return Vector3.Null; //The points form a perpendicular line to the face
            var fraction = (planeDistance - d1) / denominator;
            var intersectionPoint = Vector3.Lerp(point1, point2, fraction);

            return IsVertexInsideTriangle(vertices, intersectionPoint, true) ? intersectionPoint : Vector3.Null;
        }

        /// <summary>
        ///     Finds the point on the plane made by a line (which is described by connecting point1 and point2) intersecting
        ///     with that plane.Returns null if the intersection point is not on the line segment.
        /// </summary>
        /// <param name="normalOfPlane">The normal of plane.</param>
        /// <param name="distOfPlane">The dist of plane.</param>
        /// <param name="point1">The point1.</param>
        /// <param name="point2">The point2.</param>
        /// <returns>Vertex.</returns>
        /// <exception cref="Exception">This should never occur. Prevent this from happening</exception>
        public static Vertex PointOnPlaneFromIntersectingLineSegment(Vector3 normalOfPlane, double distOfPlane, Vertex point1,
            Vertex point2)
        {
            var position =
                PointOnPlaneFromIntersectingLineSegment(normalOfPlane, distOfPlane, point1.Coordinates, point2.Coordinates);
            return position == null ? null : new Vertex(position);
        }

        /// <summary>
        ///     Finds the point on the plane made by a line (which is described by connecting point1 and point2) intersecting
        ///     with that plane. Returns null if the intersection point is not on the line segment.
        /// </summary>
        /// <param name="normalOfPlane">The normal of plane.</param>
        /// <param name="distOfPlane">The dist of plane.</param>
        /// <param name="point1">The point1.</param>
        /// <param name="point2">The point2.</param>
        /// <returns>Vertex.</returns>
        /// <exception cref="Exception">This should never occur. Prevent this from happening</exception>
        public static Vector3 PointOnPlaneFromIntersectingLineSegment(Vector3 normalOfPlane, double distOfPlane, Vector3 point1,
            Vector3 point2)
        {
            var position = PointOnPlaneFromIntersectingLine(normalOfPlane, distOfPlane, point1, point2);
            var d1 = point2.Subtract(point1).Length();
            var d2 = point2.Subtract(position).Length();
            var d3 = point1.Subtract(position).Length();
            return d1.IsPracticallySame(d2 + d3, 1 - Constants.HighConfidence) ? position : Vector3.Null;
        }

        /// <summary>
        ///     Finds the point on the plane made by a line (which is described by connecting point1 and point2) intersecting
        ///     with that plane.
        /// </summary>
        /// <param name="normalOfPlane">The normal of plane.</param>
        /// <param name="distOfPlane">The dist of plane.</param>
        /// <param name="point1">The point1.</param>
        /// <param name="point2">The point2.</param>
        /// <returns>Vertex.</returns>
        /// <exception cref="Exception">This should never occur. Prevent this from happening</exception>
        public static Vector2 Vector2OnZPlaneFromIntersectingLine(double distOfPlane, Vertex point1,
            Vertex point2)
        {
            var toFactor = (distOfPlane - point1.Z) / (point2.Z - point1.Z);
            var fromFactor = 1 - toFactor;

            return new Vector2(fromFactor * point1.X + toFactor * point2.X,
                fromFactor * point1.Y + toFactor * point2.Y);
        }

        /// <summary>
        ///     Finds the point on the plane made by a line (which is described by connecting point1 and point2) intersecting
        ///     with that plane.
        /// </summary>
        /// <param name="normalOfPlane">The normal of plane.</param>
        /// <param name="distOfPlane">The dist of plane.</param>
        /// <param name="point1">The point1.</param>
        /// <param name="point2">The point2.</param>
        /// <returns>Vertex.</returns>
        /// <exception cref="Exception">This should never occur. Prevent this from happening</exception>
        public static Vector3 PointOnPlaneFromIntersectingLine(Vector3 normalOfPlane, double distOfPlane, Vector3 point1,
            Vector3 point2)
        {
            var d1 = normalOfPlane.Dot(point1);
            var d2 = normalOfPlane.Dot(point2);
            var fraction = (d1 - distOfPlane) / (d1 - d2);
            return Vector3.Lerp(point1, point2, fraction);
        }

        /// <summary>
        ///     Finds the point on the plane made by a line intersecting
        ///     with that plane.
        /// </summary>
        /// <param name="normalOfPlane">The normal of plane. Can be 2D or 3D. </param>
        /// <param name="distOfPlane">The dist of plane.</param>
        /// <param name="line"></param>
        /// <returns>Vertex.</returns>
        /// <exception cref="Exception">This should never occur. Prevent this from happening</exception>
        internal static Vector2 Vector2OnPlaneFromIntersectingLine(Vector2 normalOfPlane, double distOfPlane, PolygonSegment line)
        {
            Vector2OnPlaneFromIntersectingLine(normalOfPlane.X, normalOfPlane.Y, distOfPlane, line.FromPoint.X, line.FromPoint.Y,
                line.ToPoint.X, line.FromPoint.Y, out var x, out var y);
            return new Vector2(x, y);
        }

        public static void Vector2OnPlaneFromIntersectingLine(double normalOfPlaneX, double normalOfPlaneY, double distOfPlane,
            double fromPointX, double fromPointY, double toPointX, double toPointY, out double x, out double y)
        {
            var d1 = normalOfPlaneX * toPointX + normalOfPlaneY * toPointY; //2D Dot product
            var d2 = normalOfPlaneX * fromPointX + normalOfPlaneY * fromPointY;  //For a point, Position.Z = 0.0
            var fraction = (d1 - distOfPlane) / (d1 - d2);
            x = fromPointX * fraction + toPointX * (1 - fraction);
            y = fromPointY * fraction + toPointY * (1 - fraction);
        }

        /// <summary>
        ///     Finds the point on the plane made by a ray. If that ray is not going to pass through the
        ///     that plane, then null is returned.
        /// </summary>
        /// <param name="normalOfPlane">The normal of plane.</param>
        /// <param name="distOfPlane">The dist of plane.</param>
        /// <param name="rayPosition">The ray position.</param>
        /// <param name="rayDirection">The ray direction.</param>
        /// <param name="signedDistance"></param>
        /// <returns>Vertex.</returns>
        public static Vector3 PointOnPlaneFromRay(Vector3 normalOfPlane, double distOfPlane, Vector3 rayPosition,
            Vector3 rayDirection, out double signedDistance)
        {
            var dot = rayDirection.Dot(normalOfPlane);
            signedDistance = 0.0;
            if (dot == 0) return Vector3.Null;

            var d1 = -DistancePointToPlane(rayPosition, normalOfPlane, distOfPlane);
            signedDistance = d1 / dot;
            if (signedDistance.IsNegligible()) return rayPosition;
            return rayPosition + (rayDirection * signedDistance);
        }

        /// <summary>
        ///     Finds the point on the triangle made by a line. If that line is not going to pass through the
        ///     that triangle, then null is returned. The signed distance is positive if the vertex points to
        ///     the triangle along the direction (ray). User can also specify whether the edges of the triangle
        ///     are considered "inside."
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="vertex">The vertex.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="signedDistance">The signed distance.</param>
        /// 
        /// <returns>Vertex.</returns>
        public static Vector3 PointOnTriangleFromLine(this PolygonalFace face, Vertex vertex, Vector3 direction,
            out double signedDistance)
        {
            return PointOnTriangleFromLine(face, vertex.Coordinates, direction, out signedDistance);
        }

        /// <summary>
        ///     Finds the point on the triangle made by a line. If that line is not going to pass through the
        ///     that triangle, then null is returned. The signed distance is positive if the vertex points to
        ///     the triangle along the direction (ray). User can also specify whether the edges of the triangle
        ///     are considered "inside."
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point3D"></param>
        /// <param name="direction">The direction.</param>
        /// <param name="signedDistance">The signed distance.</param>
        /// <param name="onBoundaryIsInside">if set to <c>true</c> [on boundary is inside].</param>
        public static Vector3 PointOnTriangleFromLine(PolygonalFace face, Vector3 point3D, Vector3 direction,
            out double signedDistance, bool onBoundaryIsInside = true)
        {
            var distanceToOrigin = face.Normal.Dot(face.Vertices[0].Coordinates);
            var newPoint = PointOnPlaneFromRay(face.Normal, distanceToOrigin, point3D, direction, out signedDistance);
            if (newPoint == null) return Vector3.Null;
            return IsVertexInsideTriangle(face.Vertices, newPoint, onBoundaryIsInside) ? newPoint : Vector3.Null;
        }

        /// <summary>
        ///     Finds the point on the triangle made by a line. If that line is not going to pass through the
        ///     that triangle, then the result is comprise of NaN's. The signed distance is positive if the vertex points to
        ///     the triangle along the direction (ray). User can also specify whether the edges of the triangle
        ///     are considered "inside."
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point3D"></param>
        /// <param name="direction">The direction.</param>
        /// <param name="signedDistance">The signed distance.</param>
        /// <param name="onBoundaryIsInside">if set to <c>true</c> [on boundary is inside].</param>
        public static Vector3 PointOnTriangleFromLine(this PolygonalFace face, Vector3 point3D, CartesianDirections direction,
            out double signedDistance, bool onBoundaryIsInside = true)
        {
            Vector3 newPoint;
            signedDistance = double.NaN;
            var d = face.Normal.Dot(face.Vertices[0].Coordinates);
            var n = face.Normal;
            switch (direction)
            {
                case CartesianDirections.XNegative:
                case CartesianDirections.XPositive:
                    if (face.Normal.X.IsNegligible()) return Vector3.Null;
                    newPoint = new Vector3((d - n.Y * point3D.Y - n.Z * point3D.Z) / n.X, point3D.Y, point3D.Z);
                    signedDistance = (Math.Sign((int)direction)) * (newPoint.X - point3D.X);
                    break;
                case CartesianDirections.YNegative:
                case CartesianDirections.YPositive:
                    if (face.Normal.Y.IsNegligible()) return Vector3.Null;
                    newPoint = new Vector3(point3D.X, (d - n.X * point3D.X - n.Z * point3D.Z) / n.Y, point3D.Z);
                    signedDistance = (Math.Sign((int)direction)) * (newPoint.Y - point3D.Y);
                    break;
                default:
                    if (face.Normal.Z.IsNegligible()) return Vector3.Null;
                    newPoint = new Vector3(point3D.X, point3D.Y, (d - n.X * point3D.X - n.Y * point3D.Y) / n.Z);
                    signedDistance = (Math.Sign((int)direction)) * (newPoint.Z - point3D.Z);
                    break;
            }

            return IsVertexInsideTriangle(face.Vertices, newPoint, onBoundaryIsInside) ? newPoint : Vector3.Null;
        }
        #endregion

        #region Create 2D Circle Paths
        /// <summary>
        /// Returns a the path of a circle made up of points. Increment as needed.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="radianIncrement"></param>
        /// <returns></returns>
        public static List<Vector2> CreateCirclePath(Vector2 center, double radius, double radianIncrement = Math.PI / 50.0)
        {
            var path = new List<Vector2>();
            for (var theta = 0.0; theta < Math.PI * 2; theta += radianIncrement)
            {
                path.Add(new Vector2(radius * Math.Cos(theta) + center.X, radius * Math.Sin(theta) + center.Y));
            }
            return path;
        }

        /// <summary>
        /// Returns a the path of a circle made up of points. Increment as needed.
        /// </summary>
        /// <param name="circle"></param>
        /// <param name="radianIncrement"></param>
        /// <returns></returns>
        public static List<Vector2> CreateCirclePath(BoundingCircle circle, double radianIncrement = Math.PI / 50.0)
        {
            return CreateCirclePath(circle.Center, circle.Radius, radianIncrement);
        }
        #endregion

        #region isInside Methods (is 2D point inside polygon, vertex inside solid, ect.)

        /// <summary>
        ///     Returns whether a vertex lies on a triangle. User can specify whether the edges of the
        ///     triangle are considered "inside." Assumes vertex in question is in the same plane
        ///     as the triangle.
        /// </summary>
        public static bool IsVertexInsideTriangle(IList<Vertex> triangle, Vector3 vertexInQuestion,
            bool onBoundaryIsInside = true)
        {
            return IsVertexInsideTriangle(new[] { triangle[0].Coordinates, triangle[1].Coordinates, triangle[2].Coordinates },
                vertexInQuestion, onBoundaryIsInside);
        }

        /// <summary>
        ///     Returns whether a vertex lies on a triangle. User can specify whether the edges of the
        ///     triangle are considered "inside." Assumes vertex in question is in the same plane
        ///     as the triangle.
        /// </summary>
        /// <param name="triangle"></param>
        /// <param name="pointInQuestion"></param>
        /// <param name="onBoundaryIsInside"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static bool IsVertexInsideTriangle(IList<Vector3> triangle, Vector3 pointInQuestion,
            bool onBoundaryIsInside = true)
        {
            if (triangle.Count != 3) throw new Exception("Incorrect number of points in triangle");
            var p = pointInQuestion;
            var a = triangle[0];
            var b = triangle[1];
            var c = triangle[2];
            return ((b - a).Cross(p - a)).Dot((p - a).Cross(c - a)) >= 0
                && ((c - b).Cross(p - b)).Dot((p - b).Cross(a - b)) >= 0;
        }



        /// <summary>
        ///     Determines if a point is inside a tesselated solid (polyhedron).
        ///     And the polygon is not self-intersecting
        ///     http://www.cescg.org/CESCG-2012/papers/Horvat-Ray-casting_point-in-polyhedron_test.pdf
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="vertexInQuestion">The vertex in question.</param>
        /// <param name="onBoundaryIsInside">if set to <c>true</c> [on boundary is inside].</param>
        /// <returns><c>true</c> if [is vertex inside solid] [the specified ts]; otherwise, <c>false</c>.</returns>
        public static bool IsVertexInsideSolid(TessellatedSolid ts, Vertex vertexInQuestion,
            bool onBoundaryIsInside = true)
        {
            //ToDo: Rewrite function to use plane list as in SolidIntersectionFunction
            var facesAbove = new List<PolygonalFace>();
            var facesBelow = new List<PolygonalFace>();
            var inconclusive = true;
            var rnd = new Random();
            //Added while inconclusive and random direction because there are some special cases that look the  
            //same. For instance, consider a vertex sitting at the center of a half moon. Along the z axis, 
            //It will go through 1 edge or vertex (special cases) above and one below. Then consider a box
            //centered on the origin. A point on the origin would point to an edge (of the two triangles
            //forming the face) above and one below. Therefore, it was decided that special cases (through
            //edges or locations, will yeild inconclusive results. 
            while (inconclusive)
            {
                inconclusive = false;
                var direction = Vector3.Normalize(new Vector3(rnd.NextDouble(), rnd.NextDouble(), rnd.NextDouble()));
                foreach (var face in ts.Faces)
                {
                    if (face.Vertices.Any(vertex => vertexInQuestion.X.IsPracticallySame(vertex.X) &&
                                                    vertexInQuestion.Y.IsPracticallySame(vertex.Y) &&
                                                    vertexInQuestion.Z.IsPracticallySame(vertex.Z)))
                    {
                        return onBoundaryIsInside;
                    }

                    var distanceToOrigin = face.Normal.Dot(face.Vertices[0].Coordinates);
                    var t = -(vertexInQuestion.Coordinates.Dot(face.Normal) - distanceToOrigin) /
                            direction.Dot(face.Normal);
                    //Note that if t == 0, then it is on the face
                    //else, find the intersection point and determine if it is inside the polygon (face)
                    var newVertex = t.IsNegligible()
                        ? vertexInQuestion
                        : new Vertex(vertexInQuestion.Coordinates + (direction * t));
                    if (!IsVertexInsideTriangle(face.Vertices, newVertex.Coordinates)) continue;
                    //If the distance between the vertex and a plane is neglible and the vertex is inside that face
                    if (t.IsNegligible())
                    {
                        return onBoundaryIsInside;
                    }
                    if (t > 0.0) //Face is higher on Z axis than vertex.
                    {
                        //Check to make sure no adjacent faces were already added to list (e.g., the projected vertex goes 
                        //through an edge).
                        var onAdjacentFace = face.AdjacentFaces.Any(adjacentFace => facesAbove.Contains(adjacentFace));
                        //Else, inconclusive (e.g., corners of cresent moon) 
                        if (!onAdjacentFace) facesAbove.Add(face);
                        else
                        {
                            inconclusive = true;
                            break;
                        }
                    }
                    else //Face is lower on Z axis than vertex.
                    {
                        //Check to make sure no adjacent faces were already added to list (e.g., the projected vertex goes 
                        //through an edge).
                        var onAdjacentFace = face.AdjacentFaces.Any(adjacentFace => facesBelow.Contains(adjacentFace));
                        if (!onAdjacentFace) facesBelow.Add(face);
                        else //Else, inconclusive (e.g., corners of cresent moon) 
                        {
                            inconclusive = true;
                            break;
                        }
                    }
                }
            }
            if (facesAbove.Count == 0 || facesBelow.Count == 0) return false;
            return facesAbove.Count % 2 != 0 && facesBelow.Count % 2 != 0;
            //Even number of intercepts, means the vertex is inside
        }

    }
    #endregion
}