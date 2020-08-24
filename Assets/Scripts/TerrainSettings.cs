using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public struct TerrainSettings
{
    public int groundHeight;
    public int terrainHeight;
    public NoiseSettings surfaceNoise;
    public Threshold surfaceThreshold;

    public NoiseSettings ratioNoise;
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