#define meshjob
// #define injectData

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class Timing
{
    public int amount;
    public List<TimeSpan> spans = new List<TimeSpan>();
    public double min;
    public double max;
    public double avg;

    public void Add(TimeSpan span)
    {
        amount++;
        spans.Add(span);
        min = spans.Min().TotalMilliseconds;
        max = spans.Max().TotalMilliseconds;
        avg = new TimeSpan((long) spans.Select(ts => ts.Ticks).Average()).TotalMilliseconds;
    }
}

[Serializable]
public class TimingContainer
{
    public Timing noJobs = new Timing();
    public Timing noBurst = new Timing();
    public Timing burst = new Timing();
    public Timing parallel1 = new Timing();
    public Timing parallel8 = new Timing();
    public Timing parallel16 = new Timing();
    public Timing parallel32 = new Timing();
    public Timing parallel64 = new Timing();
    public Timing parallel128 = new Timing();
}

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

public class WorldGenerator : MonoBehaviour
{
    [Serializable]
    public struct ChunkSettings
    {
        public int ChunkWidth;
        public int ChunkHeight;
        public int groundHeight;
        public int terrainHeight;
        public NoiseSettings surfaceNoise;
        public Threshold surfaceThreshold;
        public NoiseSettings ratioNoise;
        [Range(0, 1)] public float threshold;
        public CaveSettings caveSettings;
        [Serializable]
        public struct CaveSettings
        {
            public Threshold caveThreshold;
            public NoiseSettings caveNoise;
            public Threshold entranceThreshold;
            public NoiseSettings entranceNoise;
            public int relativeCaveHeight;
        }
        [Serializable]
        public struct NoiseSettings
        {
            public float scale;
            public float offset;

            public NoiseSettings(float scale, float offset)
            {
                this.scale = scale;
                this.offset = offset;
            }
        }
        [Serializable]
        public struct Threshold
        {
            [Range(-1, 1)] public float min;
            [Range(-1, 1)] public float max;

            public bool IsWithinThreshold(float value)
            {
                return (min < 0 || value >= min) && (max < 0 || value <= max);
            }
            
            public bool IsWithinThresholdSqr(float value)
            {
                
                return (min < 0 || value >= min * min) && (max < 0 || value <= max * max);
            }
        }

        // public ChunkSettings()
        // {
        //     ChunkWidth = 5;
        //     ChunkHeight = 256;
        //     scale = 1;
        //     scale2 = 1;
        //     offset = 1;
        //     offset2 = 1;
        //     groundHeight = 10;
        //     threshold = 0.45f;
        //     terrainHeight = 5;
        //     caveThreshold = 3;
        //     relativeCaveHeight = 5;
        //     caveEntranceThreshold = 0.5f;
        // }
    }

    public bool renderMesh;
    public int worldSize = 3;
    public ChunkSettings chunkSettings = new ChunkSettings();
    public int testSize = 10;
    public List<int> batchSizes = new List<int>();
    public int batchSize = 1;

    private List<Chunk> chunks = new List<Chunk>();
    public bool useBurst;
    public bool useJobs;
    public bool useParallel;
    public Meshing meshing;
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
        timings.noBurst = new Timing();
        timings.noJobs = new Timing();
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

        useBurst = true;
        useParallel = false;
        for (int i = 0; i < testSize; i++)
        {
            ClearChunks();
            BuildWithJobs();
            yield return null;
        }

        //
        // useParallel = true;
        // foreach (var b in batchSizes)
        // {
        //     batchSize = b;
        //     for (int i = 0; i < testSize; i++)
        //     {
        //         ClearChunks();
        //         BuildWithJobs();
        //         yield return null;
        //     }
        // }
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
        GenerateWorld();
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
            Destroy(chunk.gameObject);
        }

        chunks.Clear();
    }

    public float GetVoxelFromWorldPos(Vector3 pos)
    {
        if (pos.y >= chunkSettings.ChunkHeight || pos.x == -1 || pos.z == -1)
        {
            return 1;
        }

        var chunkX = Mathf.FloorToInt(pos.x / chunkSettings.ChunkWidth);
        var chunkZ = Mathf.FloorToInt(pos.z / chunkSettings.ChunkWidth);
        var chunkIndex = chunkZ + chunkX * worldSize;
        if (chunkIndex >= chunks.Count)
        {
            return 1;
        }

        var chunk = chunks[chunkIndex];
        var voxelX = Mathf.FloorToInt(pos.x % chunkSettings.ChunkWidth);
        var voxelZ = Mathf.FloorToInt(pos.z % chunkSettings.ChunkWidth);
        var index = Chunk.GetIndexForPos(new int3(voxelX, (int) pos.y, voxelZ),
            new int3(chunkSettings.ChunkWidth, chunkSettings.ChunkHeight, chunkSettings.ChunkWidth));
        return chunk.map[index];
    }

    private void BuildWithJobs()
    {
        Stopwatch watch = new Stopwatch();
        watch.Start();
        var dimension = new int3(chunkSettings.ChunkWidth, chunkSettings.ChunkHeight, chunkSettings.ChunkWidth);
        var index = 0;

        NativeArray<JobHandle> handles = new NativeArray<JobHandle>(worldSize * worldSize, Allocator.Temp);
        List<NativeArray<float>> maps = new List<NativeArray<float>>();

#if meshjob
        List<NativeList<int>> triangleList = new List<NativeList<int>>();
        List<NativeList<float3>> verticesList = new List<NativeList<float3>>();

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
#endif

        var start = Mathf.FloorToInt(worldSize / 2f);
        var end = Mathf.CeilToInt(worldSize / 2f);
        for (int x = -start; x < end; x++)
        {
            for (int z = -start; z < end; z++)
            {
                var go = new GameObject();
                go.transform.SetParent(transform);
                var pos = new Vector3(x * chunkSettings.ChunkWidth, 0, z * chunkSettings.ChunkWidth);
                var posInt = new int3(pos);
                go.transform.position = pos;
                var chunk = go.AddComponent<Chunk>();
                chunk.ChunkWidth = chunkSettings.ChunkWidth;
                chunk.ChunkHeight = chunkSettings.ChunkHeight;
                chunk.threshold = chunkSettings.threshold;
                chunk.dimension = dimension;
                chunk.position = pos;
                chunk.world = this;
                var adjDimension = dimension;
                adjDimension.x++;
                adjDimension.z++;
                var length = adjDimension.x * adjDimension.y * adjDimension.z;
                var map = new NativeArray<float>(length, Allocator.TempJob);
#if meshjob
                // var triangles = new NativeArray<int>(15000, Allocator.TempJob);
                // var vertices = new NativeArray<float3>(15000, Allocator.TempJob);
                var verts = new NativeList<float3>(Allocator.TempJob);
                var tris = new NativeList<int>(Allocator.TempJob);
#endif

                JobHandle handle;
                if (useBurst)
                {
                    if (useParallel)
                    {
                        var job = new Chunk.BuildJobBurstParallel()
                        {
                            dimension = adjDimension,
                            // scale = chunkSettings.scale,
                            // scale2 = chunkSettings.scale2,
                            // offset = chunkSettings.offset,
                            // offset2 = chunkSettings.offset2,
                            position = posInt,
                            groundHeight = chunkSettings.groundHeight,
                            terrainHeight = chunkSettings.terrainHeight,
                            map = map,
                            threshold = chunkSettings.threshold,
                            // caveThreshold = chunkSettings.caveThreshold,
                        };
                        handle = job.Schedule(length, batchSize);
                    }
                    else
                    {
                        var job = new Chunk.BuildJobBurst()
                        {
                            dimension = adjDimension,
                            position = posInt,
                            groundHeight = chunkSettings.groundHeight,
                            terrainHeight = chunkSettings.terrainHeight,
                            map = map,
                            // threshold = chunkSettings.threshold,
                            // caveThreshold = chunkSettings.caveThreshold,
                            // relativeCaveHeight = chunkSettings.relativeCaveHeight,
                            // caveEntranceThreshold = chunkSettings.caveEntranceThreshold,
                            Settings =  chunkSettings,
                        };
                        handle = job.Schedule();
#if meshjob
                        var job2 = new Chunk.MeshJob()
                        {
                            chunkHeight = chunkSettings.ChunkHeight,
                            chunkWidth = chunkSettings.ChunkWidth,
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
                            // triangles = triangles,
                            // vertices = vertices,
                            threshold = chunkSettings.threshold
                        };
                        handle = job2.Schedule(handle);
#endif
                    }
                }
                else
                {
                    var job = new Chunk.BuildJob()
                    {
                        dimension = dimension,
                        // scale = chunkSettings.scale,
                        // offset = chunkSettings.offset,
                        // offset2 = chunkSettings.offset2,
                        groundHeight = chunkSettings.groundHeight,
                        terrainHeight = chunkSettings.terrainHeight,
                        position = posInt,
                        map = map
                    };
                    handle = job.Schedule();
                }
#if meshjob
                // triangleList.Add(triangles);
                // verticesList.Add(vertices);
                triangleList.Add(tris);
                verticesList.Add(verts);
#endif

                maps.Add(map);
                chunks.Add(chunk);
                handles[index++] = handle;
            }
        }

        // Debug.Log(watch.Elapsed.ToString());

        JobHandle.CompleteAll(handles);
        var fl3 = new float3(-1, -1, -1);
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].map = maps[i].ToArray();
            maps[i].Dispose();

#if meshjob
            // var verts = verticesList[i].ToArray();
            // var tris = triangleList[i].ToArray();
            // tris = tris.Where(t => t != -1).ToArray();
            // var v3Verts = verts.Where(v => (v != fl3).x).Select(v => new Vector3(v.x, v.y, v.z)).ToArray();
            Vector3[] verts = new Vector3[verticesList[i].Length];
            var x = 0;
            foreach (var vertex in verticesList[i])
            {
                verts[x++] = new Vector3(vertex.x, vertex.y, vertex.z);
            }

            // var verts = verticesList[i].Select(v => new Vector3(v.x, v.y, v.z)).ToArray();
            var tris = triangleList[i].ToArray();
            chunks[i].SetMesh(verts, tris);
            verticesList[i].Dispose();
            triangleList[i].Dispose();
#endif
        }

#if !meshjob
        if (renderMesh)
        {
            foreach (var chunk in chunks)
            {
                chunk.BuildMesh();
            }
        }
#endif

        handles.Dispose();
        watch.Stop();
        if (useBurst)
        {
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
        }
        else
        {
            timings.noBurst.Add(watch.Elapsed);
        }

        foreach (var chunk in chunks)
        {
            var mf = chunk.gameObject.GetComponent<MeshFilter>();
            meshing.Add(mf.mesh.triangles.Length, mf.mesh.vertexCount);
        }

        // Debug.Log(watch.Elapsed.ToString());
    }

    private void BuildWithoutJobs()
    {
        Stopwatch watch = new Stopwatch();
        watch.Start();
        var dimension = new int3(chunkSettings.ChunkWidth, chunkSettings.ChunkHeight, chunkSettings.ChunkWidth);
        var index = 0;

        for (int x = 0; x < worldSize; x++)
        {
            for (int z = 0; z < worldSize; z++)
            {
                var go = new GameObject();
                var pos = new Vector3(x * chunkSettings.ChunkWidth, 0, z * chunkSettings.ChunkWidth);
                var posInt = new int3(pos);
                go.transform.position = pos;
                var chunk = go.AddComponent<Chunk>();
                chunk.ChunkWidth = chunkSettings.ChunkWidth;
                chunk.ChunkHeight = chunkSettings.ChunkHeight;
                chunk.threshold = chunkSettings.threshold;
                chunk.position = pos;


                // chunk.scale = chunkSettings.scale;
                // chunk.offset2 = chunkSettings.offset2;
                // chunk.offset = chunkSettings.offset;
                chunk.Build(posInt, dimension);

                chunks.Add(chunk);
                index++;
            }
        }

        watch.Stop();
        timings.noJobs.Add(watch.Elapsed);
    }

    private void GenerateWorld()
    {
        if (useJobs)
        {
            BuildWithJobs();
        }
        else
        {
            BuildWithoutJobs();
        }
    }
}