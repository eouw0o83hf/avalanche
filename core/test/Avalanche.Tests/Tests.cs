using System;
using Xunit;

namespace Avalanche.Tests
{
    public class Tests
    {
        [Fact]
        public void Test1() 
        {
            var obj = new AvalancheService();
            Assert.True(obj != null);
        }
    }
}
