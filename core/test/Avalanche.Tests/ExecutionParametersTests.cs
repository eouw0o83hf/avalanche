using System;
using System.Linq;
using Xunit;

namespace Avalanche.Tests
{
    public class ExecutionParametersTests
    {
        [Fact]
        public void ValidationErrors_RollUp()
        {
            var parameters = new ExecutionParameters
            {
                Avalanche = new AvalancheParameters
                {
                },
                Glacier = new GlacierParameters
                {
                }
            };

            var validation = parameters.GetValidationErrors();
            Assert.Equal(6, validation.Count());
        }
    }
}