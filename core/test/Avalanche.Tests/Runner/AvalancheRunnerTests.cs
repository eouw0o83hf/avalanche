using System;
using System.Linq;
using Avalanche.Glacier;
using Avalanche.Lightroom;
using Avalanche.State;
using Avalanche.Runner;
using Microsoft.Framework.Logging;
using Xunit;
using NSubstitute;

namespace Avalanche.Tests.Runner
{
    public class AvalancheRunnerTests
    {
        private readonly ILogger<AvalancheRunner> _logger;
        private readonly IGlacierGateway _glacier;
        private readonly ILightroomReader _lightroom;
        private readonly IAvalancheRepository _avalanche;
        private readonly ExecutionParameters _parameters;

        private readonly AvalancheRunner _sut;

        public AvalancheRunnerTests()
        {
            _logger = Substitute.For<ILogger<AvalancheRunner>>();
            _glacier = Substitute.For<IGlacierGateway>();
            _lightroom = Substitute.For<ILightroomReader>();
            _avalanche = Substitute.For<IAvalancheRepository>();
            _parameters = new ExecutionParameters
            {
                Glacier = new GlacierParameters
                {
                },
                Avalanche = new AvalancheParameters
                {
                }
            };

            _sut = new AvalancheRunner(_logger, _glacier, _lightroom, _avalanche, _parameters);
        }

        [Fact]
        public void WireupSeemsToWork()
        {

        }
    }
}