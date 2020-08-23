using Unity.Mathematics;
using UnityEngine;

public static class Noise
{
    public static float Perlin2D(Vector2 pos, float scale, float offset, int2 dimension)
    {
        var x = ((pos.x  + offset + 0.001f)  / dimension.x) * scale;
        var y = ((pos.y + offset + 0.001f) / dimension.y) * scale;
        // Debug.Log($"X: {x} Y: {y}");
        return Mathf.PerlinNoise(x, y);
    }
    
    public static float Perlin2D(int2 pos, float scale, float offset, int2 dimension)
    {
        var x = ((pos.x  + offset + 0.001f)  / dimension.x) * scale;
        var y = ((pos.y + offset + 0.001f) / dimension.y) * scale;
        // Debug.Log($"X: {x} Y: {y}");
        return Mathf.PerlinNoise(x, y);
    }

    public static float Perlin3D(Vector3 pos, float scale, float offset, int3 dimension)
    {
        // var xRatio = 0.5f; //pos.x % dimension.x / dimension.x;
        // var yRatio = pos.y % dimension.y / dimension.y;
        // var zRatio = pos.z % dimension.z / dimension.z;
        var dimension2d = new int2(dimension.x, dimension.z);
        var xy1 = Perlin2D(new Vector2(pos.x, pos.y), scale, offset, dimension2d);
        // var xy2 = Perlin2D(new Vector2(pos.x, pos.y), scale2, offset2, dimension2d);
        // var xy = (xy1 * xRatio + xy2 * (1 - xRatio));

        var xz1 = Perlin2D(new Vector2(pos.x, pos.z), scale, offset, dimension2d);
        // var xz2 = Perlin2D(new Vector2(pos.x, pos.z), scale2, offset2, dimension2d);
        // var xz = (xz1 * xRatio + xz2 * (1 - xRatio));

        var yz1 = Perlin2D(new Vector2(pos.y, pos.z), scale, offset, dimension2d);
        // var yz2 = Perlin2D(new Vector2(pos.y, pos.z), scale2, offset2, dimension2d);
        // var yz = (yz1 * xRatio + yz2 * (1 - xRatio));

        var yx1 = Perlin2D(new Vector2(pos.y, pos.x), scale, offset, dimension2d);
        // var yx2 = Perlin2D(new Vector2(pos.y, pos.x), scale2, offset2, dimension2d);
        // var yx = (yx1 * xRatio + yx2 * (1 - xRatio));

        var zx1 = Perlin2D(new Vector2(pos.z, pos.x), scale, offset, dimension2d);
        // var zx2 = Perlin2D(new Vector2(pos.z, pos.x), scale2, offset2, dimension2d);
        // var zx = (zx1 * xRatio + zx2 * (1 - xRatio));

        var zy1 = Perlin2D(new Vector2(pos.z, pos.y), scale, offset, dimension2d);
        // var zy2 = Perlin2D(new Vector2(pos.z, pos.y), scale2, offset2, dimension2d);
        // var zy = (zy1 * xRatio + zy2 * (1 - xRatio));

        // var total = xy * 5 + xz * 5 + yz * 5 + yx + zx + zy;
        var total = xy1 * 5 + xz1 * 5 + yz1 * 5 + yx1 + zx1 + zy1;
        total /= 18;
        return total;
    }
    
    
    public static float Perlin3D(int3 pos, float scale, float offset,int3 dimension)
    {
        pos += new int3(1234,2345,3456);
        // var xRatio = 0.5f; //pos.x % dimension.x / dimension.x;
        // var yRatio = pos.y % dimension.y / dimension.y;
        // var zRatio = pos.z % dimension.z / dimension.z;
        var dimension2d = new int2(dimension.x, dimension.z);
        var xy1 = Perlin2D(new int2(pos.x, pos.y), scale, offset, dimension2d);
        // var xy2 = Perlin2D(new int2(pos.x, pos.y), scale2, offset2, dimension2d);
        // var xy = (xy1 * xRatio + xy2 * (1 - xRatio));

        var xz1 = Perlin2D(new int2(pos.x, pos.z), scale, offset, dimension2d);
        // var xz2 = Perlin2D(new int2(pos.x, pos.z), scale2, offset2, dimension2d);
        // var xz = (xz1 * xRatio + xz2 * (1 - xRatio));

        var yz1 = Perlin2D(new int2(pos.y, pos.z), scale, offset, dimension2d);
        // var yz2 = Perlin2D(new int2(pos.y, pos.z), scale2, offset2, dimension2d);
        // var yz = (yz1 * xRatio + yz2 * (1 - xRatio));

        var yx1 = Perlin2D(new int2(pos.y, pos.x), scale, offset, dimension2d);
        // var yx2 = Perlin2D(new int2(pos.y, pos.x), scale2, offset2, dimension2d);
        // var yx = (yx1 * xRatio + yx2 * (1 - xRatio));

        var zx1 = Perlin2D(new int2(pos.z, pos.x), scale, offset, dimension2d);
        // var zx2 = Perlin2D(new int2(pos.z, pos.x), scale2, offset2, dimension2d);
        // var zx = (zx1 * xRatio + zx2 * (1 - xRatio));

        var zy1 = Perlin2D(new int2(pos.z, pos.y), scale, offset, dimension2d);
        // var zy2 = Perlin2D(new int2(pos.z, pos.y), scale2, offset2, dimension2d);
        // var zy = (zy1 * xRatio + zy2 * (1 - xRatio));

        // var total = xy * 5 + xz * 5 + yz * 5 + yx + zx + zy;
        var total = xy1 * 5 + xz1 * 5 + yz1 * 5 + yx1 + zx1 + zy1;
        total /= 18;
        return total;
    }
}