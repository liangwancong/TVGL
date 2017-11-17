﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 02-27-2015
//
// Last Modified By : Matt Campbell
// Last Modified On : 09-21-2017
// ***********************************************************************
// <copyright file="VoxelizedSolid.cs" company="Design Engineering Lab">
//     Copyright ©  2017
// </copyright>
// <summary></summary>
// ***********************************************************************

using StarMathLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TVGL.Voxelization
{
    /// <summary>
    /// Class VoxelizedSolid.
    /// </summary>
    public class VoxelizedSolid
    {
        private const int MaxVoxelsAlongASide = 1048576; //2^20

        #region Properties
        /// <summary>
        /// Gets tessellated solid that this voxelized solid was built from.
        /// </summary>
        public TessellatedSolid tessellatedSolid { get; private set; }

        private VoxelDiscretization voxelDiscretization;

        /// <summary>
        /// Gets the voxel identifier hash set.
        /// </summary>
        /// <value>The voxel identifier hash set.</value>
        public HashSet<long> VoxelIDHashSet { get; private set; }
        /// <summary>
        /// The voxels are stored as a dictionary - accessible by their voxelID.
        /// </summary>
        public Dictionary<long, VoxelBin> VoxelBins { get; private set; }
        private HashSet<long> voxelBinHashSet { get; private set; }
        /// <summary>
        /// Gets the scale to integer space.
        /// </summary>
        /// <value>The scale to int space.</value>
        public double ScaleToIntSpace { get; private set; }
        public double[] Offset { get; private set; }
        public int NumVoxelsTotal { get; private set; }
        public int LongestDimensionIndex { get; private set; }
        public int[] NumVoxels { get; private set; }

        /// <summary>
        /// The voxel side length. It's a square, so all sides are the same length.
        /// </summary>
        public double VoxelSideLength { get; private set; }
        #endregion

        #region Private Fields
        /// <summary>
        /// The x identifier multiplier
        /// </summary>
        long xIDMultiplier;
        /// <summary>
        /// The y identifier multiplier
        /// </summary>
        long yIDMultiplier;

        /// <summary>
        /// The sign identifier multiplier
        /// </summary>
        long signIDMultiplier;


        /// <summary>
        /// The transformed array of vertex coordinates. These correspond in position to the vertices
        /// in the linked tessellated solid.
        /// </summary>
        private readonly double[][] transformedCoordinates;
        #endregion

        #region Constructor and "makeVoxels..." functions

        /// <summary>
        /// Initializes a new instance of the <see cref="VoxelizedSolid"/> class.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="numberOfVoxelsAlongMaxDirection">The number of voxels along maximum direction.</param>
        /// <param name="onlyDefineBoundary">if set to <c>true</c> [only define boundary].</param>
        /// <param name="linkToTessellatedSolid">if set to <c>true</c> [link to tessellated solid].</param>
        public VoxelizedSolid(TessellatedSolid ts, VoxelDiscretization voxelDiscretization)
        {
            tessellatedSolid = ts;
            this.voxelDiscretization = voxelDiscretization;
            SetUpIndexingParameters();
            VoxelBins = new Dictionary<long, VoxelBin>(); //todo:approximate capacity based on tessellated volume
            VoxelIDHashSet = new HashSet<long>();
            transformedCoordinates = new double[tessellatedSolid.NumberOfVertices][];
            makeVoxelsForEachVertex();
            makeVoxelsInInterior();
            NumVoxelsTotal = VoxelIDHashSet.Count;
        }

        public VoxelizedSolid(TessellatedSolid ts, Dictionary<long, VoxelBin> voxels, HashSet<long> voxelIDHashSet,
            int numberOfVoxelsAlongMaxDirection = 100)
        {
            tessellatedSolid = ts;
            SetUpIndexingParameters(numberOfVoxelsAlongMaxDirection);
            Voxels = voxels;
            VoxelIDHashSet = voxelIDHashSet;
        }

        /// <summary>
        /// Makes the voxels in interior.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        private void makeVoxelsInInterior()
        {
            var sweepDim = LongestDimensionIndex;
            var uDim = (sweepDim != 0) ? 0 : 1;
            var vDim = (sweepDim == 2) ? 1 : 2;
            var ids = Voxels.Values.Select(vx => IndicesToVoxelID(0, vx.Index[uDim], vx.Index[vDim])).Distinct()
                .AsParallel();
            var dict = ids.ToDictionary(id => id, id => new Tuple<SortedSet<VoxelBin>, SortedSet<VoxelBin>>(
                  new SortedSet<VoxelBin>(new SortByVoxelIndex(sweepDim)),
                  new SortedSet<VoxelBin>(new SortByVoxelIndex(sweepDim))));
            Parallel.ForEach(Voxels.Values, voxel =>
                         {
                             var id = IndicesToVoxelID(0, voxel.Index[uDim], voxel.Index[vDim]);
                             var sortedSets = dict[id];
                             var negativeFaceVoxels = sortedSets.Item1;
                             var positiveFaceVoxels = sortedSets.Item2;
                             var faces = voxel.TessellationElements.Where(te => te is PolygonalFace).ToList();
                             if (faces.Any(f => f.Normal[sweepDim] >= 0))
                                 lock (positiveFaceVoxels) positiveFaceVoxels.Add(voxel);
                             if (faces.Any(f => f.Normal[sweepDim] <= 0))
                                 lock (negativeFaceVoxels) negativeFaceVoxels.Add(voxel);
                         });
            var interiorVoxels = dict.Values
                .Where(entry => entry.Item1.Any() && entry.Item2.Any())
                .SelectMany(entry => MakeInteriorVoxelsAlongLine(entry.Item1, entry.Item2, sweepDim))
                .AsParallel();
            foreach (var interiorVoxel in interiorVoxels)
            {
                if (VoxelIDHashSet.Contains(interiorVoxel.ID)) continue; // why would this happen though?
                VoxelIDHashSet.Add(interiorVoxel.ID);
                Voxels.Add(interiorVoxel.ID, interiorVoxel);
            }
        }

        private List<VoxelBin> MakeInteriorVoxelsAlongLine(SortedSet<VoxelBin> sortedNegatives,
            SortedSet<VoxelBin> sortedPositives, int sweepDim)
        {
            var index = (int[])sortedNegatives.First().Index.Clone();
            var newVoxels = new List<VoxelBin>();
            var negativeQueue = new Queue<VoxelBin>(sortedNegatives);
            var positiveQueue = new Queue<VoxelBin>(sortedPositives);
            while (negativeQueue.Any() && positiveQueue.Any())
            {
                var startIndex = negativeQueue.Dequeue().Index[sweepDim];
                if (negativeQueue.Any() && negativeQueue.Peek().Index[sweepDim] - startIndex <= 1) continue;
                int endIndex = Int32.MinValue;
                while (endIndex < startIndex && positiveQueue.Any())
                    endIndex = positiveQueue.Dequeue().Index[sweepDim];
                for (int i = startIndex + 1; i < endIndex; i++)
                {
                    index[sweepDim] = i;
                    var voxelID = IndicesToVoxelID(index);
                    newVoxels.Add(new VoxelBin(index, voxelID, VoxelSideLength, VoxelRoleTypes.Full,
                        null));
                }
            }
            return newVoxels;
        }

        /// <summary>
        /// Makes the voxels for faces and edges. This is a complicated function! Originally, the scheme used in 
        /// OpenVDB was employed where a voxel group (3x3x3) is initiated at one of the vertices of a face so that 
        /// it is guaranteed to intersect with the face. Then it performs a depth first search outward and makes 
        /// many calls to the closest point on the triangle function (see under proximity). This was implemented
        /// in https://github.com/DesignEngrLab/TVGL/commit/b366f25fa8be05a6d75e0272ff1efb15660880d9 but it was
        /// shown to be 10 times slower than the method devised here. This method simply follows the edges, 
        /// which are obviously straight-lines in space to see what voxels it passes through. For the faces, 
        /// these are done in a sweep across the face progressively with the creation of the edge voxels.
        /// Details of this method are to be presented
        /// on the wiki page: https://github.com/DesignEngrLab/TVGL/wiki/Creating-Voxels-from-Tessellation
        /// </summary>
        /// <param name="linkToTessellatedSolid">if set to <c>true</c> [link to tessellated solid].</param>
        private void makeVoxelsForFacesAndEdges(bool linkToTessellatedSolid)
        {
            foreach (var face in tessellatedSolid.Faces) //loop over the faces
            {
                if (simpleCase(face, linkToTessellatedSolid)) continue;
                Vertex startVertex, leftVertex, rightVertex;
                Edge leftEdge, rightEdge;
                int uDim, vDim, sweepDim;
                double maxSweepValue;
                setUpFaceSweepDetails(face, out startVertex, out leftVertex, out rightVertex, out leftEdge,
                    out rightEdge, out uDim, out vDim,
                    out sweepDim, out maxSweepValue);
                var leftStartPoint = (double[])transformedCoordinates[startVertex.IndexInList].Clone();
                var rightStartPoint = (double[])leftStartPoint.Clone();
                var sweepValue = (int)(atIntegerValue(leftStartPoint[sweepDim])
                    ? leftStartPoint[sweepDim] + 1
                    : Math.Ceiling(leftStartPoint[sweepDim]));
                var leftEndPoint = transformedCoordinates[leftVertex.IndexInList];
                var rightEndPoint = transformedCoordinates[rightVertex.IndexInList];

                // the following 2 booleans determine if the edge needs to be voxelized as it may have
                // been done in a previous visited face
                var voxelizeLeft = leftEdge.Voxels == null || !leftEdge.Voxels.Any();
                var voxelizeRight = rightEdge.Voxels == null || !rightEdge.Voxels.Any();
                while (sweepValue <= maxSweepValue) // this is the sweep along the face
                {
                    // first fill in any voxels for face between the start points. Why do this here?! These 2 lines of code  were
                    // the last added. There are cases (not in the first loop, mind you) where it is necessary. Note this happens again
                    // at the bottom of the while-loop for the same sweep value, but for the next startpoints
                    makeVoxelsAlongLineInPlane(leftStartPoint[uDim], leftStartPoint[vDim], rightStartPoint[uDim],
                        rightStartPoint[vDim], sweepValue, uDim, vDim,
                        sweepDim, face.Normal[vDim] >= 0, linkToTessellatedSolid ? face : null);
                    makeVoxelsAlongLineInPlane(leftStartPoint[vDim], leftStartPoint[uDim], rightStartPoint[vDim],
                        rightStartPoint[uDim], sweepValue, vDim, uDim,
                        sweepDim, face.Normal[uDim] >= 0, linkToTessellatedSolid ? face : null);
                    // now two big calls for the edges: one for the left edge and one for the right. by the way, the naming of left and right are 
                    // completely arbitrary here. They are not indicative of any real position.
                    voxelizeLeft = makeVoxelsForEdgeWithinSweep(ref leftStartPoint, ref leftEndPoint, sweepValue,
                        sweepDim, uDim, vDim, linkToTessellatedSolid,
                        voxelizeLeft, leftEdge, face, rightEndPoint, startVertex);
                    voxelizeRight = makeVoxelsForEdgeWithinSweep(ref rightStartPoint, ref rightEndPoint, sweepValue,
                        sweepDim, uDim, vDim, linkToTessellatedSolid,
                        voxelizeRight, rightEdge, face, leftEndPoint, startVertex);
                    // now that the end points of the edges have moved, fill in more of the faces.
                    makeVoxelsAlongLineInPlane(leftStartPoint[uDim], leftStartPoint[vDim], rightStartPoint[uDim],
                        rightStartPoint[vDim], sweepValue, uDim, vDim,
                        sweepDim, face.Normal[vDim] >= 0, linkToTessellatedSolid ? face : null);
                    makeVoxelsAlongLineInPlane(leftStartPoint[vDim], leftStartPoint[uDim], rightStartPoint[vDim],
                        rightStartPoint[uDim], sweepValue, vDim, uDim,
                        sweepDim, face.Normal[uDim] >= 0, linkToTessellatedSolid ? face : null);
                    sweepValue++; //increment sweepValue and repeat!
                }
            }
        }

        /// <summary>
        /// Sets up face sweep details such as which dimension to sweep over and assign start and end points
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="startVertex">The start vertex.</param>
        /// <param name="leftVertex">The left vertex.</param>
        /// <param name="rightVertex">The right vertex.</param>
        /// <param name="leftEdge">The left edge.</param>
        /// <param name="rightEdge">The right edge.</param>
        /// <param name="uDim">The u dim.</param>
        /// <param name="vDim">The v dim.</param>
        /// <param name="maxSweepValue">The maximum sweep value.</param>
        private void setUpFaceSweepDetails(PolygonalFace face, out Vertex startVertex, out Vertex leftVertex, out Vertex rightVertex,
            out Edge leftEdge, out Edge rightEdge, out int uDim, out int vDim, out int sweepDim, out double maxSweepValue)
        {
            var xLength = Math.Max(Math.Max(Math.Abs(face.A.X - face.B.X), Math.Abs(face.B.X - face.C.X)),
                      Math.Abs(face.C.X - face.A.X));
            var yLength = Math.Max(Math.Max(Math.Abs(face.A.Y - face.B.Y), Math.Abs(face.B.Y - face.C.Y)),
                Math.Abs(face.C.Y - face.A.Y));
            var zLength = Math.Max(Math.Max(Math.Abs(face.A.Z - face.B.Z), Math.Abs(face.B.Z - face.C.Z)),
                Math.Abs(face.C.Z - face.A.Z));
            sweepDim = 0;
            uDim = 1;
            vDim = 2;
            if (yLength > xLength)
            {
                sweepDim = 1;
                uDim = 2;
                vDim = 0;
            }
            if (zLength > yLength && zLength > xLength)
            {
                sweepDim = 2;
                uDim = 0;
                vDim = 1;
            }
            startVertex = face.A;
            leftVertex = face.B;
            leftEdge = face.Edges[0];
            rightVertex = face.C;
            rightEdge = face.Edges[2];
            maxSweepValue = (int)Math.Ceiling(Math.Max(transformedCoordinates[face.B.IndexInList][sweepDim], transformedCoordinates[face.C.IndexInList][sweepDim]));
            if (face.B.Position[sweepDim] < face.A.Position[sweepDim])
            {
                startVertex = face.B;
                leftVertex = face.C;
                leftEdge = face.Edges[1];
                rightVertex = face.A;
                rightEdge = face.Edges[0];
                maxSweepValue = (int)Math.Ceiling(Math.Max(transformedCoordinates[face.C.IndexInList][sweepDim], transformedCoordinates[face.A.IndexInList][sweepDim]));
            }
            if (face.C.Position[sweepDim] < face.B.Position[sweepDim] &&
                face.C.Position[sweepDim] < face.A.Position[sweepDim])
            {
                startVertex = face.C;
                leftVertex = face.A;
                leftEdge = face.Edges[2];
                rightVertex = face.B;
                rightEdge = face.Edges[1];
                maxSweepValue = (int)Math.Ceiling(Math.Max(transformedCoordinates[face.A.IndexInList][sweepDim], transformedCoordinates[face.B.IndexInList][sweepDim]));
            }
        }

        /// <summary>
        /// Makes the voxels for edge within sweep.
        /// </summary>
        /// <param name="startPoint">The start point.</param>
        /// <param name="endPoint">The end point.</param>
        /// <param name="sweepValue">The sweep value.</param>
        /// <param name="sweepDim">The sweep dim.</param>
        /// <param name="uDim">The u dim.</param>
        /// <param name="vDim">The v dim.</param>
        /// <param name="linkToTessellatedSolid">if set to <c>true</c> [link to tessellated solid].</param>
        /// <param name="voxelize">if set to <c>true</c> [voxelize].</param>
        /// <param name="edge">The edge.</param>
        /// <param name="face">The face.</param>
        /// <param name="nextEndPoint">The next end point.</param>
        /// <param name="startVertex">The start vertex.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool makeVoxelsForEdgeWithinSweep(ref double[] startPoint, ref double[] endPoint, int sweepValue, int sweepDim, int uDim, int vDim,
            bool linkToTessellatedSolid, bool voxelize, Edge edge, PolygonalFace face, double[] nextEndPoint, Vertex startVertex)
        {
            double u, v;
            var reachedOtherVertex = findWhereLineCrossesPlane(startPoint, endPoint, sweepDim,
                 sweepValue, out u, out v);
            if (voxelize)
            {
                makeVoxelsAlongLineInPlane(startPoint[uDim], startPoint[vDim], u, v,
                    sweepValue, uDim, vDim, sweepDim, face.Normal[vDim] >= 0, linkToTessellatedSolid ? edge : null);
                makeVoxelsAlongLineInPlane(startPoint[vDim], startPoint[uDim], v, u,
                    sweepValue, vDim, uDim, sweepDim, face.Normal[uDim] >= 0, linkToTessellatedSolid ? edge : null);
            }
            if (reachedOtherVertex)
            {
                startPoint = (double[])endPoint.Clone();
                endPoint = nextEndPoint;
                edge = face.OtherEdge(startVertex);
                voxelize = edge.Voxels == null || !edge.Voxels.Any();

                findWhereLineCrossesPlane(startPoint, endPoint, sweepDim,
                    sweepValue, out u, out v);
                if (voxelize)
                {
                    makeVoxelsAlongLineInPlane(startPoint[uDim], startPoint[vDim], u, v,
                        sweepValue, uDim, vDim, sweepDim, face.Normal[vDim] >= 0, linkToTessellatedSolid ? edge : null);
                    makeVoxelsAlongLineInPlane(startPoint[vDim], startPoint[uDim], v, u,
                        sweepValue, vDim, uDim, sweepDim, face.Normal[uDim] >= 0, linkToTessellatedSolid ? edge : null);
                }
            }
            startPoint[uDim] = u;
            startPoint[vDim] = v;
            startPoint[sweepDim] = sweepValue;
            return voxelize;
        }

        /// <summary>
        /// Makes the voxels for each vertex and creates the vertices array for lookup in the transformed space.
        /// </summary>
        /// <param name="linkToTessellatedSolid">if set to <c>true</c> [link to tessellated solid].</param>
        private void makeVoxelsForEachVertex()
        {
            Parallel.For(0, tessellatedSolid.NumberOfVertices, i =>
            {
                var vertex = tessellatedSolid.Vertices[i];
                var coordinates = vertex.Position.multiply(ScaleToIntSpace).add(Offset);
                transformedCoordinates[i] = coordinates;
                int x, y, z;
                if (coordinates.Any(atIntegerValue))
                {
                    var edgeVectors = vertex.Edges.Select(e => e.To == vertex ? e.Vector : e.Vector.multiply(-1));
                    if (edgeVectors.All(ev => ev[0] >= 0))
                        x = (int)(coordinates[0] - 1);
                    else x = (int)Math.Floor(coordinates[0]);
                    if (edgeVectors.All(ev => ev[1] >= 0))
                        y = (int)(coordinates[1] - 1);
                    else y = (int)Math.Floor(coordinates[1]);
                    if (edgeVectors.All(ev => ev[2] >= 0))
                        z = (int)(coordinates[2] - 1);
                    else z = (int)Math.Floor(coordinates[2]);
                }
                else
                {
                    //Gets the integer coordinates, rounded down for the point.
                    x = (int)Math.Floor(coordinates[0]);
                    y = (int)Math.Floor(coordinates[1]);
                    z = (int)Math.Floor(coordinates[2]);
                }
                MakeAndStoreVoxelBin(x, y, z, vertex, 0);
            });
        }

        private void MakeAndStoreVoxelBin(int x, int y, int z, TessellationBaseClass tsObject, int level)
        {
            var voxelID = MakeVoxelID(x, y, z, level, VoxelRoleTypes.Partial);
            VoxelBin voxel;
            if (tsObject == null)
            {
                if (!voxelBinHashSet.Contains(voxelID))
                {
                    VoxelIDHashSet.Add(voxelID);
                    voxel = new VoxelBin(voxelID, VoxelRoleTypes.Partial);
                    VoxelBins.Add(voxelID, voxel);
                }
                return;
            }
            // a bit more complicated if tsObject is provided
            if (VoxelIDHashSet.Contains(voxelID))
            {
                voxel = VoxelBinss[voxelID];
                if (voxel.TessellationElements.Contains(tsObject))
                    return;
                voxel.TessellationElements.Add(tsObject);
            }
            else
            {
                VoxelIDHashSet.Add(voxelID);
                voxel = new VoxelBin(voxelID, VoxelRoleTypes.Partial, tsObject);
                VoxelBins.Add(voxelID, voxel);
            }
            tsObject.AddVoxel(voxel);
        }

        private long MakeVoxelID(int x, int y, int z,int level, VoxelRoleTypes level0, VoxelRoleTypes level1 = VoxelRoleTypes.Full,
            VoxelRoleTypes level2 = VoxelRoleTypes.Full, VoxelRoleTypes level3 = VoxelRoleTypes.Full, VoxelRoleTypes level4=VoxelRoleTypes.Full)
        {
            var shift = 4 * (4 - level);
            x = x >> shift;
            y = y >> shift;
            z = z >> shift;
            x = x << (44 + shift); //can't you combine with the above? no. The shift is doing both division
            y = y << (24 + shift); // and remainder. What I mean to say is that e.g. 7>>2<<2 = 4
            z = z << (4 + shift);
            //    x0   x1    x2   x3    x4   y0   y1    y2   y3    y4    z0   z1    z2   z3    z4  flags
            // ||----|----||----|----||----|----||----|----||----|----||----|----||----|----||----|----|
            // 64   60    56    52   48    44   40    36   32    28   24    20   16    12    8    4
            
        }

        /// <summary>
        /// If it is a simple case, just solve it and return true.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="linkToTessellatedSolid">if set to <c>true</c> [link to tessellated solid].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool simpleCase(PolygonalFace face, bool linkToTessellatedSolid)
        {
            if (!linkToTessellatedSolid)
            {
                var aIndices = transformedCoordinates[face.A.IndexInList].Select(x => (int)Math.Floor(x)).ToArray();
                var aID = IndicesToVoxelID(aIndices);
                var bIndices = transformedCoordinates[face.B.IndexInList].Select(x => (int)Math.Floor(x)).ToArray();
                var bID = IndicesToVoxelID(bIndices);
                var cIndices = transformedCoordinates[face.C.IndexInList].Select(x => (int)Math.Floor(x)).ToArray();
                var cID = IndicesToVoxelID(cIndices);
                if (aID == bID && aID == cID) return true;
                if (aIndices[0] == bIndices[0] && aIndices[0] == cIndices[0] &&
                    aIndices[1] == bIndices[1] && aIndices[1] == cIndices[1])
                {
                    makeVoxelsForFaceInCardinalLine(face, 2, false);
                    return true;
                }
                if (aIndices[0] == bIndices[0] && aIndices[0] == cIndices[0] &&
                    aIndices[2] == bIndices[2] && aIndices[2] == cIndices[2])
                {
                    makeVoxelsForFaceInCardinalLine(face, 1, false);
                    return true;
                }
                if (aIndices[2] == bIndices[2] && aIndices[2] == cIndices[2] &&
                    aIndices[1] == bIndices[1] && aIndices[1] == cIndices[1])
                {
                    makeVoxelsForFaceInCardinalLine(face, 0, false);
                    return true;
                }
                return false;
            }
            // The first simple case is that all vertices are within the same voxel. 
            if (face.A.Voxel == face.B.Voxel && face.A.Voxel == face.C.Voxel)
            {
                var voxel = face.A.Voxel;
                face.AddVoxel(voxel);
                foreach (var edge in face.Edges)
                    edge.AddVoxel(voxel);
                return true;
            }
            // the second, third, and fourth simple cases are if the triangle
            // fits within a line of voxels.
            // this condition checks that all voxels have same x & y values (hence aligned in z-direction)
            if (face.A.Voxel.Index[0] == face.B.Voxel.Index[0] &&
                     face.A.Voxel.Index[0] == face.C.Voxel.Index[0] &&
                     face.A.Voxel.Index[1] == face.B.Voxel.Index[1] &&
                     face.A.Voxel.Index[1] == face.C.Voxel.Index[1])
            {
                makeVoxelsForFaceInCardinalLine(face, 2, true);
                return true;
            }
            // this condition checks that all voxels have same x & z values (hence aligned in y-direction)
            if (face.A.Voxel.Index[0] == face.B.Voxel.Index[0] &&
                     face.A.Voxel.Index[0] == face.C.Voxel.Index[0] &&
                     face.A.Voxel.Index[2] == face.B.Voxel.Index[2] &&
                     face.A.Voxel.Index[2] == face.C.Voxel.Index[2])
            {
                makeVoxelsForFaceInCardinalLine(face, 1, true);
                return true;
            }
            // this condition checks that all voxels have same y & z values (hence aligned in x-direction)
            if (face.A.Voxel.Index[2] == face.B.Voxel.Index[2] &&
                     face.A.Voxel.Index[2] == face.C.Voxel.Index[2] &&
                     face.A.Voxel.Index[1] == face.B.Voxel.Index[1] &&
                     face.A.Voxel.Index[1] == face.C.Voxel.Index[1])
            {
                makeVoxelsForFaceInCardinalLine(face, 0, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Makes the voxels for face in cardinal line. This is only called by the preceding function, simpleCase.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="dim">The dim.</param>
        /// <param name="linkToTessellatedSolid"></param>
        private void makeVoxelsForFaceInCardinalLine(PolygonalFace face, int dim, bool linkToTessellatedSolid)
        {
            var minIndex = Math.Min(face.A.Voxel.Index[dim],     // stores the index of the voxel along dim that is the smallest
                    Math.Min(face.B.Voxel.Index[dim], face.C.Voxel.Index[dim]));
            var maxIndex = Math.Max(face.A.Voxel.Index[dim],     // stores the index of the voxel along dim that is the largest
                Math.Max(face.B.Voxel.Index[dim], face.C.Voxel.Index[dim]));
            var voxelIndex = (int[])face.A.Voxel.Index.Clone();
            for (int i = minIndex; i <= maxIndex; i++)
            {   // set up voxels for the face
                voxelIndex[dim] = i;
                storeVoxel(voxelIndex, linkToTessellatedSolid ? face : null);
            }
            if (!linkToTessellatedSolid) return;
            foreach (var faceEdge in face.Edges)
            {   // cycle over the edges to link them to the voxels, may need to make new voxels
                var lowVIndex = Math.Min(faceEdge.From.Voxel.Index[dim], faceEdge.To.Voxel.Index[dim]);
                var highVIndex = Math.Max(faceEdge.From.Voxel.Index[dim], faceEdge.To.Voxel.Index[dim]);
                for (int i = lowVIndex; i <= highVIndex; i++)
                {
                    voxelIndex[dim] = i;
                    storeVoxel(voxelIndex, linkToTessellatedSolid ? faceEdge : null);
                }
            }
        }

        /// <summary>
        /// Makes the voxels along line in plane. This is a tricky little function that took a lot of debugging.
        /// At this point, we are looking at a simple 2D problem. The startU, startV, endU, and endV are the
        /// local/barycentric coordinates and correspond to actual x, y, and z via the uDim, and yDim. The
        /// out-of-plane dimension is the sweepDim and its value (sweepValue) are provided as "read only" parameters.
        /// - meaning they are given as constants that are used only to define the voxels. This method works by 
        /// finding where the v values are that cross the u integer lines. There are some subtle
        /// issues working in the negative direction (indicated by uRange and vRange) as you need to create the voxel
        /// at the next lower integer cell - not the one specified by the line crossing.
        /// </summary>
        /// <param name="startU">The start x.</param>
        /// <param name="startV">The start y.</param>
        /// <param name="endU">The end x.</param>
        /// <param name="endV">The end y.</param>
        /// <param name="sweepValue">The value sweep dimension.</param>
        /// <param name="uDim">The index of the u dimension.</param>
        /// <param name="vDim">The index of the v dimension.</param>
        /// <param name="sweepDim">The index of the sweep dimension.</param>
        /// <param name="insideIsLowerV">if set to <c>true</c> [then the inside of the part is on the lower-side of the v-value].</param>
        /// <param name="tsObject">The ts object.</param>
        private void makeVoxelsAlongLineInPlane(double startU, double startV, double endU, double endV, int sweepValue,
            int uDim, int vDim, int sweepDim, bool insideIsLowerV, TessellationBaseClass tsObject)
        {
            var uRange = endU - startU;
            if (uRange.IsNegligible()) return;
            var increment = Math.Sign(uRange);
            var u = atIntegerValue(startU) && uRange <= 0 ? (int)(startU - 1) : (int)Math.Floor(startU);
            // if you are starting an integer value for u, but you're going in the negative directive, then decrement u to the next lower value
            // this is because voxels are defined by their lowest index values.
            var vRange = endV - startV;
            var v = atIntegerValue(startV) && (vRange < 0 || (vRange == 0 && insideIsLowerV)) ? (int)(startV - 1) : (int)Math.Floor(startV);
            // likewise for v. if you are starting an integer value and you're going in the negative directive, OR if you're at an integer
            // value and happen to be vertical line then decrement v to the next lower value
            var ijk = new int[3];
            ijk[sweepDim] = sweepValue - 1;
            while (increment * u < increment * endU)
            {
                ijk[uDim] = u;
                ijk[vDim] = v;
                storeVoxel(ijk, tsObject);
                // now move to the next increment, of course, you may not use it if the while condition is not met
                u += increment;
                var vDouble = vRange * (u - startU) / uRange + startV;
                v = atIntegerValue(vDouble) && (vRange < 0 || (vRange == 0 && insideIsLowerV)) ? (int)(vDouble - 1) : (int)Math.Floor(vDouble);
            }
        }

        /// <summary>
        /// Stores the voxel and links them to the tessellated object (vertex,
        /// edge, or face) if it is provided.
        /// </summary>
        /// <param name="ijk">The ijk.</param>
        /// <param name="tsObject">The ts object.</param>
        private void storeVoxel(int[] ijk, TessellationBaseClass tsObject)
        {
            var voxelID = IndicesToVoxelID(ijk);
            VoxelBin voxel;
            if (tsObject == null)
            {
                if (!VoxelIDHashSet.Contains(voxelID))
                {
                    VoxelIDHashSet.Add(voxelID);
                    voxel = new VoxelBin(ijk, voxelID, VoxelSideLength, VoxelRoleTypes.Partial);
                    Voxels.Add(voxelID, voxel);
                }
                return;
            }
            // a bit more complicated if tsObject is provided
            if (VoxelIDHashSet.Contains(voxelID))
            {
                voxel = Voxels[voxelID];
                if (voxel.TessellationElements.Contains(tsObject))
                    return;
                voxel.TessellationElements.Add(tsObject);
            }
            else
            {
                VoxelIDHashSet.Add(voxelID);
                voxel = new VoxelBin(ijk, voxelID, VoxelSideLength, VoxelRoleTypes.Partial, tsObject);
                Voxels.Add(voxelID, voxel);
            }
            tsObject.AddVoxel(voxel);
        }

        /// <summary>
        /// Finds the where line that is the edge length crosses sweep plane. It may be that the edge terminates
        /// before the plane. If that is the case, then this function returns true to inform the big loop above.
        /// </summary>
        /// <param name="startPoint">The start point.</param>
        /// <param name="endPoint">The end point.</param>
        /// <param name="sweepDim">The sweep dim.</param>
        /// <param name="valueSweepDim">The value sweep dim.</param>
        /// <param name="valueD1">The value d1.</param>
        /// <param name="valueD2">The value d2.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool findWhereLineCrossesPlane(double[] startPoint, double[] endPoint, int sweepDim, double valueSweepDim, out double valueD1, out double valueD2)
        {
            if (endPoint[sweepDim] <= valueSweepDim)
            {
                valueD1 = endPoint[(sweepDim + 1) % 3];
                valueD2 = endPoint[(sweepDim + 2) % 3];
                return true;
            }
            var fraction = (valueSweepDim - startPoint[sweepDim]) / (endPoint[sweepDim] - startPoint[sweepDim]);
            var dim = (sweepDim + 1) % 3;
            valueD1 = fraction * (endPoint[dim] - startPoint[dim]) + startPoint[dim];
            dim = (dim + 1) % 3;
            valueD2 = fraction * (endPoint[dim] - startPoint[dim]) + startPoint[dim];
            return false;
        }
        #endregion

        #region Indexing Functions
        /// <summary>
        /// Sets up indexing parameters.
        /// </summary>
        /// <exception cref="System.Exception">Int64 will not work for a voxel space this large, using the current index setup</exception>
        private void SetUpIndexingParameters()
        {
            var dimensions = new double[3];
            for (int i = 0; i < 3; i++)
                dimensions[i] = tessellatedSolid.Bounds[1][i] - tessellatedSolid.Bounds[0][i];
            var maxDim = dimensions.Max();
            LongestDimensionIndex = dimensions.FindIndex(d => d == maxDim);
            VoxelSideLength = maxDim / MaxVoxelsAlongASide;
            ScaleToIntSpace = 1.0 / VoxelSideLength;
            Offset = tessellatedSolid.Bounds[0].multiply(ScaleToIntSpace);
        }

        /// <summary>
        /// Indiceses to voxel identifier.
        /// </summary>
        /// <param name="ijk">The ijk.</param>
        /// <returns>System.Int64.</returns>
        public long IndicesToVoxelID(int[] ijk)
        {
            return IndicesToVoxelID(ijk[0], ijk[1], ijk[2]);
        }

        /// <summary>
        /// Indiceses to voxel identifier.
        /// </summary>
        /// <param name="i">The i.</param>
        /// <param name="j">The j.</param>
        /// <param name="k">The k.</param>
        /// <returns>
        /// System.Int64.
        /// </returns>
        public long IndicesToVoxelID(int i, int j, int k)
        {
            //To get a unique integer value for each voxel based on its index, 
            //multiply x by the (magnitude)^2, add y*(magnitude), and then add z to get a unique value.
            //Example: for a max magnitude of 1000, with x = -3, y = 345, z = -12
            //3000000 + 345000 + 12 = 3345012 => 3|345|012
            //In addition, we want to capture sign in one digit. So add (magnitude)^3*(negInt) where
            //-X = 1, -Y = 3, -Z = 5;
            //Example 0 = (+X+Y+Z); 1 = (-X+Y+Z); 3 = (+X-Y+Z);  5 = (+X+Y-Z); 
            // 4 = (-X-Y+Z); 6 = (-X+Y-Z);  8 = (+X-Y-Z);  9 = (-X-Y-Z)
            var signValue = 0;
            if (Math.Sign(i) < 0) signValue += 1;
            if (Math.Sign(j) < 0) signValue += 3;
            if (Math.Sign(k) < 0) signValue += 5;
            return signIDMultiplier * signValue + Math.Abs(i) * xIDMultiplier + Math.Abs(j) * yIDMultiplier
                + Math.Abs(k);
        }

        /// <summary>
        /// Voxels the identifier to indices.
        /// </summary>
        /// <param name="voxelID">The voxel identifier.</param>
        /// <returns>System.Int32[].</returns>
        public int[] VoxelIDToIndices(long voxelID)
        {
            var z = (int)(voxelID % yIDMultiplier);
            //uniqueCoordIndex -= z;
            var y = (int)((voxelID % xIDMultiplier) / yIDMultiplier);
            //uniqueCoordIndex -= y*ym;
            var x = (int)((voxelID % signIDMultiplier) / xIDMultiplier);
            //uniqueCoordIndex -= x*xm;
            var s = (int)(voxelID / signIDMultiplier);

            //In addition, we want to capture sign in one digit. So add (magnitude)^3*(negInt) where
            //-X = 1, -Y = 3, -Z = 5;
            switch (s)
            {
                case 0: //(+X+Y+Z)
                    break;
                case 1: //(-X+Y+Z)
                    x = -x;
                    break;
                case 3: //(+X-Y+Z)
                    y = -y;
                    break;
                case 5: //(+X+Y-Z)
                    z = -z;
                    break;
                case 4: //(-X-Y+Z)
                    x = -x;
                    y = -y;
                    break;
                case 6: //(-X+Y-Z)
                    x = -x;
                    z = -z;
                    break;
                case 8: //(+X-Y-Z)
                    y = -y;
                    z = -z;
                    break;
                case 9: //(-X-Y-Z)
                    x = -x;
                    y = -y;
                    z = -z;
                    break;
            }
            return new[] { x, y, z };
        }
        #endregion

        /// <summary>
        /// Is the double currently at an integer value?
        /// </summary>
        /// <param name="d">The d.</param>
        /// <returns></returns>
        private static bool atIntegerValue(double d)
        {
            return Math.Ceiling(d) == d;
        }
    }

    /// <summary>
    /// The discretization type for the voxelized solid. 
    /// </summary>
    public enum VoxelDiscretization
    {
        /// <summary>
        /// The coarse discretization is up to 4096 voxels on a side.
        /// </summary>
        Coarse,
        /// <summary>
        /// The fine discretization is up to 2^20 (~1million) voxels on a side.
        /// </summary>
        Fine
    };
}