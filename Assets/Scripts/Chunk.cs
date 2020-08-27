using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public Vector3 position;
    public int[] map;
    public static Material mat;

    public void SetMesh(Vector3[] verts, int[] tris, Vector2[] uvs)
    {
        Mesh m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.uv = uvs;
        m.RecalculateNormals();
        m.RecalculateTangents();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.material = mat;
        meshFilter.mesh = m;
    }
    
    public void SetMesh(Vector3[] verts, int[] tris, Color32[] colors)
    {
        Mesh m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.colors32 = colors;
        m.RecalculateNormals();
        m.RecalculateTangents();
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.material = mat;
        meshFilter.mesh = m;
    }

    public static int GetIndexForPos(int x, int y, int z, int3 dimension)
    {
        return x + y * dimension.x + z * dimension.x * dimension.y;
    }

//TODO move to utility class or something
    public static int GetIndexForPos(int3 position, int3 dimension)
    {
        return GetIndexForPos(position.x, position.y, position.z, dimension);
    }

    public static int3 GetPosForIndex(int i, int3 dimension)
    {
        int xDirection = i % dimension.x;
        int yDirection = (i / dimension.x) % dimension.y;
        int zDirection = i / (dimension.y * dimension.x);
        return new int3(xDirection, yDirection, zDirection);
    }
}