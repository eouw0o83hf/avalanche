using System;
using NSubstitute;
using StructureMap;
using Xunit;

namespace Avalanche.Tests
{
    public class InjectionFactoryTests
    {
        private readonly IContainer _container;

        private readonly InjectionFactory<object> _sut;
        
        public InjectionFactoryTests()
        {
            _container = Substitute.For<IContainer>();
            _sut = new InjectionFactory<object>(_container);
        }
        
        [Fact]
        public void GivenCreatedObject_WhenDestroyed_ReleasesInContainer()
        {
            _container.GetInstance<object>().Returns(new object());

            var item = _sut.Create();
            _sut.Destroy(item);

            _container.ReceivedWithAnyArgs(1).Release(Arg.Any<object>());
        } 
    }
}