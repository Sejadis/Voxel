using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Chunk : MonoBehaviour
{
    public int ChunkWidth = 5;
    public int ChunkHeight = 50;

    public float scale = 1;
    public float scale2 = 1;
    public float offset = 1;
    public float offset2 = 1;

    public Vector3 position;
    public float[] map;
    public float threshold = 0.45f;
    public int3 dimension;
    public List<Vector3> vertList = new List<Vector3>();
    public List<int> indexList = new List<int>();

    public WorldGenerator world;
    public static Material mat;

    [ContextMenu("Build")]
    public void Build()
    {
        var dimension = new int3(ChunkWidth, ChunkHeight, ChunkWidth);
        Build(int3.zero, dimension);
    }

    public void Build(int3 pos, int3 dimension)
    {
        this.dimension = dimension;
        map = new float[dimension.x * dimension.y * dimension.z];
        var pos2 = new int3();
        for (int x = 0; x < dimension.x; x++)
        {
            pos2.x = x;
            for (int y = 0; y < dimension.y; y++)
            {
                pos2.y = y;
                for (int z = 0; z < dimension.z; z++)
                {
                    pos2.z = z;
                    var noisePos = new Vector3(position.x + x, position.y + y, position.z + z);
                    // var noise = Noise.Perlin2D(noisePos, scale, offset, ChunkWidth);
                    var noise = Noise.Perlin3D(noisePos, scale, offset, dimension);
                    map[GetIndexForPos(pos2, dimension)] = noise;
                }
            }
        }
    }

    public void SetMesh(Vector3[] verts, int[] tris)
    {
        Mesh m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
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

    [ContextMenu("Build Mesh")]
    public void BuildMesh()
    {
        vertList = new List<Vector3>();
        indexList = new List<int>();
        int ix, iy, iz;
        float[] vertices = new float[8];
        for (int x = -1; x < ChunkWidth; x++)
            // for (int x = 0; x < ChunkWidth - 1; x++)
        {
            for (int y = 0; y < ChunkHeight - 1; y++)
            {
                for (int z = -1; z < ChunkWidth; z++)
                    // for (int z = 0; z < ChunkWidth - 1; z++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        ix = x + MeshData.VertexOffset[i, 0];
                        iy = y + MeshData.VertexOffset[i, 1];
                        iz = z + MeshData.VertexOffset[i, 2];
                        int index = ix + iy * ChunkWidth + iz * ChunkWidth * ChunkHeight;
                        var worldPos = position + new Vector3(ix, iy, iz);
                        vertices[i] = world.GetVoxelFromWorldPos(worldPos);
                        // vertices[i] = index >= map.Length ? world.GetVoxelFromWorldPos(worldPos) : map[index];
                        // Debug.Log($"calling with index {index} and value {vertices[i]}");
                    }

                    BuildMeshData(new int3(x, y, z), vertices);
                }
            }
        }

        Mesh m = new Mesh();
        m.vertices = vertList.ToArray();
        m.triangles = indexList.ToArray();
        m.RecalculateNormals();
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
        gameObject.AddComponent<MeshCollider>();
    }

    public void BuildMeshData(int3 position, float[] vertices)
    {
        float3[] EdgeVertex = new float3 [12];
        int idx = 0, vert = 2;
        float offset = 2;
        int flagIndex = 0;

        for (int i = 0; i < 8; i++)
        {
            if (vertices[i] == 0)
                flagIndex |= 1 << i;
        }

        int edgeFlags = MeshData.CubeEdgeFlags[flagIndex];
        // Debug.Log("flagIndex " + flagIndex + " edgeFlags " + edgeFlags);
        //If the cube is entirely inside or outside of the surface, then there will be no intersections
        if (edgeFlags == 0) return;

        //Find the point of intersection of the surface with each edge
        for (int i = 0; i < 12; i++)
        {
            //if there is an intersection on this edge
            if ((edgeFlags & (1 << i)) != 0)
            {
                offset = GetOffset(vertices[MeshData.EdgeConnection[i, 0]], vertices[MeshData.EdgeConnection[i, 1]]);

                EdgeVertex[i].x =
                    position.x + (MeshData.VertexOffset[MeshData.EdgeConnection[i, 0], 0] +
                                  offset * MeshData.EdgeDirection[i, 0]);
                EdgeVertex[i].y =
                    position.y + (MeshData.VertexOffset[MeshData.EdgeConnection[i, 0], 1] +
                                  offset * MeshData.EdgeDirection[i, 1]);
                EdgeVertex[i].z =
                    position.z + (MeshData.VertexOffset[MeshData.EdgeConnection[i, 0], 2] +
                                  offset * MeshData.EdgeDirection[i, 2]);
            }

            // Debug.Log("vertex " + EdgeVertex[i] + " with offset " + offset);
        }

        //Save the triangles that were found. There can be up to five per cube
        for (int i = 0; i < 5; i++)
        {
            if (MeshData.TriangleConnectionTable[flagIndex, 3 * i] < 0) break;

            idx = vertList.Count;

            for (int j = 0; j < 3; j++)
            {
                vert = MeshData.TriangleConnectionTable[flagIndex, 3 * i + j];
                var value = idx + WindingOrder[j];
                indexList.Add(value);
                vertList.Add(EdgeVertex[vert]);
                // Debug.Log("vert " + vert + " index " + (indexList.Count -1)  + " value " + value);
            }
        }
    }

    public int[] WindingOrder = new[] {2, 1, 0};

    private float GetOffset(float v1, float v2)
    {
        float delta = v2 - v1;
        return (delta == 0.0f) ? threshold : (threshold - v1) / delta;
    }

    public static int GetIndexForPos(int x, int y, int z, int3 dimension)
    {
        return x + y * dimension.x + z * dimension.x * dimension.y;
        // return x + y * ChunkWidth + z * ChunkWidth * ChunkHeight;
    }

    public static int3 GetPosForIndex(int i, int3 dimension)
    {
        int xDirection = i % dimension.x;
        int yDirection = (i / dimension.x) % dimension.y;
        int zDirection = i / (dimension.y * dimension.x);
        return new int3(xDirection, yDirection, zDirection);
    }

    public static int GetIndexForPos(int3 position, int3 dimension)
    {
        return GetIndexForPos(position.x, position.y, position.z, dimension);
    }

    [BurstCompile]
    public struct MeshJob : IJob
    {
        // private static readonly NativeArray<int> test = new NativeArray<int>(
        // {
        //     0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0,
        //     0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1
        // },Allocator.Per);

        ///*

        #region static data

        private static readonly int[] VertexOffset =
        {
            0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0,
            0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1
        };

        private static readonly int[] EdgeConnection =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        private static readonly float[] EdgeDirection =
        {
            1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, -1.0f, 0.0f,
            1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, -1.0f, 0.0f, 0.0f, 0.0f, -1.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f
        };

        private static readonly int[] CubeEdgeFlags =
        {
            0x000, 0x109, 0x203, 0x30a, 0x406, 0x50f, 0x605, 0x70c, 0x80c, 0x905, 0xa0f, 0xb06, 0xc0a, 0xd03, 0xe09,
            0xf00,
            0x190, 0x099, 0x393, 0x29a, 0x596, 0x49f, 0x795, 0x69c, 0x99c, 0x895, 0xb9f, 0xa96, 0xd9a, 0xc93, 0xf99,
            0xe90,
            0x230, 0x339, 0x033, 0x13a, 0x636, 0x73f, 0x435, 0x53c, 0xa3c, 0xb35, 0x83f, 0x936, 0xe3a, 0xf33, 0xc39,
            0xd30,
            0x3a0, 0x2a9, 0x1a3, 0x0aa, 0x7a6, 0x6af, 0x5a5, 0x4ac, 0xbac, 0xaa5, 0x9af, 0x8a6, 0xfaa, 0xea3, 0xda9,
            0xca0,
            0x460, 0x569, 0x663, 0x76a, 0x066, 0x16f, 0x265, 0x36c, 0xc6c, 0xd65, 0xe6f, 0xf66, 0x86a, 0x963, 0xa69,
            0xb60,
            0x5f0, 0x4f9, 0x7f3, 0x6fa, 0x1f6, 0x0ff, 0x3f5, 0x2fc, 0xdfc, 0xcf5, 0xfff, 0xef6, 0x9fa, 0x8f3, 0xbf9,
            0xaf0,
            0x650, 0x759, 0x453, 0x55a, 0x256, 0x35f, 0x055, 0x15c, 0xe5c, 0xf55, 0xc5f, 0xd56, 0xa5a, 0xb53, 0x859,
            0x950,
            0x7c0, 0x6c9, 0x5c3, 0x4ca, 0x3c6, 0x2cf, 0x1c5, 0x0cc, 0xfcc, 0xec5, 0xdcf, 0xcc6, 0xbca, 0xac3, 0x9c9,
            0x8c0,
            0x8c0, 0x9c9, 0xac3, 0xbca, 0xcc6, 0xdcf, 0xec5, 0xfcc, 0x0cc, 0x1c5, 0x2cf, 0x3c6, 0x4ca, 0x5c3, 0x6c9,
            0x7c0,
            0x950, 0x859, 0xb53, 0xa5a, 0xd56, 0xc5f, 0xf55, 0xe5c, 0x15c, 0x055, 0x35f, 0x256, 0x55a, 0x453, 0x759,
            0x650,
            0xaf0, 0xbf9, 0x8f3, 0x9fa, 0xef6, 0xfff, 0xcf5, 0xdfc, 0x2fc, 0x3f5, 0x0ff, 0x1f6, 0x6fa, 0x7f3, 0x4f9,
            0x5f0,
            0xb60, 0xa69, 0x963, 0x86a, 0xf66, 0xe6f, 0xd65, 0xc6c, 0x36c, 0x265, 0x16f, 0x066, 0x76a, 0x663, 0x569,
            0x460,
            0xca0, 0xda9, 0xea3, 0xfaa, 0x8a6, 0x9af, 0xaa5, 0xbac, 0x4ac, 0x5a5, 0x6af, 0x7a6, 0x0aa, 0x1a3, 0x2a9,
            0x3a0,
            0xd30, 0xc39, 0xf33, 0xe3a, 0x936, 0x83f, 0xb35, 0xa3c, 0x53c, 0x435, 0x73f, 0x636, 0x13a, 0x033, 0x339,
            0x230,
            0xe90, 0xf99, 0xc93, 0xd9a, 0xa96, 0xb9f, 0x895, 0x99c, 0x69c, 0x795, 0x49f, 0x596, 0x29a, 0x393, 0x099,
            0x190,
            0xf00, 0xe09, 0xd03, 0xc0a, 0xb06, 0xa0f, 0x905, 0x80c, 0x70c, 0x605, 0x50f, 0x406, 0x30a, 0x203, 0x109,
            0x000
        };

        private static readonly int[] TriangleConnectionTable =
        {
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1,

            3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1,

            3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1,

            3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1,

            9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1,

            9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1,

            2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1,

            8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1,

            9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1,

            4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1,

            3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1,

            1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1,

            4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1,

            4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1,

            9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1,

            5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1,

            2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1,

            9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1,

            0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1,

            2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1,

            10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1,

            4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1,

            5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1,

            5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1,

            9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1,

            0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1,

            1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1,

            10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1,

            8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1,

            2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1,

            7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1,

            9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1,

            2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1,

            11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1,

            9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1,

            5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1,

            11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1,

            11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1,

            1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1,

            9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1,

            5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1,

            2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1,

            0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1,

            5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1,

            6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1,

            3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1,

            6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1,

            5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1,

            1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1,

            10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1,

            6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1,

            8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1,

            7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1,

            3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1,

            5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1,

            0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1,

            9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1,

            8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1,

            5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1,

            0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1,

            6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1,

            10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1,

            10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1,

            8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1,

            1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1,

            3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1,

            0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1,

            10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1,

            3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1,

            6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1,

            9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1,

            8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1,

            3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1,

            6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1,

            0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1,

            10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1,

            10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1,

            2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1,

            7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1,

            7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1,

            2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1,

            1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1,

            11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1,

            8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1,

            0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1,

            7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1,

            10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1,

            2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1,

            6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1,

            7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1,

            2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1,

            1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1,

            10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1,

            10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1,

            0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1,

            7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1,

            6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1,

            8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1,

            9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1,

            6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1,

            4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1,

            10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1,

            8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1,

            0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1,

            1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1,

            8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1,

            10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1,

            4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1,

            10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1,

            5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1,

            11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1,

            9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1,

            6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1,

            7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1,

            3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1,

            7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1,

            9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1,

            3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1,

            6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1,

            9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1,

            1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1,

            4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1,

            7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1,

            6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1,

            3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1,

            0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1,

            6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1,

            0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1,

            11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1,

            6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1,

            5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1,

            9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1,

            1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1,

            1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1,

            10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1,

            0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1,

            5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1,

            10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1,

            11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1,

            9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1,

            7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1,

            2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1,

            8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1,

            9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1,

            9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1,

            1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1,

            9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1,

            9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1,

            5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1,

            0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1,

            10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1,

            2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1,

            0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1,

            0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1,

            9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1,

            5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1,

            3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1,

            5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1,

            8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1,

            0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1,

            9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1,

            0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1,

            1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1,

            3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1,

            4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1,

            9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1,

            11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1,

            11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1,

            2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1,

            9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1,

            3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1,

            1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1,

            4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1,

            4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1,

            0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1,

            3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1,

            3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1,

            0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1,

            9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1,

            1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,

            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1
        };

        #endregion

//*/
        public NativeList<float3> verts;

        public NativeList<int> tris;

        // public NativeArray<float3> vertices;
        // public NativeArray<int> triangles;
        [ReadOnly] public NativeArray<float> map;

        // [ReadOnly] public NativeArray<int> VertexOffset;
        // [ReadOnly] public NativeArray<int> EdgeConnection;
        // [ReadOnly] public NativeArray<float> EdgeDirection;
        // [ReadOnly] public NativeArray<int> CubeEdgeFlags;
        // [ReadOnly] public NativeArray<int> TriangleConnectionTable;
        [ReadOnly] public int chunkWidth;
        [ReadOnly] public int chunkHeight;
        [ReadOnly] public float threshold;

        public void Execute()
        {
            int ix, iy, iz, currentIndex = 0;
            NativeArray<float> cubeData = new NativeArray<float>(8, Allocator.Temp);
            for (int x = 0; x < chunkWidth; x++)
            {
                for (int y = 0; y < chunkHeight - 1; y++)
                {
                    for (int z = 0; z < chunkWidth; z++)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            ix = x + VertexOffset[i * 3 + 0];
                            iy = y + VertexOffset[i * 3 + 1];
                            iz = z + VertexOffset[i * 3 + 2];
                            int index = ix + iy * (chunkWidth + 1) + iz * (chunkWidth + 1) * chunkHeight;
                            cubeData[i] = index >= map.Length ? 0 : map[index];
                            // Debug.Log("calling with index"  + index + " and value " + cubeData[i]);
                        }

                        BuildMeshData(new int3(x, y, z), cubeData, ref currentIndex);
                    }
                }
            }

            // for (int i = currentIndex; i < triangles.Length; i++)
            // {
            //     triangles[i] = -1;
            //     vertices[i] = -1;
            // }
        }

        private float GetOffset(float v1, float v2)
        {
            float delta = v2 - v1;
            return (delta == 0.0f) ? threshold : (threshold - v1) / delta;
        }

        public void BuildMeshData(int3 position, NativeArray<float> cubeData, ref int currentIndex)
        {
            NativeArray<float3> EdgeVertex = new NativeArray<float3>(12, Allocator.Temp);
            float offset = 2;
            int vert = 2;

            int flagIndex = 0;

            for (int i = 0; i < 8; i++)
                if (cubeData[i] == 1)
                    flagIndex |= 1 << i;

            int edgeFlags = CubeEdgeFlags[flagIndex];
            // Debug.Log("flagIndex " + flagIndex + " edgeFlags " + edgeFlags);
            //If the cube is entirely inside or outside of the surface, then there will be no intersections
            if (edgeFlags == 0) return;

            //Find the point of intersection of the surface with each edge
            for (int i = 0; i < 12; i++)
            {
                //if there is an intersection on this edge
                if ((edgeFlags & (1 << i)) != 0)
                {
                    offset = GetOffset(cubeData[EdgeConnection[i * 2 + 0]],
                        cubeData[EdgeConnection[i * 2 + 1]]);
                    var edgeVertex = EdgeVertex[i];
                    edgeVertex.x =
                        position.x + (VertexOffset[EdgeConnection[i * 2 + 0] * 3 + 0] +
                                      offset * EdgeDirection[i * 3 + 0]);
                    edgeVertex.y =
                        position.y + (VertexOffset[EdgeConnection[i * 2 + 0] * 3 + 1] +
                                      offset * EdgeDirection[i * 3 + 1]);
                    edgeVertex.z =
                        position.z + (VertexOffset[EdgeConnection[i * 2 + 0] * 3 + 2] +
                                      offset * EdgeDirection[i * 3 + 2]);
                    EdgeVertex[i] = edgeVertex;
                }

                // Debug.Log("vertex " + EdgeVertex[i] + " with offset " + offset);
            }

            //Save the triangles that were found. There can be up to five per cube
            for (int i = 0; i < 5; i++)
            {
                if (TriangleConnectionTable[flagIndex * 16 + 3 * i] < 0) break;

                for (int j = 0; j < 3; j++)
                {
                    vert = TriangleConnectionTable[flagIndex * 16 + 3 * i + j];
                    var value = currentIndex + j;
                    // triangles[currentIndex + j] = value;
                    // vertices[currentIndex + j] = EdgeVertex[vert];
                    verts.Add(EdgeVertex[vert]);
                    tris.Add(value);
                    // Debug.Log("vert " + vert + " index " + (currentIndex + j) + " value " + value);
                }

                currentIndex += 3;
            }
        }
    }

    public struct BuildJob : IJob
    {
        public int3 position;
        public int3 dimension;
        public float scale;
        public float scale2;
        public float offset;
        public float offset2;
        public int groundHeight;
        public int terrainHeight;
        public NativeArray<float> map;

        public void Execute()
        {
            var pos = new int3();
            var t = 1;
            for (int x = 0; x < dimension.x; x++)
            {
                pos.x = x;
                for (int y = 0; y < dimension.y; y++)
                {
                    pos.y = y;
                    for (int z = 0; z < dimension.z; z++)
                    {
                        pos.z = z;
                        if (y <= groundHeight)
                        {
                            t = 0;
                        }
                        else
                        {
                            var noisePos = new Vector3(position.x + x, position.y + y, position.z + z);
                            // var noise = Noise.Perlin2D(noisePos, scale, offset, ChunkWidth);
                            var noise = Noise.Perlin3D(noisePos, scale, offset, dimension);
                            var terrain = Mathf.FloorToInt(noise * terrainHeight);
                            terrain += groundHeight;
                            if (y <= terrain)
                            {
                                t = 0;
                            }
                        }

                        map[GetIndexForPos(pos, dimension)] = t;
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct BuildJobBurst : IJob
    {
        public int3 position;
        public int3 dimension;
        public int groundHeight;
        public int terrainHeight;
        public NativeArray<float> map;
        public WorldGenerator.ChunkSettings Settings;

        public void Execute()
        {
            int voxelValue;
            var pos = new int3();
            var solidGround = 2;
            var dim2D = new int2(dimension.x, dimension.z);
            for (int x = 0; x < dimension.x; x++)
            {
                pos.x = position.x + x;
                var ratioNoise = Noise.Perlin2D(new int2(pos.x, 1), Settings.ratioNoise.scale,
                    Settings.ratioNoise.offset, new int2(1234, 1));
                for (int z = 0; z < dimension.z; z++)
                {
                    pos.z = position.z + z;
                    var noise2D = Noise.Perlin2D(new int2(pos.x, pos.z), Settings.surfaceNoise.scale,
                        Settings.surfaceNoise.offset, dim2D);
                    var terrain = Mathf.CeilToInt(noise2D * terrainHeight);
                    var surfaceHeight = terrain + groundHeight;

                    for (int y = 0; y < dimension.y; y++)
                    {
                        pos.y = position.y + y;
                        var surfaceNoise = Noise.Perlin3D(pos, Settings.surfaceNoise.scale,
                            Settings.surfaceNoise.offset, dimension);
                        var entranceNoise = Noise.Perlin3D(pos, Settings.caveSettings.entranceNoise.scale,
                            Settings.caveSettings.entranceNoise.offset, dimension);
                        var maxSurfaceHeight = terrain -
                                               (y / 2f * Noise.Perlin2D(new Vector2(pos.x, pos.z), 0.5f,
                                                   345.345f,
                                                   new int2(dimension.x, dimension.z)));
                        //subsurface
                        if (y < surfaceHeight - solidGround)
                        {
                            // solid ground at the bottom
                            if (y <= solidGround)
                            {
                                voxelValue = 0;
                            }
                            //caves
                            else if (y < surfaceHeight + Settings.caveSettings.relativeCaveHeight)
                            {
                                var caveFloorHeight = solidGround + 5;
                                var caveNoise = Noise.Perlin3D(pos, Settings.caveSettings.caveNoise.scale,
                                    Settings.caveSettings.caveNoise.offset, dimension);
                                if (Settings.caveSettings.caveThreshold.IsWithinThresholdSqr(caveNoise * caveNoise))
                                {
                                    //cave floor
                                    if (y <= caveFloorHeight)
                                    {
                                        var n1 = Noise.Perlin2D(new Vector2(pos.x, pos.z), 0.5f, 690823.234f,
                                            dim2D);
                                        var n2 = Noise.Perlin2D(new Vector2(pos.x, pos.z), 3f, 690823.234f,
                                            dim2D);
                                        var n = n1 * ratioNoise + (1 - ratioNoise) * n2;
                                        voxelValue = y <= caveFloorHeight * 2 * n
                                            ? 0
                                            : 1;
                                    }
                                    else
                                    {
                                        voxelValue = 1;
                                    }
                                }
                                else
                                {
                                    voxelValue = 0;
                                }
                            }
                            else
                            {
                                voxelValue = 0;
                            }
                        }
                        //surface
                        else if (y >= surfaceHeight + Settings.caveSettings.relativeCaveHeight && y <= surfaceHeight)
                        {
                            voxelValue = Settings.caveSettings.entranceThreshold.IsWithinThreshold(entranceNoise)
                                ? 1
                                : 0;
                        }
                        //above surface
                        else if (Settings.surfaceThreshold.IsWithinThreshold(surfaceNoise) &&
                                 y <= surfaceHeight + maxSurfaceHeight)
                        {
                            voxelValue = 0;
                        }

                        else
                        {
                            voxelValue = 1;
                        }

                        var index = GetIndexForPos(x, y, z, dimension);
                        map[index] = voxelValue;
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct BuildJobBurstParallel : IJobParallelFor
    {
        public int3 position;
        public int3 dimension;
        public float scale;
        public float scale2;
        public float offset;
        public float offset2;
        public int groundHeight;
        public int terrainHeight;
        public float threshold;
        public float caveThreshold;
        public NativeArray<float> map;

        public void Execute(int index)
        {
            var posFromIndex = GetPosForIndex(index, dimension);
            var t = 0;
            var x = posFromIndex.x;
            var y = posFromIndex.y;
            var z = posFromIndex.z;

            var noisePos = new int3(position.x + x, position.y + y, position.z + z);
            // var noise = Noise.Perlin2D(noisePos, scale, offset, ChunkWidth);
            var noise = Noise.Perlin3D(noisePos, scale, offset, dimension);
            var terrain = Mathf.FloorToInt(noise * terrainHeight);
            terrain += groundHeight;

            //caves
            if (y < terrain - caveThreshold)
            {
                if (y == 0)
                {
                    t = 1;
                }
                else if (y <= 5f)
                {
                    t = y <= 10f * Noise.Perlin2D(new int2(noisePos.x, noisePos.z), 1f, 690823.234f,
                        new int2(dimension.x, dimension.z))
                        ? 0
                        : 1;
                }
                else if (y < terrain)
                {
                    t = noise * noise > threshold * threshold ? 1 : 0;
                }
                else
                {
                    t = 1;
                }
            }
            //overhangs + top of plateaus
            else if ((noise > threshold && y <= groundHeight + terrainHeight -
                (y / 2f * Noise.Perlin2D(new int2(noisePos.x, noisePos.z), 0.5f, 345.345f,
                    new int2(dimension.x, dimension.z)))))
            {
                t = 1;
            }
            else
            {
                t = 0;
            }

            map[index] = t;
        }
    }
}