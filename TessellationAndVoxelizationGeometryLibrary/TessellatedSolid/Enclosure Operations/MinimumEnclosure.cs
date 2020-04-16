﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 02-27-2015
//
// Last Modified By : Matt Campbell
// Last Modified On : 05-28-2016
// ***********************************************************************
// <copyright file="MinimumBoundingBox.cs" company="Design Engineering Lab">
//     Copyright ©  2014
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using TVGL.Numerics;
using TVGL.TwoDimensional;

namespace TVGL
{
    /// <summary>
    ///     The MinimumEnclosure class includes static functions for defining smallest enclosures for a
    ///     tesselated solid. For example: convex hull, minimum bounding box, or minimum bounding sphere.
    /// </summary>
    public static partial class MinimumEnclosure
    {
        /// <summary>
        ///     The maximum rotations for obb
        /// </summary>
        private const int MaxRotationsForOBB = 24;

        /// <summary>
        ///     Finds the minimum bounding rectangle given a set of points. Either send any set of points
        ///     OR the convex hull 2D.
        ///     Optional booleans for what information should be set in the Bounding Rectangle.
        ///     Example: If you really just need the area, you don't need the corner points or
        ///     points on side. 
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="pointsAreConvexHull">if set to <c>true</c> [points are convex hull].</param>
        /// <param name="setCornerPoints"></param>
        /// <param name="setPointsOnSide"></param>
        /// <returns>BoundingRectangle.</returns>
        public static BoundingRectangle BoundingRectangle(this IEnumerable<Vector2> polygon, bool pointsAreConvexHull = false,
            bool setCornerPoints = true, bool setPointsOnSide = true)
        {
            return RotatingCalipers2DMethod(polygon, pointsAreConvexHull, setCornerPoints, setPointsOnSide);
        }

        /// <summary>
        ///     Finds the minimum bounding box.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <returns>BoundingBox.</returns>
        public static BoundingBox OrientedBoundingBox(this TessellatedSolid ts)
        {
            return OrientedBoundingBox(ts.ConvexHull.Vertices.Any() ? ts.ConvexHull.Vertices : ts.Vertices);
        }

        /// <summary>
        ///     Finds the minimum bounding box.
        /// </summary>
        /// <param name="convexHullVertices">The convex hull vertices.</param>
        /// <returns>BoundingBox.</returns>
        public static BoundingBox OrientedBoundingBox(this IList<Vertex> convexHullVertices)
        {
            // here we create 13 directions. Why 13? basically it is all ternary combinations of x,y,and z.
            // skipping symmetric and 0,0,0. Another way to think of it is to make a Direction from a cube with
            // vectors emanating from every vertex, edge, and face. that would be 8+12+6 = 26. And since there
            // is no need to do mirror image directions this is 26/2 or 13.
            var directions = new List<Vector3>();
            for (var i = -1; i <= 1; i++)
                for (var j = -1; j <= 1; j++)
                    directions.Add(new Vector3(1.0, i, j).Normalize());
            directions.Add(new Vector3(0, 0, 1));
            directions.Add(new Vector3(0, 1, 0));
            directions.Add(new Vector3(0, 1, 1).Normalize());
            directions.Add(new Vector3(0, -1, 1).Normalize());

            var boxes = directions.Select(v => new BoundingBox(new Vector3(double.PositiveInfinity), new[] { v },
                new Vector3(double.NegativeInfinity))).ToList();
            for (var i = 0; i < 13; i++)
                boxes[i] = Find_via_ChanTan_AABB_Approach(convexHullVertices, boxes[i]);
            var minVolume = double.PositiveInfinity;
            var minBox = boxes[0];

            foreach (var box in boxes)
            {
                if (box.Volume >= minVolume) continue;
                minVolume = box.Volume;
                minBox = box;
            }
            return minBox;
        }

        #region ChanTan AABB Approach

        /// <summary>
        ///     Find_via_s the chan tan_ aab b_ approach.
        /// </summary>
        /// <param name="convexHullVertices">The convex hull vertices.</param>
        /// <param name="minOBB">The minimum obb.</param>
        /// <returns>BoundingBox.</returns>
        private static BoundingBox Find_via_ChanTan_AABB_Approach(IEnumerable<Vertex> convexHullVertices, BoundingBox minOBB)
        {
            var failedConsecutiveRotations = 0;
            var k = 0;
            var i = 0;
            do
            {
                //Find new OBB along OBB.direction2 and OBB.direction3, keeping the best OBB.
                var newObb = FindOBBAlongDirection(convexHullVertices, minOBB.Directions[i++]);
                if (newObb.Volume.IsLessThanNonNegligible(minOBB.Volume))
                {
                    minOBB = newObb;
                    failedConsecutiveRotations = 0;
                }
                else failedConsecutiveRotations++;
                if (i == 3) i = 0;
                k++;
            } while (failedConsecutiveRotations < 3 && k < MaxRotationsForOBB);
            return minOBB;
        }

        #endregion

        #region Get Length And Extreme Vertices

        /// <summary>
        ///     Given a Direction, dir, this function returns the maximum length along this Direction
        ///     for the provided vertices as well as the vertices that represent the extremes.
        /// </summary>
        /// <param name="direction">The direction.</param>
        /// <param name="vertices">The vertices.</param>
        /// <param name="bottomVertices">The bottom vertices.</param>
        /// <param name="topVertices">The top vertices.</param>
        /// <returns>System.Double.</returns>
        public static double GetLengthAndExtremeVertices(this IEnumerable<Vertex> vertices, Vector3 direction,
            out List<Vertex> bottomVertices,
            out List<Vertex> topVertices)
        {
            var dir = direction.Normalize();
            var minD = double.PositiveInfinity;
            bottomVertices = new List<Vertex>();
            topVertices = new List<Vertex>();
            var maxD = double.NegativeInfinity;
            foreach (var v in vertices)
            {
                var distance = dir.Dot(v.Coordinates);
                if (distance.IsPracticallySame(minD, Constants.BaseTolerance))
                    bottomVertices.Add(v);
                else if (distance < minD)
                {
                    bottomVertices.Clear();
                    bottomVertices.Add(v);
                    minD = distance;
                }
                if (distance.IsPracticallySame(maxD, Constants.BaseTolerance))
                    topVertices.Add(v);
                else if (distance > maxD)
                {
                    topVertices.Clear();
                    topVertices.Add(v);
                    maxD = distance;
                }
            }
            return maxD - minD;
        }

        /// <summary>
        ///     Given a Direction, dir, this function returns the maximum length along this Direction and one vertex 
        ///     that represents each extreme. Use this if you do not need all the vertices at the extremes.
        /// </summary>
        /// <param name="direction">The direction.</param>
        /// <param name="vertices">The vertices.</param>
        /// <param name="bottomVertex"></param>
        /// <param name="topVertex"></param>
        /// <returns>System.Double.</returns>
        public static double GetLengthAndExtremeVertex(this IEnumerable<Vertex> vertices, Vector3 direction,
            out Vertex bottomVertex,
            out Vertex topVertex)
        {
            var dir = direction.Normalize();
            var minD = double.PositiveInfinity;
            bottomVertex = null;
            topVertex = null;
            var maxD = double.NegativeInfinity;
            foreach (var v in vertices)
            {
                var distance = dir.Dot(v.Coordinates);
                if (distance < minD)
                {
                    bottomVertex = v;
                    minD = distance;
                }
                if (distance > maxD)
                {
                    topVertex = v;
                    maxD = distance;
                }
            }
            return maxD - minD;
        }

        /// <summary>
        ///     Given a Direction, dir, this function returns the maximum length along this Direction
        ///     for the provided points as well as the points that represent the extremes.
        /// </summary>
        /// <param name="direction2D">The direction.</param>
        /// <param name="points">The vertices.</param>
        /// <param name="bottomPoints">The bottom vertices.</param>
        /// <param name="topPoints">The top vertices.</param>
        /// <returns>System.Double.</returns>
        public static double GetLengthAndExtremePoints(this IEnumerable<Vector2> points,Vector2 direction2D,
            out List<Vector2> bottomPoints,
            out List<Vector2> topPoints)
        {
            var minD = double.PositiveInfinity;
            bottomPoints = new List<Vector2>();
            topPoints = new List<Vector2>();
            var maxD = double.NegativeInfinity;
            foreach (var point in points)
            {
                var distance = direction2D.Dot(point);
                if (distance.IsPracticallySame(minD, Constants.BaseTolerance))
                    bottomPoints.Add(point);
                else if (distance < minD)
                {
                    bottomPoints.Clear();
                    bottomPoints.Add(point);
                    minD = distance;
                }
                if (distance.IsPracticallySame(maxD, Constants.BaseTolerance))
                    bottomPoints.Add(point);
                else if (distance > maxD)
                {
                    topPoints.Clear();
                    topPoints.Add(point);
                    maxD = distance;
                }
            }
            return maxD - minD;
        }

        #endregion

        #region 2D Rotating Calipers
        private static readonly double[] CaliperOffsetAngles = new[] { Math.PI / 2, Math.PI, -Math.PI / 2, 0.0 };

        /// <summary>
        ///     Rotating the calipers 2D method. Convex hull must be a counter clockwise loop.
        ///     Optional booleans for what information should be set in the Bounding Rectangle.
        ///     Example: If you really just need the area, you don't need the corner points or
        ///     points on side. 
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="pointsAreConvexHull">if set to <c>true</c> [points are convex hull].</param>
        /// <param name="setCornerPoints"></param>
        /// <param name="setPointsOnSide"></param>
        /// <returns>System.Double.</returns>
        /// <exception cref="Exception">
        ///     Area should never be negligible unless data is messed up.
        /// </exception>
        private static BoundingRectangle RotatingCalipers2DMethod(IEnumerable<Vector2> points,
            bool pointsAreConvexHull = false, bool setCornerPoints = true,
            bool setPointsOnSide = true)
        {
            var cvxPoints = pointsAreConvexHull
                ? (points is IList<Vector2>) ? (IList<Vector2>)points : points.ToList()
                : ConvexHull2D(points).ToList();
            if (cvxPoints.Count < 3) throw new Exception("Rotating Calipers requires at least 3 points.");

            //Simplify the points to make sure they are the minimal convex hull
            //Only set it as the convex hull if it contains more than three points.
            var cvxPointsSimple = PolygonOperations.Simplify(cvxPoints);
            if (cvxPointsSimple.Count >= 3) cvxPoints = cvxPointsSimple;
            /* the cvxPoints will be arranged from a point with minimum X-value around in a CCW loop to the last point */
            //First, check to make sure the given convex hull has the min x-value at 0.
            var minX = cvxPoints[0].X;
            var numCvxPoints = cvxPoints.Count;
            var startIndex = 0;
            for (var i = 1; i < numCvxPoints; i++)
            {
                if (!(cvxPoints[i].X < minX)) continue;
                minX = cvxPoints[i].X;
                startIndex = i;
            }
            //Reorder if necessary
            var tempList = new List<Vector2>();
            if (startIndex != 0)
            {
                for (var i = startIndex; i < numCvxPoints; i++)
                {
                    tempList.Add(cvxPoints[i]);
                }
                for (var i = 0; i < startIndex; i++)
                {
                    tempList.Add(cvxPoints[i]);
                }
                cvxPoints = tempList;
            }

            #region Get Extreme Points
            var extremeIndices = new int[4];

            //Good picture of extreme vertices in the following link
            //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.155.5671&rep=rep1&type=pdf
            //Godfried Toussaint: Solving Geometric Problems with the Rotating Calipers
            //Note that while these points are ordered counter clockwise, we are rotating the calipers in reverse (clockwise),
            //Which is why the points are directed this way.
            //Point0 = min X, with max Y for ties
            //Point1 = min Y, with min X for ties
            //Point2 = max X, with min Y for ties
            //Point3 = max Y, with max X for ties

            // extremeIndices[3] => max-Y, with max X for ties
            extremeIndices[3] = cvxPoints.Count - 1;
            // this is likely rare, but first we check if the first point has a higher y value (only when point is both min-x and max-Y)
            if (cvxPoints[0].Y > cvxPoints[extremeIndices[3]].Y) extremeIndices[3] = 0;
            else
            {
                while (extremeIndices[3] > 0 && cvxPoints[extremeIndices[3]].Y <= cvxPoints[extremeIndices[3] - 1].Y)
                    extremeIndices[3]--;
            }
            /* at this point, the max-Y point has been established. Next we walk backwards in the list until we hit the max-X point */
            // extremeIndices[2] => max-X, with min Y for ties
            extremeIndices[2] = extremeIndices[3] == 0 ? cvxPoints.Count - 1 : extremeIndices[3];
            while (extremeIndices[2] > 0 && cvxPoints[extremeIndices[2]].X <= cvxPoints[extremeIndices[2] - 1].X)
                extremeIndices[2]--;
            // extremeIndices[1] => min-Y, with min X for ties 
            extremeIndices[1] = extremeIndices[2] == 0 ? cvxPoints.Count - 1 : extremeIndices[2];
            while (extremeIndices[1] > 0 && cvxPoints[extremeIndices[1]].Y >= cvxPoints[extremeIndices[1] - 1].Y)
                extremeIndices[1]--;
            // extrememIndices[0] => min-X, with max Y for ties
            // First we check if the last point has an eqaully small x value, if it does we will need to walk backwards.
            if (cvxPoints.Last().X > cvxPoints[0].X) extremeIndices[0] = 0;
            else
            {
                extremeIndices[0] = cvxPoints.Count - 1;
                while (cvxPoints[extremeIndices[0]].X >= cvxPoints[extremeIndices[0] - 1].X)
                    extremeIndices[0]--;
            }
            #endregion

            var bestRectangle = new BoundingRectangle { Area = double.MaxValue };
            var PointsOnSides = new List<Vector2>[4];

            #region Cycle through 90-degrees
            var deltaAngles = new double[4];
            do
            {
                #region update the deltaAngles from the current orientation

                //For each of the 4 supporting points (those forming the rectangle),
                var minAngle = double.PositiveInfinity;
                var refIndex = 0;
                for (var i = 0; i < 4; i++)
                {
                    var index = extremeIndices[i];
                    var prev = index == 0 ? cvxPoints[numCvxPoints - 1] : cvxPoints[index - 1];
                    var current = cvxPoints[index];
                    var tempDelta = Math.Atan2(prev.Y - current.Y, prev.X - current.X);
                    var angle = CaliperOffsetAngles[i] - tempDelta;

                    //If the angle has rotated beyond the 90 degree bounds, it will be negative
                    //And should never be chosen from then on.
                    if (angle < 0) deltaAngles[i] = double.PositiveInfinity;
                    else
                    {
                        deltaAngles[i] = angle;
                        if (angle < minAngle)
                        {
                            minAngle = angle;
                            refIndex = i;
                        }
                    }
                }
                if (minAngle.IsGreaterThanNonNegligible(Math.PI / 2)) break;

                #endregion

                #region find area

                //Get unit normal for current edge
                var otherIndex = extremeIndices[refIndex] == 0 ? numCvxPoints - 1 : extremeIndices[refIndex] - 1;
                var direction = (cvxPoints[extremeIndices[refIndex]] - cvxPoints[otherIndex]).Normalize();
                //If point type = 1 or 3, then use inversed Direction
                if (refIndex == 1 || refIndex == 3)
                {
                    direction = new Vector2(-direction.Y, direction.X);
                }
                var vectorLength = new Vector2(cvxPoints[extremeIndices[2]].X - cvxPoints[extremeIndices[0]].X,
                    cvxPoints[extremeIndices[2]].Y - cvxPoints[extremeIndices[0]].Y);

                var angleVector1 = new Vector2(-direction.Y, direction.X);
                var length = Math.Abs(vectorLength.Dot(angleVector1));
                var vectorWidth = new Vector2(cvxPoints[extremeIndices[3]].X - cvxPoints[extremeIndices[1]].X,
                    cvxPoints[extremeIndices[3]].Y - cvxPoints[extremeIndices[1]].Y);
                var angleVector2 = new Vector2(direction.X, direction.Y);
                var width = Math.Abs(vectorWidth.Dot(angleVector2));
                var area = length * width;

                #endregion

                var d1Max = double.MinValue;
                var d1Min = double.MaxValue;
                var d2Max = double.MinValue;
                var d2Min = double.MaxValue;
                var pointsOnSides = new List<Vector2>[4];
                for (var i = 0; i < 4; i++)
                {
                    pointsOnSides[i] = new List<Vector2>();
                    var dir = i % 2 == 0 ? angleVector1 : angleVector2;
                    var distance = cvxPoints[extremeIndices[i]].Dot(dir);
                    if (i % 2 == 0) //D1
                    {
                        if (distance > d1Max) d1Max = distance;
                        if (distance < d1Min) d1Min = distance;
                    }
                    else //D2
                    {
                        if (distance > d2Max) d2Max = distance;
                        if (distance < d2Min) d2Min = distance;
                    }
                    var prevIndex = extremeIndices[i];
                    do
                    {
                        extremeIndices[i] = prevIndex;
                        if (setPointsOnSide)
                        {
                            pointsOnSides[i].Add(cvxPoints[extremeIndices[i]]);
                        }
                        prevIndex = extremeIndices[i] == 0 ? numCvxPoints - 1 : extremeIndices[i] - 1;
                    } while (distance.IsPracticallySame(cvxPoints[prevIndex].Dot(dir),
                        Constants.BaseTolerance));
                }

                //If this is an improvement, set the parameters for the best bounding rectangle.
                if (area < bestRectangle.Area)
                {
                    bestRectangle.Area = area;
                    bestRectangle.Length = length; //Lenght corresponds with direction 1.
                    bestRectangle.Width = width;
                    bestRectangle.LengthDirection = angleVector1;
                    bestRectangle.WidthDirection = angleVector2;
                    bestRectangle.LengthDirectionMax = d1Max;
                    bestRectangle.LengthDirectionMin = d1Min;
                    bestRectangle.WidthDirectionMax = d2Max;
                    bestRectangle.WidthDirectionMin = d2Min;
                    PointsOnSides = pointsOnSides;
                }
            } while (true); //process will end on its own by the break statement in line 314

            #endregion

            if (bestRectangle.Area.IsNegligible())
            {
                var polygon = new Polygon(cvxPoints);
                var allPoints = new List<Vector2>(points);
                if (!polygon.IsConvex())
                {
                    var c = 0;
                    var random = new Random(1);//Use a specific random generator to make this repeatable                  
                    var pointCount = allPoints.Count;
                    while (pointCount > 10 && c < 10) //Ten points would be ideal
                    {
                        //Remove a random point
                        var max = pointCount - 1;
                        var index = random.Next(0, max);
                        var point = allPoints[index];
                        allPoints.RemoveAt(index);

                        //Check if it is still invalid
                        var newConvexHull = ConvexHull2D(allPoints).ToList();
                        polygon = new Polygon(newConvexHull);
                        if (polygon.IsConvex())
                        {
                            //Don't remove the point
                            c++;
                            allPoints.Insert(index, point);
                        }
                        else pointCount--;
                    }
                }

                //var polyLight = new Polygon(allPoints);
                //var date = DateTime.Now.ToString("MM.dd.yy_HH.mm");
                //polyLight.Serialize("ConvexHullError_" + date + ".PolyLight");
                //var cvxHullLight = polygon.Path;
                //cvxHullLight.Serialize("ConvexHull_" + date + ".PolyLight");
                throw new Exception("Error in Minimum Bounding Box, likely due to faulty convex hull.");
            }
            if (setCornerPoints)
                bestRectangle.SetCornerPoints();
            if (setPointsOnSide)
                bestRectangle.PointsOnSides = PointsOnSides;
            return bestRectangle;
        }

        #endregion



        #region Find OBB Along Direction

        /// <summary>
        ///     Finds the minimum oriented bounding rectangle (2D). The 3D points of a tessellated solid
        ///     are projected to the plane defined by "Direction". This returns a BoundingBox structure
        ///     where the first Direction is the same as the prescribed Direction and the other two are
        ///     in-plane unit vectors.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The Direction.</param>
        /// <returns>BoundingBox.</returns>
        /// <exception cref="Exception">Volume should never be negligible, unless the input data is bad</exception>
        /// <exception cref="System.Exception"></exception>
        public static BoundingBox OBBAlongDirection(IList<Vertex> vertices, Vector3 direction)
        {
            var boundingBox = FindOBBAlongDirection(vertices, direction);
            return boundingBox;
        }

        private static BoundingBox FindOBBAlongDirection(IEnumerable<Vertex> vertices, Vector3 direction)
        {
            var direction1 = direction.Normalize();
            var depth = GetLengthAndExtremeVertices(vertices, direction, out var bottomVertices, out var topVertices);

            var pointsDict = vertices.ProjectVerticesTo2DCoordinatesWithRefToVertices(direction, out var backTransform);
            var boundingRectangle = RotatingCalipers2DMethod(pointsDict.Keys.ToList());

            //Get the Direction vectors from rotating caliper and projection.
            var tempDirection = new Vector3(boundingRectangle.LengthDirection.X, boundingRectangle.LengthDirection.Y, 0);
            tempDirection = tempDirection.Transform(backTransform);
            var direction2 = new Vector3(tempDirection[0], tempDirection[1], tempDirection[2]);
            tempDirection = new Vector3(boundingRectangle.WidthDirection.X, boundingRectangle.WidthDirection.Y, 0);
            var direction3 = tempDirection.Transform(backTransform);
            var verticesOnFaces = new List<List<Vertex>>
            {
                bottomVertices,
                topVertices,
                boundingRectangle.PointsOnSides[0].SelectMany(p =>pointsDict[p]).ToList(),
                boundingRectangle.PointsOnSides[1].SelectMany(p =>pointsDict[p]).ToList(),
                boundingRectangle.PointsOnSides[2].SelectMany(p => pointsDict[p]).ToList(),
                boundingRectangle.PointsOnSides[3].SelectMany(p => pointsDict[p]).ToList()
            };
            if ((depth * boundingRectangle.Length * boundingRectangle.Width).IsNegligible())
                throw new Exception("Volume should never be negligible, unless the input data is bad");
            //var dim2 = GetLengthAndExtremeVertices(direction2, vertices, out bottomVertices, out topVertices);
            //var dim3 = GetLengthAndExtremeVertices(direction3, vertices, out bottomVertices, out topVertices);
            //if (!dim2.IsPracticallySame(boundingRectangle.Dimensions[0], 0.000001)) throw new Exception("Error in implementation");
            //if (!dim3.IsPracticallySame(boundingRectangle.Dimensions[1], 0.000001)) throw new Exception("Error in implementation");
            var minBox = new BoundingBox(new Vector3(depth, boundingRectangle.Length, boundingRectangle.Width),
                new[] { direction1, direction2, direction3 }, bottomVertices[0].Coordinates,
                verticesOnFaces[2][0].Coordinates, verticesOnFaces[4][0].Coordinates);
            minBox.PointsOnFaces = verticesOnFaces.ToArray();
            return minBox;
        }

        #endregion
    }
}