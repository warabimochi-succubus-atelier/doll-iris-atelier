using DollIrisGenerator.Controls;
using Xunit;

namespace DollIrisGenerator.Tests
{
    public sealed class ScrollChainingTests
    {
        private const int WheelUp = 120;
        private const int WheelDown = -120;

        [Fact]
        public void TestListWithNothingToScrollPassesBothDirections()
        {
            Assert.True(ScrollChaining.ShouldPassToParent(WheelUp, 0, 0));
            Assert.True(ScrollChaining.ShouldPassToParent(WheelDown, 0, 0));
        }

        [Fact]
        public void TestListInTheMiddleKeepsTheWheel()
        {
            Assert.False(ScrollChaining.ShouldPassToParent(WheelUp, 5, 10));
            Assert.False(ScrollChaining.ShouldPassToParent(WheelDown, 5, 10));
        }

        [Fact]
        public void TestListAtAnEndPassesOnlyOutwards()
        {
            // At the top: scrolling up goes to the page, scrolling down still moves the list
            Assert.True(ScrollChaining.ShouldPassToParent(WheelUp, 0, 10));
            Assert.False(ScrollChaining.ShouldPassToParent(WheelDown, 0, 10));

            // At the bottom: the other way round
            Assert.True(ScrollChaining.ShouldPassToParent(WheelDown, 10, 10));
            Assert.False(ScrollChaining.ShouldPassToParent(WheelUp, 10, 10));
        }

        [Fact]
        public void TestNoWheelMovementIsNotPassed()
        {
            Assert.False(ScrollChaining.ShouldPassToParent(0, 0, 0));
        }

        [Fact]
        public void TestAMouseWheelNotchScrollsAsBefore()
        {
            // WPF's own step: 3 lines x 16 px per notch
            Assert.Equal(48.0, ProportionalWheelScrolling.ScrollPixels(WheelDown, 3, 500));
            Assert.Equal(-48.0, ProportionalWheelScrolling.ScrollPixels(WheelUp, 3, 500));
        }

        [Fact]
        public void TestTouchpadEventsScrollInProportion()
        {
            // A two-finger swipe arrives as many small events: ten events of 12 add up to one notch, not ten
            double total = 0;
            for (int i = 0; i < 10; i++) total += ProportionalWheelScrolling.ScrollPixels(-12, 3, 500);
            Assert.Equal(48.0, total, 9);
            Assert.Equal(-4.8, ProportionalWheelScrolling.ScrollPixels(12, 3, 500), 9);
        }

        [Fact]
        public void TestOneScreenAtATimeSettingScrollsAViewportPerNotch()
        {
            // Windows' 「一度に 1 画面ずつ」 (WheelScrollLines = -1)
            Assert.Equal(500.0, ProportionalWheelScrolling.ScrollPixels(WheelDown, -1, 500));
            Assert.Equal(-250.0, ProportionalWheelScrolling.ScrollPixels(60, -1, 500));
        }
    }
}
