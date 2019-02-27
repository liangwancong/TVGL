﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace TVGL.CUDA
{
    /// <summary>
    /// Class VoxelizedSolidCUDA.
    /// </summary>
    public partial class VoxelizedSolidCUDA
    {
        public int[][] GetNeighbors(int i, int j, int k)
        {
            var neighbors = new int[][] { null, null, null, null, null, null };
            if (i + 1 != VoxelsPerSide[0] && Voxels[i + 1, j, k] != 0) neighbors[1] = new[] { i + 1, j, k };
            if (j + 1 != VoxelsPerSide[1] && Voxels[i, j + 1, k] != 0) neighbors[3] = new[] { i, j + 1, k };
            if (k + 1 != VoxelsPerSide[2] && Voxels[i, j, k + 1] != 0) neighbors[5] = new[] { i, j, k + 1 };
            if (i != 0 && Voxels[i - 1, j, k] != 0) neighbors[0] = new[] { i - 1, j, k };
            if (j != 0 && Voxels[i, j - 1, k] != 0) neighbors[2] = new[] { i, j - 1, k };
            if (k != 0 && Voxels[i, j, k - 1] != 0) neighbors[4] = new[] { i, j, k - 1 };
            return neighbors;
        }

        public void UpdateProperties()
        {
            //SetCount();
            SetVolume();
            //SetSurfaceArea();
        }

        private void SetCount()
        {
            Count = Voxels.Cast<byte>().Count(vox => vox != 0);
        }

        private void SetVolume()
        {
            Volume = Count * Math.Pow(VoxelSideLength, 3);
        }

        //private void SetSurfaceArea()
        //{
        //    var sa = 0;
        //    for (var i = 0; i < VoxelsPerSide[0]; i++)
        //    for (var j = 0; j < VoxelsPerSide[1]; j++)
        //    for (var k = 1; k < VoxelsPerSide[2]; k++)
        //    {
        //        if (k == 1 && Voxels[i, j, k - 1] != 0)
        //            sa++;
        //        if (Voxels[i, j, k] != Voxels[i, j, k - 1])
        //            sa++;
        //        if (k == VoxelsPerSide[2] - 1 && Voxels[i, j, k] != 0)
        //            sa++;
        //    }
        //    for (var j = 0; j < VoxelsPerSide[1]; j++)
        //    for (var k = 0; k < VoxelsPerSide[2]; k++)
        //    for (var i = 1; i < VoxelsPerSide[0]; i++)
        //    {
        //        if (i == 1 && Voxels[i - 1, j, k] == 1)
        //            sa++;
        //        if (Voxels[i, j, k] != Voxels[i - 1, j, k])
        //            sa++;
        //        if (i == VoxelsPerSide[0] - 1 && Voxels[i, j, k] != 0)
        //            sa++;
        //    }
        //    for (var k = 0; k < VoxelsPerSide[2]; k++)
        //    for (var i = 0; i < VoxelsPerSide[0]; i++)
        //    for (var j = 1; j < VoxelsPerSide[1]; j++)
        //    {
        //        if (j == 1 && Voxels[i, j - 1, k] == 1)
        //            sa++;
        //        if (Voxels[i, j, k] != Voxels[i, j - 1, k])
        //            sa++;
        //        if (j == VoxelsPerSide[1] - 1 && Voxels[i, j, k] != 0)
        //            sa++;
        //    }

        //    SurfaceArea = sa * Math.Pow(VoxelSideLength, 2);
        //}

        public VoxelizedSolidCUDA Copy(VoxelizedSolidCUDA vs)
        {
            return new VoxelizedSolidCUDA(vs);
        }

        public VoxelizedSolidCUDA CreateBoundingSolid()
        {
            return new VoxelizedSolidCUDA(VoxelsPerSide, Discretization, VoxelSideLength, Bounds, 1);
        }

        public VoxelizedSolidCUDA InvertToNewSolid_CPU()
        {
            var vs = new VoxelizedSolidCUDA(VoxelsPerSide, Discretization, VoxelSideLength, Bounds);
            
            Parallel.For(0, VoxelsPerSide[0], i =>
            {
                for (var j = 0; j < VoxelsPerSide[1]; j++)
                for (var k = 0; k < VoxelsPerSide[2]; k++)
                    if (Voxels[i, j, k] == 0)
                        vs.Voxels[i, j, k] = 1;
            });

            return vs;
        }

        //static void NOTKernel(Index3 index, ArrayView<byte, Index3> a, byte[,,] b)
        //{
        //    a[index] = (byte) ~b[index.X, index.Y, index.Z];
        //}
        //public VoxelizedSolidCUDA InvertToNewSolid()
        //{
        //    var newVoxels = new byte[VoxelsPerSide[0], VoxelsPerSide[1], VoxelsPerSide[2]];
        //    var idx = new Index3(VoxelsPerSide[0], VoxelsPerSide[1], VoxelsPerSide[2]);

        //    using (var context = new Context())
        //    {
        //        foreach (var acceleratorId in Accelerator.Accelerators)
        //        {
        //            using (var accelerator = Accelerator.Create(context, acceleratorId))
        //            {
        //                var notKernel = accelerator.LoadAutoGroupedStreamKernel<Index3, ArrayView<byte, Index3>, byte[,,]>(NOTKernel);
        //                using (var buffer = accelerator.Allocate<byte, Index3>(idx))
        //                {
        //                    notKernel(buffer.Extent, buffer.View, Voxels);
        //                    accelerator.Synchronize();
        //                    newVoxels = buffer.GetAsArray();
        //                }
        //            }
        //        }
        //    }

        //    return new VoxelizedSolidCUDA(newVoxels, Discretization, VoxelsPerSide, VoxelSideLength, Bounds);
        //}

        //public VoxelizedSolidCUDA InvertToNewSolid()
        //{

        //}
    }
}