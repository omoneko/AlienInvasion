using System.Collections.Generic;

namespace AlienInvasion.Core
{
    /// <summary>The triangles of a single material, as read from an OBJ.</summary>
    public class ObjSubmesh
    {
        /// <summary>The usemtl name. Faces appearing before the first usemtl get "".</summary>
        public string Material;

        /// <summary>0-based vertex position indices; three per triangle, in Unity's winding order.</summary>
        public List<int> Triangles;

        public ObjSubmesh()
        {
            Material = "";
            Triangles = new List<int>();
        }
    }

    /// <summary>The parsed contents of a Blender-exported OBJ, as a Unity-independent intermediate.</summary>
    public class ObjData
    {
        /// <summary>Every vertex's x, y and z flattened into one list, with X already mirrored.</summary>
        public List<float> Positions;

        public List<ObjSubmesh> Submeshes;

        public ObjData()
        {
            Positions = new List<float>();
            Submeshes = new List<ObjSubmesh>();
        }

        public int VertexCount { get { return Positions.Count / 3; } }
    }
}
