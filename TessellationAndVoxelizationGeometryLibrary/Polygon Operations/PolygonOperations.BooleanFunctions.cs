﻿using System;
using System.Collections.Generic;
using System.Linq;
using TVGL.Numerics;

namespace TVGL.TwoDimensional
{
    /// <summary>
    /// A set of general operation for points and paths
    /// </summary>
    public static partial class PolygonOperations
    {

        #region Union Public Methods
        /// <summary>
        /// Returns the list of polygons that exist in either A OR B.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Union(this Polygon polygonA, Polygon polygonB)
        {
            var relationship = GetPolygonRelationshipAndIntersections(polygonA, polygonB, out var intersections);
            return Union(polygonA, polygonB, relationship, intersections);
        }
        /// <summary>
        /// Returns the list of polygons that exist in either A OR B.By providing the intersections
        /// between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="polygonRelationship">The polygon relationship.</param>
        /// <param name="intersections">The intersections.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Union(this Polygon polygonA, Polygon polygonB, PolygonRelationship polygonRelationship, List<IntersectionData> intersections)
        {
            switch (polygonRelationship)
            {
                case PolygonRelationship.Separated:
                    return new List<Polygon> { polygonA.Copy(), polygonB.Copy() };
                case PolygonRelationship.BIsCompletelyInsideA:
                case PolygonRelationship.BInsideAButBordersTouch:
                    var polygonACopy = polygonA.Copy();
                    if (!polygonB.IsPositive)
                        polygonACopy.AddHole(polygonB.Copy());
                    return new List<Polygon> { polygonACopy };
                case PolygonRelationship.AIsCompletelyInsideB:
                case PolygonRelationship.AInsideBButBordersTouch:
                    var polygonBCopy = polygonB.Copy();
                    if (!polygonA.IsPositive)
                        polygonBCopy.AddHole(polygonA.Copy());
                    return new List<Polygon> { polygonBCopy };
                case PolygonRelationship.SeparatedButBordersTouch:
                    if (intersections.All(intersect => (intersect.Relationship & PolygonSegmentRelationship.CoincidentLines) == 0b0))
                        return new List<Polygon> { polygonA.Copy(), polygonB.Copy() };
                    return BooleanOperation(polygonA, polygonB, intersections, false, true, false);
                //case PolygonRelationship.Intersect:
                default:
                    return BooleanOperation(polygonA, polygonB, intersections, false, true, false);
            }
        }
        /// <summary>
        /// Returns the list of polygons that are the subshapes of ANY of the provided polygons.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Union(this IEnumerable<Polygon> polygons)
        {
            var polygonList = polygons.ToList();
            //bool unionFound;
            //do
            //{
            //unionFound = false;
            for (int i = polygonList.Count - 1; i > 0; i--)
            {
                Presenter.ShowAndHang(polygonList);
                for (int j = i - 1; j >= 0; j--)
                {
                    var polygonRelationship = GetPolygonRelationshipAndIntersections(polygonList[i],
                        polygonList[j], out var intersections);
                    if (polygonRelationship == PolygonRelationship.Separated ||
                        (polygonRelationship == PolygonRelationship.SeparatedButBordersTouch &&
                        intersections.All(intersect => (intersect.Relationship & PolygonSegmentRelationship.CoincidentLines) == 0b0)))
                        continue;
                    var newPolygons = Union(polygonList[i], polygonList[j], polygonRelationship, intersections);
                    polygonList.RemoveAt(i);
                    polygonList.RemoveAt(j);
                    polygonList.AddRange(newPolygons);
                    //unionFound = true;
                    i = polygonList.Count; // to stop the outer loop
                    break; // to stop the inner loop
                }
            }
            //} while (unionFound) ;
            return polygonList;
        }
        #endregion

        #region Intersect Public Methods
        /// <summary>
        /// Returns the list of polygons that result from the subshapes common to both A and B. 
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="minAllowableArea">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Intersect(this Polygon polygonA, Polygon polygonB, double minAllowableArea = Constants.BaseTolerance)
        {
            var relationship = GetPolygonRelationshipAndIntersections(polygonA, polygonB, out var intersections);
            return Intersect(polygonA, polygonB, relationship, intersections, minAllowableArea);
        }

        /// <summary>
        /// Returns the list of polygons that result from the subshapes common to both A and B. By providing the intersections
        /// between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="polygonRelationship">The polygon relationship.</param>
        /// <param name="intersections">The intersections.</param>
        /// <param name="minAllowableArea">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Intersect(this Polygon polygonA, Polygon polygonB, PolygonRelationship polygonRelationship, List<IntersectionData> intersections, double minAllowableArea = Constants.BaseTolerance)
        {
            switch (polygonRelationship)
            {
                case PolygonRelationship.SeparatedButBordersTouch:
                case PolygonRelationship.Separated:
                case PolygonRelationship.AIsInsideHoleOfB:
                case PolygonRelationship.BIsInsideHoleOfA:
                    return new List<Polygon>();
                case PolygonRelationship.BIsCompletelyInsideA:
                case PolygonRelationship.BInsideAButBordersTouch:
                    if (polygonB.IsPositive) return new List<Polygon> { polygonB.Copy() };
                    else
                    {
                        var polygonACopy = polygonA.Copy();
                        polygonACopy.AddHole(polygonB.Copy());
                        return new List<Polygon> { polygonACopy };
                    }
                case PolygonRelationship.AIsCompletelyInsideB:
                case PolygonRelationship.AInsideBButBordersTouch:
                    if (polygonA.IsPositive) return new List<Polygon> { polygonA.Copy() };
                    else
                    {
                        var polygonBCopy = polygonB.Copy();
                        polygonBCopy.AddHole(polygonA.Copy());
                        return new List<Polygon> { polygonBCopy };
                    }
                //case PolygonRelationship.Intersect:
                default:
                    return BooleanOperation(polygonA, polygonB, intersections, false, false, false, minAllowableArea);
            }
        }

        /// <summary>
        /// Returns the list of polygons that are the subshapes of ALL of the provided polygons.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Intersect(this IEnumerable<Polygon> polygons)
        {
            var polygonList = polygons.ToList();
            var numPolygons = -1;
            while (true) // the same condition fromthe Union operation won't always work for intersect
                         //numPolygons != polygonList.Count
                         // this is because one could intersect two object and get two new objects
            {
                numPolygons = polygonList.Count;
                var relationships = new PolygonRelationship[numPolygons / 2];
                var allIntersections = new List<IntersectionData>[numPolygons / 2];
                for (int i = 0; i < polygonList.Count; i += 2)
                {
                    var polygonRelationship = GetPolygonRelationshipAndIntersections(polygonList[i + 1],
                        polygonList[i], out var intersections);
                    if (polygonRelationship == 0) return new List<Polygon>();
                    relationships[i / 2] = polygonRelationship;
                    allIntersections[i / 2] = intersections;
                }
                var indices = Enumerable.Range(0, numPolygons / 2);

                polygonList = indices.AsParallel().SelectMany(index => Union(polygonList[2 * index + 1], polygonList[2 * index],
                    relationships[index], allIntersections[index])).ToList();
            }
            return polygonList;
        }

        #endregion

        #region Subtract Public Methods
        /// <summary>
        /// Returns the list of polygons that result from A-B (subtracting polygon B from polygon A). 
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="minAllowableArea">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Subtract(this Polygon polygonA, Polygon polygonB, double minAllowableArea = Constants.BaseTolerance)
        {
            var relationship = GetPolygonRelationshipAndIntersections(polygonA, polygonB, out var intersections);
            return Subtract(polygonA, polygonB, relationship, intersections, minAllowableArea);
        }

        /// <summary>
        /// Returns the list of polygons that result from A-B (subtracting polygon B from polygon A). By providing the intersections
        /// between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="polygonRelationship">The polygon relationship.</param>
        /// <param name="intersections">The intersections.</param>
        /// <param name="minAllowableArea">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Subtract(this Polygon polygonA, Polygon polygonB,
                    PolygonRelationship polygonRelationship, List<IntersectionData> intersections,
                    double minAllowableArea = Constants.BaseTolerance)
        {
            switch (polygonRelationship)
            {
                case PolygonRelationship.Separated:
                case PolygonRelationship.SeparatedButBordersTouch:
                case PolygonRelationship.BIsInsideHoleOfA:
                    return new List<Polygon> { polygonA.Copy() };
                case PolygonRelationship.BIsCompletelyInsideA:
                    var polygonACopy = polygonA.Copy();
                    if (polygonB.IsPositive)
                    {
                        var polygonBCopy = polygonB.Copy();
                        polygonBCopy.Reverse();
                        polygonACopy.AddHole(polygonBCopy);
                    }
                    return new List<Polygon> { polygonACopy };
                case PolygonRelationship.AIsCompletelyInsideB:
                case PolygonRelationship.AInsideBButBordersTouch:
                case PolygonRelationship.AIsInsideHoleOfB:
                    return new List<Polygon>();
                default:
                    //case PolygonRelationship.Intersect:
                    //case PolygonRelationship.BInsideAButBordersTouch:
                    return BooleanOperation(polygonA, polygonB, intersections, true, false, false, minAllowableArea);
            }
        }

        #endregion

        #region Exclusive-OR Public Methods
        /// <summary>
        /// Returns the list of polygons that are the Exclusive-OR of the two input polygons. Exclusive-OR are the regions where one polgyon
        /// resides but not both.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="minAllowableArea">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> ExclusiveOr(this Polygon polygonA, Polygon polygonB, double minAllowableArea = Constants.BaseTolerance)
        {
            var relationship = GetPolygonRelationshipAndIntersections(polygonA, polygonB, out var intersections);
            return ExclusiveOr(polygonA, polygonB, relationship, intersections, minAllowableArea);
        }

        /// <summary>
        /// Returns the list of polygons that are the Exclusive-OR of the two input polygons. Exclusive-OR are the regions where one polgyon
        /// resides but not both. By providing the intersections between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="polygonRelationship">The polygon relationship.</param>
        /// <param name="intersections">The intersections.</param>
        /// <param name="minAllowableArea">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> ExclusiveOr(this Polygon polygonA, Polygon polygonB, PolygonRelationship polygonRelationship,
                    List<IntersectionData> intersections, double minAllowableArea = Constants.BaseTolerance)
        {
            switch (polygonRelationship)
            {
                case PolygonRelationship.Separated:
                case PolygonRelationship.SeparatedButBordersTouch:
                case PolygonRelationship.AIsInsideHoleOfB:
                case PolygonRelationship.BIsInsideHoleOfA:
                    return new List<Polygon> { polygonA.Copy(), polygonB.Copy() };
                case PolygonRelationship.BIsCompletelyInsideA:
                    var polygonACopy1 = polygonA.Copy();
                    if (polygonB.IsPositive)
                    {
                        var polygonBCopy1 = polygonB.Copy();
                        polygonBCopy1.Reverse();
                        polygonACopy1.AddHole(polygonBCopy1);
                    }
                    return new List<Polygon> { polygonACopy1 };
                case PolygonRelationship.AIsCompletelyInsideB:
                    var polygonBCopy2 = polygonB.Copy();
                    if (polygonA.IsPositive)
                    {
                        var polygonACopy2 = polygonA.Copy();
                        polygonACopy2.Reverse();
                        polygonBCopy2.AddHole(polygonACopy2);
                    }
                    return new List<Polygon> { polygonBCopy2 };
                //case PolygonRelationship.Intersect:
                //case PolygonRelationship.AInsideBButBordersTouch:
                //case PolygonRelationship.BInsideAButBordersTouch:
                default:
                    return BooleanOperation(polygonA, polygonB, intersections, true, false, true, minAllowableArea);
            }
        }
        #endregion

        #region RemoveSelfIntersections Public Method
        public static List<Polygon> RemoveSelfIntersections(this Polygon polygon, double minAllowableArea = Constants.BaseTolerance)
        {
            return RemoveSelfIntersections(polygon, polygon.GetSelfIntersections(), minAllowableArea);
        }
        public static List<Polygon> RemoveSelfIntersections(this Polygon polygon, List<IntersectionData> intersections,
            double minAllowableArea = Constants.BaseTolerance)
        {
            var isSubtract = false;
            var isUnion = true;
            var boothApproachDirections = true;
            if (intersections.Count == 0) return new List<Polygon> { polygon.Copy() };
            var intersectionLookup = MakeIntersectionLookupList(polygon.Lines.Count, intersections);
            var positivePolygons = new SortedDictionary<double, Polygon>(new NoEqualSort()); //store positive polygons in increasing area
            var negativePolygons = new SortedDictionary<double, Polygon>(new NoEqualSort()); //store negative in increasing (from -inf to 0) area
            while (GetNextStartingIntersection(intersections, isSubtract, isUnion, boothApproachDirections, out var startingIntersection,
                out var startEdge))
            {
                var polyCoordinates = MakePolygonThroughIntersections(intersectionLookup, intersections, startingIntersection,
                    startEdge, isSubtract, isUnion).ToList();
                var area = polyCoordinates.Area();
                if (area.IsNegligible(minAllowableArea)) continue;
                if (area < 0) negativePolygons.Add(area, new Polygon(polyCoordinates));
                else positivePolygons.Add(area, new Polygon(polyCoordinates));
            }
            return CreateShallowPolygonTreesPostBooleanOperation(positivePolygons.Values.ToList(), negativePolygons.Values);
        }
        #endregion

        #region Private Functions used by the above public methods
        /// <summary>
        /// All of the previous boolean operations are accomplished by this function. Note that the function RemoveSelfIntersections is also
        /// very simliar to this function.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="intersections">The intersections.</param>
        /// <param name="isSubtract">The switch direction.</param>
        /// <param name="crossProductSign">The cross product sign.</param>
        /// <param name="minAllowableArea">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        private static List<Polygon> BooleanOperation(this Polygon polygonA, Polygon polygonB, List<IntersectionData> intersections, bool isSubtract,
                    bool isUnion, bool bothApproachDirections, double minAllowableArea = Constants.BaseTolerance)
        {
            var id = 0;
            foreach (var polygon in polygonA.AllPolygons)
            {
                foreach (var vertex in polygon.Vertices)
                    vertex.IndexInList = id++;
            }
            // temporarily number the vertices so that each has a unique number. this is important for the Intersection Lookup List
            foreach (var polygon in polygonB.AllPolygons)
            {
                foreach (var vertex in polygon.Vertices)
                    vertex.IndexInList = id++;
            }
            var intersectionLookup = MakeIntersectionLookupList(id, intersections);
            var positivePolygons = new SortedDictionary<double, Polygon>(new NoEqualSort()); //store positive polygons in increasing area
            var negativePolygons = new SortedDictionary<double, Polygon>(new NoEqualSort()); //store negative in increasing (from -inf to 0) area
            while (GetNextStartingIntersection(intersections, isSubtract, isUnion, bothApproachDirections, out var startingIntersection,
                out var startEdge))
            {
                var polyCoordinates = MakePolygonThroughIntersections(intersectionLookup, intersections, startingIntersection,
                    startEdge, isSubtract, isUnion).ToList();
                var area = polyCoordinates.Area();
                if (area.IsNegligible(minAllowableArea)) continue;
                if (area < 0) negativePolygons.Add(area, new Polygon(polyCoordinates));
                else positivePolygons.Add(area, new Polygon(polyCoordinates));
            }
            // reset ids for polygon B
            id = 0;
            foreach (var vertex in polygonB.Vertices)
                vertex.IndexInList = id++;

            return CreateShallowPolygonTreesPostBooleanOperation(positivePolygons.Values.ToList(), negativePolygons.Values);
        }

        /// <summary>
        /// Gets the next intersection by looking through the intersectionLookupList. It'll return false, when there are none left.
        /// </summary>
        /// <param name="intersectionLookup">The intersection lookup.</param>
        /// <param name="intersections">The intersections.</param>
        /// <param name="crossProductSign">The cross product sign.</param>
        /// <param name="nextStartingIntersection">The next starting intersection.</param>
        /// <param name="currentEdge">The current edge.</param>
        /// <returns><c>true</c> if a new starting intersection was found, <c>false</c> otherwise.</returns>
        /// <exception cref="NotImplementedException"></exception>
        private static bool GetNextStartingIntersection(List<IntersectionData> intersections, bool isSubtract, bool isUnion, bool bothApproachDirections,
            out IntersectionData nextStartingIntersection, out PolygonSegment currentEdge)
        {
            foreach (var intersectionData in intersections)
            {
                #region first some conditions that tell us to skip this intersection
                if (intersectionData.Visited) continue;
                if ((intersectionData.Relationship & PolygonSegmentRelationship.CoincidentLines) != 0b0)
                {  // this addresses the special cases where lines are coincident
                    if ((intersectionData.Relationship & (PolygonSegmentRelationship.SameLineBeforePoint | PolygonSegmentRelationship.SameLineAfterPoint))
                        == (PolygonSegmentRelationship.SameLineBeforePoint | PolygonSegmentRelationship.SameLineAfterPoint))
                        continue;
                    if (!isSubtract && (intersectionData.Relationship & PolygonSegmentRelationship.OppositeDirections) == 0b0)
                        // if it is in the same direction (OppositeDirections bit is 0), then that won't work unless it's subtract 
                        continue;
                    if (isSubtract && (((intersectionData.Relationship & PolygonSegmentRelationship.OppositeDirections) != 0b0)
                        || ((intersectionData.Relationship & PolygonSegmentRelationship.SameLineBeforePoint) != 0b0)))
                        // if it's subtract then they should be in the same direction, but we're only going to set current as the minuend
                        // so we can also remove cases where the intersection is the same point before the point (that's a bit of a confusingness)
                        continue;
                }

                #endregion

                // now look at 00 ("glance"), 11 ("overlapping"), 10 ("A encompasses B"), and 01 ("B encompasses A")
                #region "Glance" A and B touch but are only abutting one another. no overlap in regions
                if (isUnion && (intersectionData.Relationship & PolygonSegmentRelationship.Overlapping) == 0b0 &&
                    (intersectionData.Relationship & PolygonSegmentRelationship.OppositeDirections) != 0b0 &&
                    (intersectionData.Relationship & PolygonSegmentRelationship.CoincidentLines) != 0b0)
                { //the only time non-overlapping intersections are intereseting is when we are doing union and lines are coincident
                  // otherwise you simply stay on the same polygon you enter with
                    if (((intersectionData.Relationship & PolygonSegmentRelationship.SameLineBeforePoint) != 0b0 &&
                         (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfA) != 0b0)
                        ||
                        ((intersectionData.Relationship & PolygonSegmentRelationship.SameLineAfterPoint) != 0b0 &&
                          (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfB) != 0b0))
                    {
                        currentEdge = intersectionData.EdgeB;
                        nextStartingIntersection = intersectionData;
                        return true;
                    }
                    else if (((intersectionData.Relationship & PolygonSegmentRelationship.SameLineBeforePoint) != 0b0 &&
                              (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfB) != 0b0)
                             ||
                             ((intersectionData.Relationship & PolygonSegmentRelationship.SameLineAfterPoint) != 0b0 &&
                              (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfA) != 0b0))
                    {
                        currentEdge = intersectionData.EdgeA;
                        nextStartingIntersection = intersectionData;
                        return true;
                    }
                }
                #endregion
                #region Overlapping. The conventional case where A and B cross into one another
                else if ((intersectionData.Relationship & PolygonSegmentRelationship.Overlapping) == PolygonSegmentRelationship.Overlapping)
                {
                    var cross = intersectionData.EdgeA.Vector.Cross(intersectionData.EdgeB.Vector);
                    var switchSign = (isSubtract || isUnion) ? 1 : -1;
                    if (switchSign * cross < 0 && (!isSubtract || bothApproachDirections ||
                        intersectionData.EdgeA.IndexInList < intersectionData.EdgeB.IndexInList))
                    {
                        currentEdge = intersectionData.EdgeA;
                        nextStartingIntersection = intersectionData;
                        return true;
                    }
                    if (switchSign * cross > 0 && (!isSubtract || bothApproachDirections ||
                        intersectionData.EdgeB.IndexInList < intersectionData.EdgeA.IndexInList))
                    {
                        currentEdge = intersectionData.EdgeB;
                        nextStartingIntersection = intersectionData;
                        return true;
                    }
                }
                #endregion
                #region Polygon A encompasses all of polygon B at this intersection 
                else if (isSubtract && (bothApproachDirections || intersectionData.EdgeA.IndexInList < intersectionData.EdgeB.IndexInList) &&
                    (intersectionData.Relationship & PolygonSegmentRelationship.AEncompassesB) != 0b0)
                {
                    currentEdge = intersectionData.EdgeA;
                    nextStartingIntersection = intersectionData;
                    return true;

                }
                #endregion
                #region Polygon B encompasses all of polygon A at this intersection

                else if (isSubtract && (bothApproachDirections || intersectionData.EdgeB.IndexInList < intersectionData.EdgeA.IndexInList) &&
                    (intersectionData.Relationship & PolygonSegmentRelationship.BEncompassesA) != 0b0)
                {
                    currentEdge = intersectionData.EdgeB;
                    nextStartingIntersection = intersectionData;
                    return true;
                }
                #endregion
            }
            nextStartingIntersection = null;
            currentEdge = null;
            return false;
        }

        /// <summary>
        /// Makes the polygon through intersections. This is actually the heart of the matter here. The method is the main
        /// while loop that switches between segments everytime a new intersection is encountered. It is universal to all
        /// the boolean operations
        /// </summary>
        /// <param name="intersectionLookup">The readonly intersection lookup.</param>
        /// <param name="intersections">The intersections.</param>
        /// <param name="intersectionData">The intersection data.</param>
        /// <param name="currentEdge">The current edge.</param>
        /// <param name="isSubtract">if set to <c>true</c> [switch directions].</param>
        /// <returns>Polygon.</returns>
        /// <exception cref="NotImplementedException"></exception>
        private static List<Vector2> MakePolygonThroughIntersections(List<int>[] intersectionLookup,
            List<IntersectionData> intersections, IntersectionData startingIntersection, PolygonSegment startingEdge, bool isSubtract, bool isUnion)
        {
            var newPath = new List<Vector2>();
            var intersectionData = startingIntersection;
            var currentEdge = startingEdge;
            var forward = true; // as in following the edges in the forward direction (from...to). If false, then traverse backwards
            var currentEdgeIsFromPolygonA = currentEdge == intersectionData.EdgeA;
            do
            {
                intersectionData.Visited = true;
                var intersectionCoordinates = intersectionData.IntersectCoordinates;
                // only add the point to the path if it wasn't added below in the while loop. i.e. it is an intermediate point to the 
                // current polygon edge
                if (!forward || (currentEdgeIsFromPolygonA && (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfA) == 0b0)
                 || (!currentEdgeIsFromPolygonA && (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfB) == 0b0))
                    newPath.Add(intersectionCoordinates);
                currentEdgeIsFromPolygonA = !currentEdgeIsFromPolygonA;
                currentEdge = currentEdgeIsFromPolygonA ? intersectionData.EdgeA : intersectionData.EdgeB;
                if (isSubtract) forward = !forward;
                if (!forward && ((currentEdgeIsFromPolygonA &&
                                  (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfA) != 0b0) ||
                                 (!currentEdgeIsFromPolygonA &&
                                  (intersectionData.Relationship & PolygonSegmentRelationship.AtStartOfB) != 0b0)))
                    currentEdge = currentEdge.FromPoint.EndLine;

                // the following while loop add all the points along the subpath until the next intersection is encountered
                while (!ClosestNextIntersectionOnThisEdge(intersectionLookup, currentEdge, intersections,
                        intersectionCoordinates, forward, isSubtract, isUnion, out intersectionData))
                // when this returns true (a valid intersection is found - even if previously visited), then we break
                // out of the loop. The intersection is identified here, but processed above
                {
                    if (forward)
                    {
                        newPath.Add(currentEdge.ToPoint.Coordinates);
                        currentEdge = currentEdge.ToPoint.StartLine;
                    }
                    else
                    {
                        newPath.Add(currentEdge.FromPoint.Coordinates);
                        currentEdge = currentEdge.FromPoint.EndLine;
                    }
                    intersectionCoordinates = Vector2.Null; // this is set to null because its value is used in ClosestNextIntersectionOnThisEdge
                                                            // when multiple intersections cross the edge. If we got through the first pass then there are no previous intersections on 
                                                            // the edge that concern us. We want that function to report the first one for the edge
                }
            } while (currentEdge != startingEdge && intersectionData != startingIntersection);
            return newPath;
        }

        /// <summary>
        /// This is invoked by the previous function, . It is possible that there are multiple intersections crossing the currentEdge. Based on the
        /// direction (forward?), the next closest one is identified.
        /// </summary>
        /// <param name="intersectionLookup">The intersection lookup.</param>
        /// <param name="currentEdge">The current edge.</param>
        /// <param name="allIntersections">All intersections.</param>
        /// <param name="formerIntersectCoords">The former intersect coords.</param>
        /// <param name="forward">if set to <c>true</c> [forward].</param>
        /// <param name="indexOfIntersection">The index of intersection.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        /// <exception cref="NotImplementedException"></exception>
        private static bool ClosestNextIntersectionOnThisEdge(List<int>[] intersectionLookup, PolygonSegment currentEdge, List<IntersectionData> allIntersections,
        Vector2 formerIntersectCoords, bool forward, bool isSubtract, bool isUnion, out IntersectionData indexOfIntersection)
        {
            var intersectionIndices = intersectionLookup[currentEdge.IndexInList];
            indexOfIntersection = null;
            if (intersectionIndices == null)
                return false;
            var minDistanceToIntersection = double.PositiveInfinity;
            var vector = forward ? currentEdge.Vector : -currentEdge.Vector;
            var datum = !formerIntersectCoords.IsNull() ? formerIntersectCoords :
                forward ? currentEdge.FromPoint.Coordinates : currentEdge.ToPoint.Coordinates;
            foreach (var index in intersectionIndices)
            {
                var thisIntersectData = allIntersections[index];

                // if the two polygons just "glance" off of one another at this intersection, then don't consider this as a valid place to switch
                if (((thisIntersectData.Relationship & PolygonSegmentRelationship.Overlapping) == 0b0)
                // if union and current edge is on the outer polygon, then don't consider this as a valid place to switch
                || (isUnion && ((currentEdge == thisIntersectData.EdgeA && (thisIntersectData.Relationship & PolygonSegmentRelationship.Overlapping) == PolygonSegmentRelationship.AEncompassesB)
                    || (currentEdge == thisIntersectData.EdgeB && (thisIntersectData.Relationship & PolygonSegmentRelationship.Overlapping) == PolygonSegmentRelationship.BEncompassesA)))
                // if intersect and current edge is on the inner polygon, then don't consider this as a valid place to switch
                || (!isSubtract && ((currentEdge == thisIntersectData.EdgeA && (thisIntersectData.Relationship & PolygonSegmentRelationship.Overlapping) == PolygonSegmentRelationship.BEncompassesA)
                    || (currentEdge == thisIntersectData.EdgeB && (thisIntersectData.Relationship & PolygonSegmentRelationship.Overlapping) == PolygonSegmentRelationship.AEncompassesB))))
                {
                    // well, even though the intersection is a valid place to switch, we need to mark that it is visited so that we don't start here next time
                    // GetNextStartingIntersection gets called
                    thisIntersectData.Visited = true;
                    continue;
                }

                var distance = vector.Dot(thisIntersectData.IntersectCoordinates - datum);
                if (distance < 0 || (distance == 0 && !formerIntersectCoords.IsNull())) continue;
                if (minDistanceToIntersection > distance)
                {
                    minDistanceToIntersection = distance;
                    indexOfIntersection = thisIntersectData;
                }
            }
            return indexOfIntersection != null;
        }

        /// <summary>
        /// Makes the intersection lookup table that allows us to quickly find the intersections for a given edge.
        /// </summary>
        /// <param name="numLines">The number lines.</param>
        /// <param name="intersections">The intersections.</param>
        /// <returns>System.Collections.Generic.List&lt;System.Int32&gt;[].</returns>
        private static List<int>[] MakeIntersectionLookupList(int numLines, List<IntersectionData> intersections)
        {
            var result = new List<int>[numLines];
            for (int i = 0; i < intersections.Count; i++)
            {
                var intersection = intersections[i];
                intersection.Visited = false;
                var index = intersection.EdgeA.IndexInList;
                result[index] ??= new List<int>();
                result[index].Add(i);
                index = intersection.EdgeB.IndexInList;
                result[index] ??= new List<int>();
                result[index].Add(i);
            }
            return result;
        }
        #endregion
    }
}