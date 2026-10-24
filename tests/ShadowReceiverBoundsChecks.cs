using System;
using BigWalkDLSS;

internal static class Program
{
    private const int Resolution = 8192;
    private const int Guard = 16;
    private static int _checks;
    private static int _covered;

    private static int Main()
    {
        try
        {
            KnownRectangles();
            InvalidAndUncertain();
            DepthAndScaleInvariance();
            RandomReceiverCoverage();
            AllocationCheck();
            Console.WriteLine($"Shadow receiver bounds checks passed: {_checks} assertions; {_covered} independent receiver/filter samples; no managed hot-path allocations.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new Exception(message);
    }

    private static double[] Orthographic(double minX, double maxX, double minY, double maxY) => new[]
    {
        2 / (maxX - minX), 0, 0, -(maxX + minX) / (maxX - minX),
        0, 2 / (maxY - minY), 0, -(maxY + minY) / (maxY - minY),
        0, 0, 1, 0,
        0, 0, 0, 1
    };

    private static void Rectangle(double[] matrix, double minX, double maxX, double minY, double maxY, int guard)
    {
        Check(ShadowReceiverBounds.TryCompute(matrix, Resolution, guard, out var rect), "Known narrow cone fell back");
        Check(rect.X <= Math.Max(0, minX * Resolution - guard)
            && rect.Y <= Math.Max(0, minY * Resolution - guard)
            && rect.X + rect.Width >= Math.Min(Resolution, maxX * Resolution + guard)
            && rect.Y + rect.Height >= Math.Min(Resolution, maxY * Resolution + guard), "Analytic rectangle was underbounded");
        Check(rect.X >= Math.Max(0, Math.Floor(minX * Resolution) - guard - 1)
            && rect.Y >= Math.Max(0, Math.Floor(minY * Resolution) - guard - 1)
            && rect.X + rect.Width <= Math.Min(Resolution, Math.Ceiling(maxX * Resolution) + guard + 1)
            && rect.Y + rect.Height <= Math.Min(Resolution, Math.Ceiling(maxY * Resolution) + guard + 1), "Known rectangle lost useful tightness");
        foreach (double x in new[] { minX, maxX })
        foreach (double y in new[] { minY, maxY }) Cover(rect, new V(x, y, .5), Resolution, guard);
    }

    private static void KnownRectangles()
    {
        Rectangle(Orthographic(.4, .6, .3, .7), .4, .6, .3, .7, Guard);
        Rectangle(Orthographic(.40125, .61375, .2125, .7375), .40125, .61375, .2125, .7375, 0);
        Rectangle(Orthographic(0, .25, .75, 1), 0, .25, .75, 1, Guard);
        Rectangle(Orthographic(-.1, .125, .2, .4), 0, .125, .2, .4, Guard);
        // Apex (.5,.5,-1), x half-width (z+1)/10, y half-width (z+1)/20.
        var perspective = new double[] { 10, 0, 0, -5, 0, 20, 0, -10, 0, 0, 1, 0, 0, 0, 1, 1 };
        Rectangle(perspective, .3, .7, .4, .6, Guard);
        // Cone apex inside the cube: the negative-w half must not enlarge or truncate the forward cone.
        perspective[15] = -.5;
        Rectangle(perspective, .45, .55, .475, .525, Guard);
        // Tile depth can shear both camera axes; this is separate from the camera's clip-depth row.
        var sheared = new double[] { 10, 0, 2, -6, 0, 20, -3, -8.5, 0, 0, -1, 1, 0, 0, 1, 1 };
        Rectangle(sheared, .2, .7, .375, .675, Guard);
        Rectangle(TileDepthAffine(sheared, .5, .25), .275, .675, .4, .625, Guard);
        Rectangle(TileDepthAffine(sheared, -1, 1), .2, .7, .375, .675, Guard);
        // A thin valid region needs rounding even with no caller guard.
        Rectangle(Orthographic(.5, .5 + 1e-5, .25, .75), .5, .5 + 1e-5, .25, .75, 0);
    }

    private static void Fallback(double[] matrix, string reason, int resolution = Resolution, int guard = Guard)
    {
        bool computed = ShadowReceiverBounds.TryCompute(matrix, resolution, guard, out var rect);
        Check(!computed && rect == new ShadowScissor(0, 0, Math.Max(0, resolution), Math.Max(0, resolution)), reason);
    }

    private static void InvalidAndUncertain()
    {
        var narrow = Orthographic(.4, .6, .3, .7);
        Fallback(Array.Empty<double>(), "Empty matrix");
        Fallback(new double[15], "Short matrix");
        Fallback(new double[17], "Long matrix");
        Fallback(new double[16], "Zero matrix");
        Fallback(narrow, "Zero resolution", 0);
        Fallback(narrow, "Negative resolution", -7);
        Fallback(narrow, "Negative guard", guard: -1);
        Fallback(narrow, "Oversized guard", guard: Resolution + 1);
        Fallback(narrow, "Guard consumes tile", guard: Resolution);
        Fallback(narrow, "Single pixel has no smaller scissor", resolution: 1, guard: 0);
        Fallback(Orthographic(-1, 2, -1, 2), "All-visible cube");
        Fallback(Orthographic(2, 3, .2, .8), "Disjoint cone must use full map");
        Fallback(Orthographic(1, 2, .2, .8), "Face-only contact is degenerate");
        Fallback(Orthographic(1, 2, 1, 2), "Edge-only contact is degenerate");
        for (int i = 0; i < 16; i++)
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var matrix = (double[])narrow.Clone(); matrix[i] = invalid;
            Fallback(matrix, $"Nonfinite entry {i}");
        }
        var repeated = (double[])narrow.Clone();
        Array.Copy(repeated, 0, repeated, 4, 4);
        Fallback(repeated, "Rank-deficient side mapping");
        var singular = (double[])narrow.Clone();
        singular[4] = singular[0]; singular[5] = 1e-13; singular[7] = singular[3];
        Fallback(singular, "Nearly singular side mapping");
        var nearlyParallel = Orthographic(0, .2, .3, .7);
        nearlyParallel[1] = 5e-10; nearlyParallel[3] = -1 - 2.5e-10;
        Fallback(nearlyParallel, "Near-parallel triple must fall back");
        // Subnormal normalization must not overflow or fabricate an empty map.
        for (int i = 0; i < 16; i++) singular[i] = narrow[i] * double.Epsilon;
        ShadowReceiverBounds.TryCompute(singular, Resolution, Guard, out var subnormal);
        Check(subnormal.Width > 0 && subnormal.Height > 0, "Subnormal input produced an empty scissor");
        Check(ShadowReceiverBounds.TryCompute(narrow, int.MaxValue, 16, out var huge)
            && huge.X >= 0 && huge.Width > 0 && (long)huge.X + huge.Width <= int.MaxValue, "Pixel arithmetic overflow");
    }

    private static void DepthAndScaleInvariance()
    {
        var original = Orthographic(.4, .6, .3, .7);
        Check(ShadowReceiverBounds.TryCompute(original, Resolution, Guard, out var expected), "Baseline failed");
        foreach (double[] depth in new[]
        {
            new[] { 0d, 0, -1, 1 }, new[] { 7d, -31, 0, 1000 },
            new[] { 0d, 0, 0, 0 }, new[] { 1e300, -1e300, 1e300, -1e300 }
        })
        {
            var matrix = (double[])original.Clone(); Array.Copy(depth, 0, matrix, 8, 4);
            Check(ShadowReceiverBounds.TryCompute(matrix, Resolution, Guard, out var actual) && actual == expected,
                "Finite reversed-Z/depth-affine row changed side-cone bounds");
        }
        foreach (double scale in new[] { 1e-250, 1e-50, 1d, 1e50, 1e250 })
        {
            var matrix = (double[])original.Clone();
            for (int i = 0; i < 16; i++) matrix[i] *= scale;
            Check(ShadowReceiverBounds.TryCompute(matrix, Resolution, Guard, out var actual) && actual == expected,
                "Positive homogeneous scaling changed bounds");
        }
        original[11] = -1e200; // Receivers on both sides of any camera near/far interval remain included.
        Check(ShadowReceiverBounds.TryCompute(original, Resolution, Guard, out var noDepth) && noDepth == expected,
            "Camera near/far was applied");
        var sheared = new double[] { 10, 0, 2, -6, 0, 20, -3, -8.5, 0, 0, -1, 1, 0, 0, 1, 1 };
        Check(ShadowReceiverBounds.TryCompute(sheared, Resolution, Guard, out var forward)
            && ShadowReceiverBounds.TryCompute(TileDepthAffine(sheared, -1, 1), Resolution, Guard, out var reversed)
            && forward == reversed, "Reversing tile depth changed the receiver set");
    }

    private static double[] TileDepthAffine(double[] original, double scale, double offset)
    {
        var matrix = (double[])original.Clone();
        for (int row = 0; row < 16; row += 4)
        {
            matrix[row + 2] = original[row + 2] * scale;
            matrix[row + 3] = original[row + 3] + original[row + 2] * offset;
        }
        return matrix;
    }

    private static void RandomReceiverCoverage()
    {
        var random = new Random(20261024);
        int useful = 0, receivers = 0, clippedReceivers = 0;
        for (int camera = 0; camera < 180; camera++)
        {
            var center = new V(.5, .5, .5);
            V eye = center + Unit(new V(random.NextDouble() - .5, random.NextDouble() - .5, random.NextDouble() - .5))
                * (.3 + random.NextDouble() * 2.7);
            V forward = Unit(center - eye);
            V right = Unit(Cross(forward, Math.Abs(forward.Y) < .8 ? new V(0, 1, 0) : new V(1, 0, 0)));
            V up = Cross(right, forward);
            double fx = 3 + random.NextDouble() * 12, fy = 3 + random.NextDouble() * 12;
            var matrix = new double[16];
            PutRow(matrix, 0, right * fx, eye); PutRow(matrix, 4, up * fy, eye);
            PutRow(matrix, 12, forward, eye);
            matrix[10] = camera % 2 == 0 ? 1 : -1; matrix[11] = camera % 3 == 0 ? 1e8 : -1e8;
            bool clipped = ShadowReceiverBounds.TryCompute(matrix, Resolution, Guard, out var rect);
            if (clipped) useful++;
            Check(rect.Width > 0 && rect.Height > 0 && rect.X >= 0 && rect.Y >= 0
                && rect.X + rect.Width <= Resolution && rect.Y + rect.Height <= Resolution, "Invalid random output rectangle");
            // Independent oracle: construct camera rays, then intersect their parametric segments with
            // the cube using slab intervals. It neither forms halfspaces nor enumerates plane triples.
            for (int ray = 0; ray < 400; ray++)
            {
                double sx = ray < 9 ? ray % 3 - 1 : random.NextDouble() * 2 - 1;
                double sy = ray < 9 ? ray / 3 - 1 : random.NextDouble() * 2 - 1;
                V direction = forward + right * (sx / fx) + up * (sy / fy);
                if (!CubeSegment(eye, direction, out double enter, out double exit)) continue;
                foreach (double t in new[] { enter, exit, enter + (exit - enter) * random.NextDouble() })
                {
                    V point = eye + direction * t;
                    Cover(rect, point, Resolution, Guard); receivers++;
                    if (clipped) clippedReceivers++;
                }
            }
            // Also sample tile points and classify them directly in the camera basis.
            for (int sample = 0; sample < 300; sample++)
            {
                V point = new(random.NextDouble(), random.NextDouble(), random.NextDouble());
                V relative = point - eye; double depth = Dot(relative, forward);
                if (depth < 0 || Math.Abs(Dot(relative, right) * fx) > depth || Math.Abs(Dot(relative, up) * fy) > depth) continue;
                Cover(rect, point, Resolution, Guard); receivers++;
                if (clipped) clippedReceivers++;
            }
        }
        Check(useful >= 100, $"Coverage tests mostly exercised fallback: {useful}/180 useful scissors");
        Check(receivers > 100000, $"Too few independent receivers: {receivers}");
        Check(clippedReceivers > 50000, $"Too few receivers tested against a reduced scissor: {clippedReceivers}");
        Console.WriteLine($"Random coverage: {useful}/180 useful scissors, {receivers} receivers ({clippedReceivers} using reduced scissors).");
    }

    private static void Cover(ShadowScissor rect, V point, int resolution, int guard)
    {
        foreach (int dx in new[] { -guard, 0, guard })
        foreach (int dy in new[] { -guard, 0, guard })
        {
            double u = Math.Clamp(point.X, 0, 1) + (double)dx / resolution;
            double v = Math.Clamp(point.Y, 0, 1) + (double)dy / resolution;
            int x = (int)Math.Clamp(Math.Floor(u * resolution), 0, resolution - 1);
            int y = (int)Math.Clamp(Math.Floor(v * resolution), 0, resolution - 1);
            Check(x >= rect.X && x < rect.X + rect.Width && y >= rect.Y && y < rect.Y + rect.Height,
                $"Receiver/filter texel ({x},{y}) outside {rect}, point {point}, guard {guard}");
            _covered++;
        }
    }

    private static bool CubeSegment(V origin, V direction, out double enter, out double exit)
    {
        enter = 0; exit = double.PositiveInfinity;
        return Slab(origin.X, direction.X, ref enter, ref exit)
            && Slab(origin.Y, direction.Y, ref enter, ref exit)
            && Slab(origin.Z, direction.Z, ref enter, ref exit) && exit >= enter;
    }

    private static bool Slab(double origin, double direction, ref double enter, ref double exit)
    {
        if (Math.Abs(direction) < 1e-15) return origin >= 0 && origin <= 1;
        double a = -origin / direction, b = (1 - origin) / direction;
        enter = Math.Max(enter, Math.Min(a, b)); exit = Math.Min(exit, Math.Max(a, b));
        return exit >= enter;
    }

    private static void AllocationCheck()
    {
        var matrix = Orthographic(.4, .6, .3, .7);
        for (int i = 0; i < 100; i++) ShadowReceiverBounds.TryCompute(matrix, Resolution, Guard, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) ShadowReceiverBounds.TryCompute(matrix, Resolution, Guard, out _);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "Geometry helper allocated managed memory");
    }

    private static void PutRow(double[] matrix, int offset, V basis, V eye)
    {
        matrix[offset] = basis.X; matrix[offset + 1] = basis.Y; matrix[offset + 2] = basis.Z;
        matrix[offset + 3] = -Dot(basis, eye);
    }

    private static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static V Unit(V a) => a * (1 / Math.Sqrt(Dot(a, a)));
    private static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private readonly record struct V(double X, double Y, double Z)
    {
        public static V operator +(V a, V b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V operator -(V a, V b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V operator *(V a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    }
}
