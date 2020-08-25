using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

    [BurstCompile]
    public struct BuildJobBurst : IJob
    {
        public int3 position;
        public int3 dimension;
        public int groundHeight;
        public int terrainHeight;
        public NativeArray<int> map;
        public TerrainSettings Settings;

        public void Execute()
        {
            int voxelValue;
            var pos = new int3();
            var dim2D = new int2(dimension.x, dimension.z);
            var solidGround = 2;
            
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
                                               (y / 2f * Noise.Perlin2D(new int2(pos.x, pos.z), 0.5f,
                                                   345.345f,
                                                   new int2(dimension.x, dimension.z)));
                        //surface + below
                        if (y <= surfaceHeight)
                        {
                            var caveNoise = Noise.Perlin3D(pos, Settings.caveSettings.caveNoise.scale,
                                Settings.caveSettings.caveNoise.offset, dimension);
                            var isCave =
                                Settings.caveSettings.caveThreshold.IsWithinThresholdSqr(caveNoise * caveNoise);
                            // solid ground at the bottom
                            if (y <= solidGround)
                            {
                                voxelValue = 0;
                            }
                            //caves
                            else if (y < surfaceHeight + Settings.caveSettings.relativeCaveHeight)
                            {
                                var caveFloorHeight = solidGround + 5;
                                //is a cave
                                if (isCave)
                                {
                                    //cave floor
                                    if (y <= caveFloorHeight)
                                    {
                                        var n1 = Noise.Perlin2D(new int2(pos.x, pos.z), 0.5f, 690823.234f,
                                            dim2D);
                                        var n2 = Noise.Perlin2D(new int2(pos.x, pos.z), 3f, 690823.234f,
                                            dim2D);
                                        var n = n1 * ratioNoise + (1 - ratioNoise) * n2;
                                        voxelValue = y <= caveFloorHeight * 2 * n
                                            ? 0
                                            : 1;
                                    }
                                    //actual cave
                                    else
                                    {
                                        voxelValue = 1;
                                    }
                                }
                                //solid ground
                                else
                                {
                                    voxelValue = 0;
                                }
                            }
                            //cave entrance
                            else if (isCave && Settings.caveSettings.entranceThreshold.IsWithinThreshold(entranceNoise))
                            {
                                voxelValue = 1;
                            }
                            //solid surface
                            else
                            {
                                voxelValue = 0;
                            }
                        }
                        //above surface
                        //TODO separate noise 
                        else if (Settings.surfaceThreshold.IsWithinThreshold(surfaceNoise) &&
                                 y <= surfaceHeight + maxSurfaceHeight)
                        {
                            voxelValue = 0;
                        }
                        //air 
                        else
                        {
                            voxelValue = 1;
                        }

                        var index = Chunk.GetIndexForPos(x, y, z, dimension);
                        map[index] = voxelValue;
                    }
                }
            }
        }
    }
