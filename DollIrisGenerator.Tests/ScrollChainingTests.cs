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
    }
}
