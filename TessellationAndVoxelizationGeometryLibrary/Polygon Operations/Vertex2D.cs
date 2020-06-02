﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : Design Engineering Lab
// Created          : 04-18-2016
//
// Last Modified By : Design Engineering Lab
// Last Modified On : 05-26-2016
// ***********************************************************************
// <copyright file="SpecialClasses.cs" company="Design Engineering Lab">
//     Copyright ©  2014
// </copyright>
// <summary></summary>
// ***********************************************************************

using System.Collections.Generic;
using TVGL.Numerics;


namespace TVGL.TwoDimensional
{
    /// <summary>
    ///     Vertex2D class used in Triangulate Polygon
    ///     Inherits position from point class
    /// </summary>
    public class Vertex2D
    {
        #region Properties

        /// <summary>
        ///     Gets the loop ID that this node belongs to.
        /// </summary>
        /// <value>The loop identifier.</value>
        internal int LoopID { get; set; }

        /// <summary>
        ///     Gets or sets the x.
        /// </summary>
        /// <value>The x.</value>
        public double X => Coordinates.X;

        /// <summary>
        ///     Gets or sets the y.
        /// </summary>
        /// <value>The y.</value>
        public double Y => Coordinates.Y;


        /// <summary>
        ///     Gets the line that starts at this node.
        /// </summary>
        /// <value>The start line.</value>
        internal PolygonSegment StartLine { get; set; }

        /// <summary>
        ///     Gets the line that ends at this node.
        /// </summary>
        /// <value>The end line.</value>
        internal PolygonSegment EndLine { get; set; }

        /// <summary>
        ///     Gets the type of  node.
        /// </summary>
        /// <value>The type.</value>
        internal NodeType Type { get; set; }

        /// <summary>
        ///     Gets the base class, Point of this node.
        /// </summary>
        /// <value>The point.</value>
        internal Vector2 Coordinates { get; private set; }
        internal int IndexInList { get; set; }
        /// <summary>
        ///     Gets the base class, Point of this node.
        /// </summary>
        /// <value><c>true</c> if this instance is right chain; otherwise, <c>false</c>.</value>
        internal bool IsRightChain { get; set; }

        /// <summary>
        ///     Gets the base class, Point of this node.
        /// </summary>
        /// <value><c>true</c> if this instance is left chain; otherwise, <c>false</c>.</value>
        internal bool IsLeftChain { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        ///     Initializes a new instance of the <see cref="Node" /> class.
        /// </summary>
        /// <param name="currentPoint">The current point.</param>
        /// <param name="loopID">The loop identifier.</param>
        internal Vertex2D(Vector2 currentPoint, int referenceID, int loopID)
        {
            LoopID = loopID;
            Coordinates = currentPoint;
            IndexInList = referenceID;
        }
        internal Vertex2D Copy()
        {
            return new Vertex2D
            {
                Coordinates = this.Coordinates,
                IndexInList = this.IndexInList,
                IsLeftChain = this.IsLeftChain,
                IsRightChain = this.IsRightChain,
                LoopID = this.LoopID,
                Type = this.Type
            };
        }
        // the following private argument-less constructor is only used in the copy function
        private Vertex2D() { }
        #endregion
    }
}