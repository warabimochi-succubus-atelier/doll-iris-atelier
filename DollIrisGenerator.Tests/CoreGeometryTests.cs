using System;
using System.Linq;
using DollIrisGenerator.Services;
using CorePoint = DollIrisGenerator.Core.Geometry.Point;
using CoreRect = DollIrisGenerator.Core.Geometry.Rect;
using CoreVector = DollIrisGenerator.Core.Geometry.Vector;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;
using WpfVector = System.Windows.Vector;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// The design core (no WPF) has its own Point / Vector / Rect. They must compute exactly what the WPF types
    /// did (bit for bit), or outlines, patterns and gradients would shift from designs made with earlier versions.
    /// </summary>
    public class CoreGeometryTests
    {
        private static double[] Values()
        {
            var rng = new Random(20261108);
            var list = Enumerable.Range(0, 200).Select(_ => (rng.NextDouble() - 0.5) * Math.Pow(10, rng.Next(-6, 7))).ToList();
            list.AddRange(new[] { 0.0, -0.0, 1.0, -1.0, 1e-300, 1e300, 0.1, 1.0 / 3.0 });
            return list.ToArray();
        }

        private static void Same(double expected, double actual) =>
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

        [Fact]
        public void TestVectorArithmeticMatchesWpfBitForBit()
        {
            var v = Values();
            for (int i = 0; i + 3 < v.Length; i++)
            {
                var wa = new WpfVector(v[i], v[i + 1]);
                var wb = new WpfVector(v[i + 2], v[i + 3]);
                var ca = new CoreVector(v[i], v[i + 1]);
                var cb = new CoreVector(v[i + 2], v[i + 3]);
                double s = v[(i * 7) % v.Length];

                Same(wa.Length, ca.Length);
                Same(wa.LengthSquared, ca.LengthSquared);
                Same(WpfVector.Multiply(wa, wb), CoreVector.Multiply(ca, cb));
                Same(WpfVector.AngleBetween(wa, wb), CoreVector.AngleBetween(ca, cb));

                var wd = wa / s; var cd = ca / s;
                Same(wd.X, cd.X); Same(wd.Y, cd.Y);
                var wm = wa * s; var cm = ca * s;
                Same(wm.X, cm.X); Same(wm.Y, cm.Y);

                if (wa.X != 0 || wa.Y != 0)
                {
                    var wn = wa; wn.Normalize();
                    var cn = ca; cn.Normalize();
                    Same(wn.X, cn.X); Same(wn.Y, cn.Y);
                }

                var wp = new WpfPoint(v[i], v[i + 2]) + wb;
                var cp = new CorePoint(v[i], v[i + 2]) + cb;
                Same(wp.X, cp.X); Same(wp.Y, cp.Y);
                var wdiff = new WpfPoint(v[i], v[i + 1]) - new WpfPoint(v[i + 2], v[i + 3]);
                var cdiff = new CorePoint(v[i], v[i + 1]) - new CorePoint(v[i + 2], v[i + 3]);
                Same(wdiff.X, cdiff.X); Same(wdiff.Y, cdiff.Y);
            }
        }

        [Fact]
        public void TestRectMatchesWpf()
        {
            var w = new WpfRect(1.5, -2.25, 10, 4);
            var c = new CoreRect(1.5, -2.25, 10, 4);
            w.Offset(0.1, 0.2);
            c.Offset(0.1, 0.2);
            Assert.Equal(w, c.ToWpf());
            Assert.Equal(w.Right, c.Right);
            Assert.Equal(w.Bottom, c.Bottom);

            Assert.True(CoreRect.Empty.IsEmpty);
            Assert.Equal(WpfRect.Empty.X, CoreRect.Empty.X);
            Assert.Equal(WpfRect.Empty.Width, CoreRect.Empty.Width);
            Assert.Equal(WpfRect.Empty, CoreRect.Empty.ToWpf());
            Assert.Throws<InvalidOperationException>(() => { var e = CoreRect.Empty; e.Offset(1, 1); });
            Assert.Throws<ArgumentException>(() => new CoreRect(0, 0, -1, 1));
        }

        [Fact]
        public void TestCoreAssemblyDoesNotReferenceWpf()
        {
            var refs = typeof(CorePoint).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
            Assert.DoesNotContain("PresentationCore", refs);
            Assert.DoesNotContain("PresentationFramework", refs);
            Assert.DoesNotContain("WindowsBase", refs);
        }
    }
}
