using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
    public struct BuildJobBurstParallel : IJobParallelFor
    {
        public int3 position;
        public int3 dimension;
        public int groundHeight;
        public int terrainHeight;
        public NativeArray<float> map;
        public TerrainSettings Settings;

        public void Execute(int index)
        {
            var posFromIndex = Chunk.GetPosForIndex(index, dimension);
            var pos = new int3();
            pos.x = posFromIndex.x + position.x;
            pos.y = posFromIndex.y + position.y;
            pos.z = posFromIndex.z + position.z;

            var y = posFromIndex.y;

            int voxelValue;
            var dim2D = new int2(dimension.x, dimension.z);
            var solidGround = 2;
            var ratioNoise = Noise.Perlin2D(new int2(pos.x, 1), Settings.ratioNoise.scale,
                Settings.ratioNoise.offset, new int2(1234, 1));


            var noise2D = Noise.Perlin2D(new int2(pos.x, pos.z), Settings.surfaceNoise.scale,
                Settings.surfaceNoise.offset, dim2D);
            var terrain = Mathf.CeilToInt(noise2D * terrainHeight);
            var surfaceHeight = terrain + groundHeight;
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
            
            map[index] = voxelValue;
        }
    }
