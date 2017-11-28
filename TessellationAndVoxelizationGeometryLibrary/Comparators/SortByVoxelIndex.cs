using System.Collections.Generic;
using TVGL.Voxelization;

namespace TVGL
{
    internal class SortByVoxelIndex : IComparer<VoxelClass>
    {
        private int dimension;

        internal SortByVoxelIndex(int dimension)
        {
            this.dimension = dimension;
        }
        public int Compare(VoxelClass x, VoxelClass y)
        {
            //if (x.Index[dimension].Equals(y.Index[dimension])) return 0;
            if (VoxelizedSolid.GetCoordinateFromID(x.ID, dimension, 4, 4) <
                VoxelizedSolid.GetCoordinateFromID(y.ID, dimension, 4, 4)) return -1;
            else return 1;
        }
    }
}