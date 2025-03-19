using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelGenerator
{
    public class IDPool
    {
        public static void Reset()
        {
            ID = 0;
        }

        public static int GenID()
        {
            return ID++;
        }

        private static int ID = 0;
    }
}

