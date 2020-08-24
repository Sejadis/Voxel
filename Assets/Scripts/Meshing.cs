using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class Meshing
{
    public int amount;
    public List<int> vertices = new List<int>();
    public List<int> tris = new List<int>();
    public double minTris;
    public double maxTris;
    public double avgTris;
    public double minVerts;
    public double maxVerts;
    public double avgVerts;

    public void Add(int tris, int verts)
    {
        amount++;
        vertices.Add(verts);
        this.tris.Add(tris);

        minTris = this.tris.Min();
        maxTris = this.tris.Max();
        avgTris = this.tris.Average();

        minVerts = vertices.Min();
        maxVerts = vertices.Max();
        avgVerts = vertices.Average();
    }
}