using System.Linq;
using DollIrisGenerator.Controls;
using Xunit;

namespace DollIrisGenerator.Tests
{
    public class TabRowsPanelTests
    {
        [Fact]
        public void TestShortTabsShareTheWidthAndLongTabsKeepTheirLabelWidth()
        {
            // Common width would be 100; the 150 label is wider, so the other two share 300 - 150
            double[] widths = TabRowsPanel.StretchWidths(new[] { 40.0, 150.0, 60.0 }, 300);

            Assert.Equal(new[] { 75.0, 150.0, 75.0 }, widths);
            Assert.Equal(300, widths.Sum(), 6);
        }

        [Fact]
        public void TestTabsAreSpreadEquallyWhenEveryLabelFits()
        {
            Assert.Equal(new[] { 100.0, 100.0, 100.0 }, TabRowsPanel.StretchWidths(new[] { 40.0, 90.0, 60.0 }, 300));
        }

        [Fact]
        public void TestLineThatIsAlreadyFullOrUnboundedIsUnchanged()
        {
            Assert.Equal(new[] { 200.0, 150.0 }, TabRowsPanel.StretchWidths(new[] { 200.0, 150.0 }, 300));
            Assert.Equal(new[] { 40.0, 60.0 }, TabRowsPanel.StretchWidths(new[] { 40.0, 60.0 }, double.PositiveInfinity));
        }
    }
}
