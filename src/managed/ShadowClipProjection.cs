using System;

namespace BigWalkDLSS;

// Double-precision composition of the actual receiver and atlas transforms.
// The native adapter supplies row-major matrices; no Unity calls belong here.
internal static class ShadowClipProjection
{
    internal static void IncludeNeighbourFilters(ReadOnlySpan<ShadowScissor> source, int resolution, int guard,
        Span<ShadowScissor> destination)
    {
        if (source.Length != 4 || destination.Length != 4 || resolution < 1 || resolution > 16384 || guard < 0 || guard > resolution)
            throw new ArgumentException("Expected four valid cascade rectangles and a bounded filter guard.");
        Span<ShadowScissor> original = stackalloc ShadowScissor[4];
        for (int i = 0; i < 4; i++)
        {
            var rect = source[i];
            original[i] = rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0 ||
                (long)rect.X + rect.Width > resolution || (long)rect.Y + rect.Height > resolution
                ? new ShadowScissor(0, 0, resolution, resolution) : rect;
        }
        for (int target = 0; target < 4; target++)
        {
            int dx = target % 2 * resolution, dy = target / 2 * resolution;
            var own = original[target];
            int left = own.X, bottom = own.Y, right = own.X + own.Width, top = own.Y + own.Height;
            for (int i = 0; i < 4; i++)
            {
                if (i == target) continue;
                var other = original[i];
                int sx = i % 2 * resolution, sy = i / 2 * resolution;
                int l = Math.Max(0, sx + other.X - guard - dx), b = Math.Max(0, sy + other.Y - guard - dy);
                int r = Math.Min(resolution, sx + other.X + other.Width + guard - dx), t = Math.Min(resolution, sy + other.Y + other.Height + guard - dy);
                if (l < r && b < t) { left = Math.Min(left, l); bottom = Math.Min(bottom, b); right = Math.Max(right, r); top = Math.Max(top, t); }
            }
            destination[target] = new ShadowScissor(left, bottom, right - left, top - bottom);
        }
    }

    internal static bool TryBuild(ReadOnlySpan<double> cameraClipFromWorld, ReadOnlySpan<double> worldToAtlas,
        int atlasWidth, int atlasHeight, int offsetX, int offsetY, int resolution,
        Span<double> cameraClipFromTile)
    {
        if (cameraClipFromWorld.Length != 16 || worldToAtlas.Length != 16 || cameraClipFromTile.Length != 16 ||
            atlasWidth < 1 || atlasHeight < 1 || resolution < 1 || offsetX < 0 || offsetY < 0 ||
            (long)offsetX + resolution > atlasWidth || (long)offsetY + resolution > atlasHeight) return false;
        foreach (double value in cameraClipFromWorld) if (!double.IsFinite(value)) return false;
        foreach (double value in worldToAtlas) if (!double.IsFinite(value)) return false;
        // Directional maps must be affine. Avoid treating a perspective map as a cube.
        if (Math.Abs(worldToAtlas[12]) > 1e-10 || Math.Abs(worldToAtlas[13]) > 1e-10 ||
            Math.Abs(worldToAtlas[14]) > 1e-10 || Math.Abs(worldToAtlas[15] - 1) > 1e-6) return false;
        double a = worldToAtlas[0], b = worldToAtlas[1], c = worldToAtlas[2];
        double d = worldToAtlas[4], e = worldToAtlas[5], f = worldToAtlas[6];
        double g = worldToAtlas[8], h = worldToAtlas[9], i = worldToAtlas[10];
        double determinant = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        double scale = Math.Max(Math.Max(Math.Abs(a) + Math.Abs(b) + Math.Abs(c), Math.Abs(d) + Math.Abs(e) + Math.Abs(f)),
            Math.Abs(g) + Math.Abs(h) + Math.Abs(i));
        if (!(scale > 0) || Math.Abs(determinant) < 1e-8 * scale * scale * scale) return false;
        Span<double> inverse = stackalloc double[16];
        inverse.Clear();
        inverse[0] = (e * i - f * h) / determinant; inverse[1] = (c * h - b * i) / determinant; inverse[2] = (b * f - c * e) / determinant;
        inverse[4] = (f * g - d * i) / determinant; inverse[5] = (a * i - c * g) / determinant; inverse[6] = (c * d - a * f) / determinant;
        inverse[8] = (d * h - e * g) / determinant; inverse[9] = (b * g - a * h) / determinant; inverse[10] = (a * e - b * d) / determinant;
        inverse[15] = 1;
        for (int row = 0; row < 3; row++)
            inverse[row * 4 + 3] = -(inverse[row * 4] * worldToAtlas[3] + inverse[row * 4 + 1] * worldToAtlas[7] + inverse[row * 4 + 2] * worldToAtlas[11]);
        // Compose local tile UV/depth -> atlas UV/depth -> world.
        for (int row = 0; row < 4; row++)
        {
            inverse[row * 4 + 3] += inverse[row * 4] * offsetX / atlasWidth + inverse[row * 4 + 1] * offsetY / atlasHeight;
            inverse[row * 4] *= (double)resolution / atlasWidth;
            inverse[row * 4 + 1] *= (double)resolution / atlasHeight;
        }
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
            {
                double value = 0;
                for (int k = 0; k < 4; k++) value += cameraClipFromWorld[row * 4 + k] * inverse[k * 4 + column];
                if (!double.IsFinite(value)) return false;
                cameraClipFromTile[row * 4 + column] = value;
            }
        return true;
    }
}
