using System.Globalization;
using System.Windows.Data;
using DollIrisGenerator.Converters;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>
    /// 「透明度」 sliders show 1 − opacity (0% opaque, 100% invisible), like the browser version; the design keeps the
    /// opacity, so saved files are unchanged.
    /// </summary>
    public class TransparencySliderTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [Theory]
        [InlineData(1.0, 0.0)]
        [InlineData(0.0, 1.0)]
        [InlineData(0.85, 0.15)]
        public void SliderShowsTransparency(double opacity, double transparency)
        {
            var c = new OpacityToTransparencyConverter();
            Assert.Equal(transparency, (double)c.Convert(opacity, typeof(double), null!, Inv), 9);
        }

        [Theory]
        [InlineData(1.0, 0.0)]      // slider at 100% → invisible
        [InlineData(0.0, 1.0)]      // slider at 0% → opaque
        [InlineData(0.3333333, 0.6667)] // rounded to 4 decimals, like the browser version
        public void MovingTheSliderStoresTheOpacity(double transparency, double opacity)
        {
            var c = new OpacityToTransparencyConverter();
            Assert.Equal(opacity, (double)c.ConvertBack(transparency, typeof(double), null!, Inv));
        }

        [Fact]
        public void LabelShowsTransparencyInPercent()
        {
            var c = new TransparencyPercentConverter();
            Assert.Equal("15%", c.Convert(0.85, typeof(string), null!, Inv));
            Assert.Equal("100%", c.Convert(0.0, typeof(string), null!, Inv));
            Assert.Equal("0%", c.Convert(1.0, typeof(string), null!, Inv));
            Assert.Equal(Binding.DoNothing, new OpacityToTransparencyConverter().ConvertBack("x", typeof(double), null!, Inv));
        }
    }
}
