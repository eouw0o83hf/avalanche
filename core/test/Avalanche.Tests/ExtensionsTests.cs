using System;
using Xunit;

namespace Avalanche.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void ParseGuid_Success() 
        {
            var guid = Guid.NewGuid();
            var asString = guid.ToString();
            var parsed = asString.ParseGuid();
            Assert.NotNull(parsed);
            Assert.Equal(guid, parsed.Value);
        }

        [Fact]
        public void ParseGuid_Null() 
        {
            var parsed = ((string)null).ParseGuid();
            Assert.Null(parsed);
        }

        [Fact]
        public void ParseGuid_Invalid() 
        {
            var parsed = "this is not a guid".ParseGuid();
            Assert.Null(parsed);
        }

        private enum SampleEnum
        {
            First,
            Second
        }

        [Fact]
        public void ParseEnum_Success() 
        {
            var val = SampleEnum.First;
            var asString = val.ToString();
            var parsed = asString.ParseEnum<SampleEnum>();
            Assert.NotNull(parsed);
            Assert.Equal(val, parsed.Value);
        }

        [Fact]
        public void ParseEnum_Null() 
        {
            var parsed = ((string)null).ParseEnum<SampleEnum>();
            Assert.Null(parsed);
        }

        [Fact]
        public void ParseEnum_Invalid() 
        {
            var parsed = "this is not an enum value".ParseEnum<SampleEnum>();
            Assert.Null(parsed);
        }
    }
}
