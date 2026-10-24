using BigWalkDLSS;
using System;
using System.Linq;
using System.Numerics;

static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
static double[] Rows(Matrix4x4 matrix) => new double[] {matrix.M11,matrix.M12,matrix.M13,matrix.M14,matrix.M21,matrix.M22,matrix.M23,matrix.M24,
    matrix.M31,matrix.M32,matrix.M33,matrix.M34,matrix.M41,matrix.M42,matrix.M43,matrix.M44};
static double[] Mul(double[] m, double[] p) => Enumerable.Range(0,4).Select(r=>Enumerable.Range(0,4).Sum(c=>m[r*4+c]*p[c])).ToArray();
try
{
    var rng=new Random(5683);
    // Independent reference: build invertible world-from-atlas transforms using
    // Numerics, then compare a point travelling through both complete routes.
    for(int run=0;run<200;run++)
    {
        var transform=Matrix4x4.CreateScale(.2f+(float)rng.NextDouble(),.2f+(float)rng.NextDouble(),.2f+(float)rng.NextDouble()) *
            Matrix4x4.CreateRotationY((float)rng.NextDouble()) * Matrix4x4.CreateTranslation(20,-5,7);
        Matrix4x4.Invert(transform,out var inverse);
        double[] worldFromAtlas=Rows(Matrix4x4.Transpose(transform)), worldToAtlas=Rows(Matrix4x4.Transpose(inverse));
        double[] camera=Rows(Matrix4x4.Transpose(Matrix4x4.CreateRotationX(.2f)*Matrix4x4.CreateTranslation(1,2,-3)));
        for(int tile=0;tile<4;tile++)
        {
            var result=new double[16];
            Check(ShadowClipProjection.TryBuild(camera,worldToAtlas,16384,16384,tile%2*8192,tile/2*8192,8192,result),"Affine composition rejected");
            for(int point=0;point<20;point++)
            {
                var p=new[]{rng.NextDouble(),rng.NextDouble(),rng.NextDouble(),1.0};
                var atlas=new[]{p[0]*.5+tile%2*.5,p[1]*.5+tile/2*.5,p[2],1};
                var expected=Mul(camera,Mul(worldFromAtlas,atlas));var actual=Mul(result,p);
                Check(expected.Zip(actual).All(pair=>Math.Abs(pair.First-pair.Second)<.0001),"World/atlas/tile composition changed a point");
            }
        }
    }
    var identity=Rows(Matrix4x4.Identity);var output=new double[16];
    var invalid=(double[])identity.Clone();invalid[0]=double.NaN;
    Check(!ShadowClipProjection.TryBuild(identity,invalid,16384,16384,0,0,8192,output),"Nonfinite accepted");
    invalid=(double[])identity.Clone();invalid[0]=0;
    Check(!ShadowClipProjection.TryBuild(identity,invalid,16384,16384,0,0,8192,output),"Singular accepted");
    invalid=(double[])identity.Clone();invalid[12]=.1;
    Check(!ShadowClipProjection.TryBuild(identity,invalid,16384,16384,0,0,8192,output),"Perspective shadow map accepted");
    Check(!ShadowClipProjection.TryBuild(identity,identity,16384,16384,16383,0,8192,output),"Out-of-atlas tile accepted");
    // Check neighboring-tile dependencies against the global filter footprint,
    // including corners, without using the helper's union arithmetic as oracle.
    const int size=256,guard=16;
    for(int run=0;run<100;run++)
    {
        var core=Enumerable.Range(0,4).Select(_=>new ShadowScissor(rng.Next(0,128),rng.Next(0,128),128,128)).ToArray();
        core[run%4]=new ShadowScissor(0,0,256,256);
        // TryCompute already expands each receiver region by the filter guard
        // and clips it to its own tile. Neighbour union recovers the spill.
        var source=core.Select(r=>{int l=Math.Max(0,r.X-guard),b=Math.Max(0,r.Y-guard),right=Math.Min(size,r.X+r.Width+guard),top=Math.Min(size,r.Y+r.Height+guard);return new ShadowScissor(l,b,right-l,top-b);}).ToArray();
        var result=new ShadowScissor[4];ShadowClipProjection.IncludeNeighbourFilters(source,size,guard,result);
        for(int index=0;index<4;index++)
        for(int sample=0;sample<100;sample++)
        {
            var rect=core[index];double x=index%2*size+rect.X+rng.NextDouble()*rect.Width;
            double y=index/2*size+rect.Y+rng.NextDouble()*rect.Height;
            foreach(int u in new[]{-guard,0,guard})foreach(int v in new[]{-guard,0,guard})
            {
                double px=x+u,py=y+v;if(px<0||py<0||px>=size*2||py>=size*2)continue;
                int target=(int)(px/size)+(int)(py/size)*2;var r=result[target];
                Check(px-target%2*size>=r.X&&px-target%2*size<=r.X+r.Width&&py-target/2*size>=r.Y&&py-target/2*size<=r.Y+r.Height,"Cross-tile filter sample omitted");
            }
        }
    }
    Console.WriteLine("Shadow projection checks passed: 16,000 independent transform points, invalid inputs and cross-tile filter footprints.");
    return 0;
}
catch(Exception error){Console.Error.WriteLine("FAIL "+error);return 1;}
