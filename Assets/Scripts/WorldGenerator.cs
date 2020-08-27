// #define injectData

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

public class WorldGenerator : MonoBehaviour
{
    private int3 lastChunkPosition;
    public Transform cameraTransform;
    public int ChunkWidth;
    public int ChunkHeight;

    public int viewDistance = 3;

    [FormerlySerializedAs("chunkSettings")]
    public TerrainSettings terrainSettings;

    public int testSize = 10;
    public List<int> batchSizes = new List<int>();
    public int batchSize = 1;

    private Dictionary<int3, Chunk> chunks = new Dictionary<int3, Chunk>();
    public Color32 error = new Color32(255, 0, 0, 255);
    public Color32 surface = new Color32(0, 255, 0, 255);
    public Color32 mountain = new Color32(107, 107, 107, 255);
    public Color32 earth = new Color32(112, 55, 14, 255);

    public bool useParallel;

    // public Meshing meshing;
    public Material mat;

    public TimingContainer timings;
#if meshjob && injectData
    NativeArray<int> vertexOffset;
    NativeArray<int> edgeConnection;
    NativeArray<float> EdgeDirection;
    NativeArray<int> CubeEdgeFlags;
    private NativeArray<int> TriangleConnectionTable;
    private bool isInited = false;
#endif

    [ContextMenu("Reset Timings")]
    public void ResetTimings()
    {
        timings.burst = new Timing();
        timings.parallel1 = new Timing();
        timings.parallel8 = new Timing();
        timings.parallel16 = new Timing();
        timings.parallel32 = new Timing();
        timings.parallel64 = new Timing();
        timings.parallel128 = new Timing();
    }

    private IEnumerator Test()
    {
        yield return null;
        Debug.Log("Starting Test");
        // for (int i = 0; i < testSize; i++)
        // {
        //     ClearChunks();
        //     BuildWithoutJobs();
        //     yield return null;
        // }
        //
        // useBurst = false;
        // for (int i = 0; i < testSize; i++)
        // {
        //     ClearChunks();
        //     BuildWithJobs();
        //     yield return null;
        // }

        useParallel = false;
        for (int i = 0; i < testSize; i++)
        {
            ClearChunks();
            BuildWithJobs();
            yield return null;
        }


        useParallel = true;
        foreach (var b in batchSizes)
        {
            batchSize = b;
            for (int i = 0; i < testSize; i++)
            {
                ClearChunks();
                BuildWithJobs();
                yield return null;
            }
        }

        Debug.Log("Test finished");
    }

#if meshjob && injectData
    private void OnDestroy()
    {
        vertexOffset.Dispose();
        edgeConnection.Dispose();
        EdgeDirection.Dispose();
        CubeEdgeFlags.Dispose();
        TriangleConnectionTable.Dispose();
    }
#endif

    [ContextMenu("Build")]
    void Start()
    {
        Chunk.mat = mat;
        ClearChunks();
        BuildWithJobs();
    }

    [ContextMenu("Run Test")]
    public void RunTest()
    {
        StartCoroutine(Test());
    }

    private void ClearChunks()
    {
        foreach (var chunk in chunks)
        {
            Destroy(chunk.Value.gameObject);
        }

        chunks.Clear();
    }

    private void BuildWithJobs()
    {
        Stopwatch watch = new Stopwatch();
        watch.Start();
        var dimension = new int3(ChunkWidth, ChunkHeight, ChunkWidth);
        var index = 0;
        var chunkList = new List<Chunk>();
        NativeArray<JobHandle> handles =
            new NativeArray<JobHandle>((viewDistance * 2 + 1) * (viewDistance * 2 + 1), Allocator.Temp);
        List<NativeArray<int>> maps = new List<NativeArray<int>>();

        List<NativeList<int>> triangleList = new List<NativeList<int>>();
        List<NativeList<float3>> verticesList = new List<NativeList<float3>>();
        List<NativeList<int2>> uvList = new List<NativeList<int2>>();


#if injectData
        if (!isInited)
        {
            vertexOffset = new NativeArray<int>(MeshData.VertexOffset2.Length, Allocator.Persistent);
            edgeConnection = new NativeArray<int>(MeshData.EdgeConnection2.Length, Allocator.Persistent);
            EdgeDirection = new NativeArray<float>(MeshData.EdgeDirection2.Length, Allocator.Persistent);
            CubeEdgeFlags = new NativeArray<int>(MeshData.CubeEdgeFlags2.Length, Allocator.Persistent);
            TriangleConnectionTable =
                new NativeArray<int>(MeshData.TriangleConnectionTable2.Length, Allocator.Persistent);
        
            vertexOffset.CopyFrom(MeshData.VertexOffset2);
            edgeConnection.CopyFrom(MeshData.EdgeConnection2);
            EdgeDirection.CopyFrom(MeshData.EdgeDirection2);
            CubeEdgeFlags.CopyFrom(MeshData.CubeEdgeFlags2);
            TriangleConnectionTable.CopyFrom(MeshData.TriangleConnectionTable2);
            isInited = true;
        }
#endif
        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                var go = new GameObject();
                go.transform.SetParent(transform);
                var pos = new Vector3(x * ChunkWidth, 0, z * ChunkWidth);
                var posInt = new int3(pos);
                go.transform.position = pos;
                var chunk = go.AddComponent<Chunk>();
                chunk.position = pos;

                var adjDimension = dimension;
                adjDimension.x++;
                adjDimension.z++;

                var length = adjDimension.x * adjDimension.y * adjDimension.z;
                var map = new NativeArray<int>(length, Allocator.TempJob);

                var verts = new NativeList<float3>(Allocator.TempJob);
                var tris = new NativeList<int>(Allocator.TempJob);
                var uvs = new NativeList<int2>(Allocator.TempJob);

                JobHandle handle;

                if (useParallel)
                {
                    var job = new BuildJobBurstParallel()
                    {
                        dimension = adjDimension,
                        position = posInt,
                        groundHeight = terrainSettings.groundHeight,
                        terrainHeight = terrainSettings.terrainHeight,
                        map = map,
                        Settings = terrainSettings,
                    };
                    handle = job.Schedule(length, batchSize);
                }
                else
                {
                    var job = new BuildJobBurst()
                    {
                        dimension = adjDimension,
                        position = posInt,
                        groundHeight = terrainSettings.groundHeight,
                        terrainHeight = terrainSettings.terrainHeight,
                        map = map,
                        Settings = terrainSettings,
                    };
                    handle = job.Schedule();
                }

                JobHandle.ScheduleBatchedJobs();
                var job2 = new BuildMeshJob()
                {
                    chunkHeight = ChunkHeight,
                    chunkWidth = ChunkWidth,
                    map = map,
#if injectData
                            VertexOffset = vertexOffset,
                            EdgeDirection = EdgeDirection,
                            EdgeConnection = edgeConnection,
                            CubeEdgeFlags = CubeEdgeFlags,
                            TriangleConnectionTable = TriangleConnectionTable,
#endif
                    tris = tris,
                    verts = verts,
                    uvs = uvs,
                };
                handle = job2.Schedule(handle);

                triangleList.Add(tris);
                verticesList.Add(verts);
                uvList.Add(uvs);

                maps.Add(map);
                chunkList.Add(chunk);
                handles[index++] = handle;
            }
        }

        // Debug.Log(watch.Elapsed.ToString());

        JobHandle.CompleteAll(handles);
        for (int i = 0; i < chunkList.Count; i++)
        {
            chunkList[i].map = maps[i].ToArray();
            maps[i].Dispose();

            var tris = triangleList[i].ToArray();
            Vector3[] verts = new Vector3[verticesList[i].Length];
            var x = 0;
            foreach (var vertex in verticesList[i])
            {
                verts[x++] = new Vector3(vertex.x, vertex.y, vertex.z);
            }

            Color32[] colors = new Color32[uvList[i].Length];
            Color32 lastColor = new Color32();
            x = 0;
            foreach (var uv in uvList[i])
            {
                Color32 c = error;
                switch (uv.x)
                {
                    case -1:
                    {
                        c = lastColor;
                        break;
                    }
                    case 0:
                    {
                        c = surface;
                        break;
                    }
                    case 1:
                    {
                        c = earth;
                        break;
                    }
                    case 2:
                    {
                        c = mountain;
                        break;
                    }
                }

                lastColor = c;
                colors[x++] = c;
            }
            // Vector2[] uvs = new Vector2[uvList[i].Length];
            // x = 0;
            // foreach (var uv in uvList[i])
            // {
            //     uvs[x++] = new Vector2(uv.x, uv.y);
            // }

            // chunkList[i].SetMesh(verts, tris, uvs);
            chunkList[i].SetMesh(verts, tris, colors);
            verticesList[i].Dispose();
            triangleList[i].Dispose();
            uvList[i].Dispose();
            chunks[new int3(chunkList[i].position)] = chunkList[i];
        }

        handles.Dispose();
        watch.Stop();

        if (useParallel)
        {
            ref Timing timing = ref timings.parallel1;
            switch (batchSize)
            {
                case 8:
                {
                    timing = ref timings.parallel8;
                    break;
                }
                case 16:
                {
                    timing = ref timings.parallel16;
                    break;
                }
                case 32:
                {
                    timing = ref timings.parallel32;
                    break;
                }
                case 64:
                {
                    timing = ref timings.parallel64;
                    break;
                }
                case 128:
                {
                    timing = ref timings.parallel128;
                    break;
                }
            }

            timing.Add(watch.Elapsed);
        }
        else
        {
            timings.burst.Add(watch.Elapsed);
        }

        // foreach (var chunk in chunks)
        // {
        //     var mf = chunk.gameObject.GetComponent<MeshFilter>();
        //     meshing.Add(mf.mesh.triangles.Length, mf.mesh.vertexCount);
        // }

        // Debug.Log(watch.Elapsed.ToString());
    }

    private void Update()
    {
        int3 pos = new int3(cameraTransform.position);
        pos.x -= Mathf.Sign(pos.x) < 0 ? ChunkWidth - pos.x % ChunkWidth : pos.x % ChunkWidth;
        pos.z -= Mathf.Sign(pos.z) < 0 ? ChunkWidth - pos.z % ChunkWidth : pos.z % ChunkWidth;
        pos.x /= ChunkWidth;
        pos.z /= ChunkWidth;
        pos.y = 0;
        var delta = pos - lastChunkPosition;
        lastChunkPosition = pos;
        if (!delta.Equals(int3.zero))
        {
            var thisPos = pos * ChunkWidth;
            var change = delta * ChunkWidth;
            var buildPos = thisPos + change * viewDistance;
            var removePos = thisPos - change * (viewDistance + 1);

            List<int3> remove = new List<int3>();
            List<int3> add = new List<int3>();
            for (int i = -viewDistance; i <= viewDistance; i++)
            {
                var offset = i * ChunkWidth;
                if (delta.x != 0)
                {
                    buildPos.z = thisPos.z + offset;
                    removePos.z = thisPos.z + offset;
                }
                else if (delta.z != 0)
                {
                    buildPos.x = thisPos.x + offset;
                    removePos.x = thisPos.x + offset;
                }
                else
                {
                    //whats happening here? that shouldn't be possible
                    continue;
                }

                add.Add(buildPos);
                remove.Add(removePos);
            }

            foreach (var int3 in remove)
            {
                Destroy(chunks[int3].gameObject);
                chunks.Remove(int3);
            }

            remove.Clear();

            foreach (var int3 in add)
            {
                BuildChunk(int3);
            }

            add.Clear();
        }
    }

    [ContextMenu("Load new Chunk")]
    public void LoadChunk()
    {
        var pos = lastChunkPosition;
        pos.x *= ChunkWidth;
        pos.y *= ChunkHeight;
        pos.z *= ChunkWidth;
        if (!chunks.ContainsKey(pos))
        {
            BuildChunk(pos);
        }
        else
        {
            Destroy(chunks[pos].gameObject);
            chunks.Remove(pos);
        }
    }

    public void BuildChunk(int3 position)
    {
        var dimension = new int3(ChunkWidth, ChunkHeight, ChunkWidth);
        var go = new GameObject();
        go.transform.SetParent(transform);
        var posVector = new Vector3(position.x, position.y, position.z);
        go.transform.position = posVector;
        var chunk = go.AddComponent<Chunk>();
        chunk.position = posVector;

        var adjDimension = dimension;
        adjDimension.x++;
        adjDimension.z++;

        var length = adjDimension.x * adjDimension.y * adjDimension.z;
        var map = new NativeArray<int>(length, Allocator.TempJob);

        var verts = new NativeList<float3>(Allocator.TempJob);
        var tris = new NativeList<int>(Allocator.TempJob);
        var uvs = new NativeList<int2>(Allocator.TempJob);

        JobHandle handle;

        if (useParallel)
        {
            var job = new BuildJobBurstParallel()
            {
                dimension = adjDimension,
                position = position,
                groundHeight = terrainSettings.groundHeight,
                terrainHeight = terrainSettings.terrainHeight,
                map = map,
                Settings = terrainSettings,
            };
            handle = job.Schedule(length, batchSize);
        }
        else
        {
            var job = new BuildJobBurst()
            {
                dimension = adjDimension,
                position = position,
                groundHeight = terrainSettings.groundHeight,
                terrainHeight = terrainSettings.terrainHeight,
                map = map,
                Settings = terrainSettings,
            };
            handle = job.Schedule();
        }

        var job2 = new BuildMeshJob()
        {
            chunkHeight = ChunkHeight,
            chunkWidth = ChunkWidth,
            map = map,
#if injectData
                            VertexOffset = vertexOffset,
                            EdgeDirection = EdgeDirection,
                            EdgeConnection = edgeConnection,
                            CubeEdgeFlags = CubeEdgeFlags,
                            TriangleConnectionTable = TriangleConnectionTable,
#endif
            tris = tris,
            verts = verts,
            uvs = uvs,
        };
        handle = job2.Schedule(handle);
        handle.Complete();
        chunk.map = map.ToArray();
        map.Dispose();

        Vector3[] vertsV3 = new Vector3[verts.Length];
        var x = 0;
        foreach (var vertex in verts)
        {
            vertsV3[x++] = new Vector3(vertex.x, vertex.y, vertex.z);
        }

        Color32[] colors = new Color32[uvs.Length];
        Color32 lastColor = new Color32();
        x = 0;
        foreach (var uv in uvs)
        {
            Color32 c = error;
            switch (uv.x)
            {
                case -1:
                {
                    c = lastColor;
                    break;
                }
                case 0:
                {
                    c = surface;
                    break;
                }
                case 1:
                {
                    c = earth;
                    break;
                }
                case 2:
                {
                    c = mountain;
                    break;
                }
            }

            lastColor = c;
            colors[x++] = c;
        }

        var trisArr = tris.ToArray();
        chunk.SetMesh(vertsV3, trisArr, colors);
        verts.Dispose();
        tris.Dispose();
        uvs.Dispose();
        chunks[position] = chunk;
    }
}