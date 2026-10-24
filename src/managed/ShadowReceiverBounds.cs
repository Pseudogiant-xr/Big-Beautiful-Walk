using System;

namespace BigWalkDLSS;

internal readonly record struct ShadowScissor(int X, int Y, int Width, int Height);

// Tile coordinates are UV/depth, not atlas coordinates. No projection, UV, or resolution changes.
internal static class ShadowReceiverBounds
{
    private const double Epsilon = 2.2204460492503131e-16;
    private const double PlanePadding = 1e-12;
    private const double MinimumDeterminant = 1e-10;

    // False means use the full tile; it never authorizes skipping a shadow pass.
    // Invalid resolution has no usable full tile and produces a zero-sized sentinel.
    internal static bool TryCompute(ReadOnlySpan<double> cameraClipFromTile, int resolution,
        int guardPixels, out ShadowScissor bounds)
    {
        bounds = new ShadowScissor(0, 0, Math.Max(0, resolution), Math.Max(0, resolution));
        if (resolution <= 0 || guardPixels < 0 || guardPixels > resolution || cameraClipFromTile.Length != 16)
            return false;

        double scale = 0;
        for (int i = 0; i < 16; i++)
        {
            if (!double.IsFinite(cameraClipFromTile[i])) return false;
            // Depth is deliberately absent: reversed Z and arbitrary finite depth remaps are equivalent.
            if (i < 8 || i >= 12) scale = Math.Max(scale, Math.Abs(cameraClipFromTile[i]));
        }
        if (scale == 0) return false;

        Span<double> rows = stackalloc double[12];
        for (int i = 0; i < 4; i++)
        {
            rows[i] = cameraClipFromTile[i] / scale;
            rows[4 + i] = cameraClipFromTile[4 + i] / scale;
            rows[8 + i] = cameraClipFromTile[12 + i] / scale;
        }
        double rank = 0;
        for (int omitted = 0; omitted < 4; omitted++)
        {
            int a = omitted == 0 ? 1 : 0;
            int b = omitted <= 1 ? 2 : 1;
            int c = omitted <= 2 ? 3 : 2;
            double minor = rows[a] * (rows[4 + b] * rows[8 + c] - rows[4 + c] * rows[8 + b])
                - rows[b] * (rows[4 + a] * rows[8 + c] - rows[4 + c] * rows[8 + a])
                + rows[c] * (rows[4 + a] * rows[8 + b] - rows[4 + b] * rows[8 + a]);
            rank = Math.Max(rank, Math.Abs(minor));
        }
        double row0 = 0, row1 = 0, row3 = 0;
        for (int i = 0; i < 4; i++)
        {
            row0 = Math.Max(row0, Math.Abs(rows[i]));
            row1 = Math.Max(row1, Math.Abs(rows[4 + i]));
            row3 = Math.Max(row3, Math.Abs(rows[8 + i]));
        }
        double rankScale = row0 * row1 * row3;
        if (rankScale == 0 || rank <= MinimumDeterminant * rankScale) return false;

        Span<Plane> planes = stackalloc Plane[10];
        planes[0] = new(1, 0, 0, 0); planes[1] = new(-1, 0, 0, 1);
        planes[2] = new(0, 1, 0, 0); planes[3] = new(0, -1, 0, 1);
        planes[4] = new(0, 0, 1, 0); planes[5] = new(0, 0, -1, 1);
        for (int i = 0; i < 4; i++)
        {
            int row = i < 2 ? 0 : 4;
            double sign = (i & 1) == 0 ? 1 : -1;
            double a = rows[8] + sign * rows[row];
            double b = rows[9] + sign * rows[row + 1];
            double c = rows[10] + sign * rows[row + 2];
            double d = rows[11] + sign * rows[row + 3];
            double normal = Math.Max(Math.Abs(a), Math.Max(Math.Abs(b), Math.Abs(c)));
            // Cancellation/constant planes cannot certify a useful cone.
            if (normal <= PlanePadding) return false;
            double divisor = Math.Max(normal, Math.Abs(d));
            // Inflate before normalization, covering row addition and cancellation error on the unit cube.
            planes[6 + i] = new(a / divisor, b / divisor, c / divisor, (d + PlanePadding) / divisor);
        }

        double minX = 1, minY = 1, minZ = 1, maxX = 0, maxY = 0, maxZ = 0;
        int vertices = 0;
        for (int i = 0; i < 8; i++)
        for (int j = i + 1; j < 9; j++)
        for (int k = j + 1; k < 10; k++)
        {
            Plane a = planes[i], b = planes[j], c = planes[k];
            if (Parallel(a, b) || Parallel(a, c) || Parallel(b, c)) continue;
            double bcX = b.B * c.C - b.C * c.B;
            double bcY = b.C * c.A - b.A * c.C;
            double bcZ = b.A * c.B - b.B * c.A;
            double caX = c.B * a.C - c.C * a.B;
            double caY = c.C * a.A - c.A * a.C;
            double caZ = c.A * a.B - c.B * a.A;
            double abX = a.B * b.C - a.C * b.B;
            double abY = a.C * b.A - a.A * b.C;
            double abZ = a.A * b.B - a.B * b.A;
            double determinant = a.A * bcX + a.B * bcY + a.C * bcZ;
            // A zero permanent proves structural dependence without a cancellation-sensitive predicate.
            double permanent = Math.Abs(a.A) * (Math.Abs(b.B * c.C) + Math.Abs(b.C * c.B))
                + Math.Abs(a.B) * (Math.Abs(b.C * c.A) + Math.Abs(b.A * c.C))
                + Math.Abs(a.C) * (Math.Abs(b.A * c.B) + Math.Abs(b.B * c.A));
            if (permanent == 0)
            {
                if (ZeroProduct(a.A, b.B, c.C) && ZeroProduct(a.A, b.C, c.B)
                    && ZeroProduct(a.B, b.C, c.A) && ZeroProduct(a.B, b.A, c.C)
                    && ZeroProduct(a.C, b.A, c.B) && ZeroProduct(a.C, b.B, c.A)) continue;
                return false; // Products underflowed; this is not a proof of parallelism.
            }
            double nx = -a.D * bcX - b.D * caX - c.D * abX;
            double ny = -a.D * bcY - b.D * caY - c.D * abY;
            double nz = -a.D * bcZ - b.D * caZ - c.D * abZ;
            if (Math.Abs(determinant) <= MinimumDeterminant)
            {
                // A nearly parallel triple is harmless if its equalities cannot meet inside the cube.
                // Cramer's identity det*x = numerator holds even when det is zero; bound both sides
                // without division. This handles upright cameras' dependent side-plane triples.
                double px = Math.Abs(a.D) * (Math.Abs(b.B * c.C) + Math.Abs(b.C * c.B))
                    + Math.Abs(b.D) * (Math.Abs(c.B * a.C) + Math.Abs(c.C * a.B))
                    + Math.Abs(c.D) * (Math.Abs(a.B * b.C) + Math.Abs(a.C * b.B));
                double py = Math.Abs(a.D) * (Math.Abs(b.C * c.A) + Math.Abs(b.A * c.C))
                    + Math.Abs(b.D) * (Math.Abs(c.C * a.A) + Math.Abs(c.A * a.C))
                    + Math.Abs(c.D) * (Math.Abs(a.C * b.A) + Math.Abs(a.A * b.C));
                double pz = Math.Abs(a.D) * (Math.Abs(b.A * c.B) + Math.Abs(b.B * c.A))
                    + Math.Abs(b.D) * (Math.Abs(c.A * a.B) + Math.Abs(c.B * a.A))
                    + Math.Abs(c.D) * (Math.Abs(a.A * b.B) + Math.Abs(a.B * b.A));
                if (OutsideUnitInterval(nx, px, determinant, permanent)
                    || OutsideUnitInterval(ny, py, determinant, permanent)
                    || OutsideUnitInterval(nz, pz, determinant, permanent)) continue;
                return false;
            }

            double x = nx / determinant, y = ny / determinant, z = nz / determinant;
            // Normals and offsets are bounded by ~1. This deliberately loose forward-error envelope
            // covers the determinant/Cramer arithmetic, including cancellation in a coordinate.
            double error = 256 * Epsilon * (1 + Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))))
                / Math.Abs(determinant);
            if (!double.IsFinite(error) || error > 1e-5) return false;
            bool inside = true;
            for (int p = 0; p < 10; p++)
            {
                Plane plane = planes[p];
                double value = plane.A * x + plane.B * y + plane.C * z + plane.D;
                double tolerance = error * (Math.Abs(plane.A) + Math.Abs(plane.B) + Math.Abs(plane.C))
                    + 16 * Epsilon * (Math.Abs(plane.A * x) + Math.Abs(plane.B * y)
                        + Math.Abs(plane.C * z) + Math.Abs(plane.D));
                if (value < -tolerance) { inside = false; break; }
            }
            if (!inside) continue;
            vertices++;
            minX = Math.Min(minX, x - error); maxX = Math.Max(maxX, x + error);
            minY = Math.Min(minY, y - error); maxY = Math.Max(maxY, y + error);
            minZ = Math.Min(minZ, z - error); maxZ = Math.Max(maxZ, z + error);
        }

        // Empty or almost flat intersections are uncertainty, never an empty-scissor optimization.
        if (vertices < 4 || maxX - minX <= 1e-9 || maxY - minY <= 1e-9 || maxZ - minZ <= 1e-9)
            return false;
        int left = Lower(minX, resolution, guardPixels), bottom = Lower(minY, resolution, guardPixels);
        int right = Upper(maxX, resolution, guardPixels), top = Upper(maxY, resolution, guardPixels);
        if (right <= left || top <= bottom || (left == 0 && bottom == 0 && right == resolution && top == resolution))
            return false;
        bounds = new ShadowScissor(left, bottom, right - left, top - bottom);
        return true;
    }

    private static int Lower(double value, int resolution, int guard) =>
        (int)Math.Max(0, Math.Floor(Math.Clamp(value, 0, 1) * resolution) - guard);

    private static int Upper(double value, int resolution, int guard) =>
        (int)Math.Min(resolution, Math.Ceiling(Math.Clamp(value, 0, 1) * resolution) + guard);

    private static bool OutsideUnitInterval(double numerator, double numeratorPermanent,
        double determinant, double determinantPermanent)
    {
        double error = 64 * Epsilon * (numeratorPermanent + determinantPermanent) + 64 * double.Epsilon;
        return numerator < Math.Min(0, determinant) - error || numerator > Math.Max(0, determinant) + error;
    }

    private static bool ZeroProduct(double a, double b, double c) => a == 0 || b == 0 || c == 0;

    // Exact equal/opposite normalized normals occur for cube and orthographic face pairs.
    private static bool Parallel(Plane a, Plane b) =>
        (a.A == b.A && a.B == b.B && a.C == b.C) || (a.A == -b.A && a.B == -b.B && a.C == -b.C);

    private readonly record struct Plane(double A, double B, double C, double D);
}
