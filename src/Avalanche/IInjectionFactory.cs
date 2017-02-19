using System;
using StructureMap;

namespace Avalanche
{
    public interface IInjectionFactory<T>
    {
        T Create();
        void Destroy(T item);
    }

    public class InjectionFactory<T> : IInjectionFactory<T>
    {
        private readonly IContainer _container;

        public InjectionFactory(IContainer container)
        {
            _container = container;
        }

        public T Create()
        {
            return _container.GetInstance<T>();
        }

        public void Destroy(T item)
        {
            _container.Release(item);
        }
    }

    public static class InjectionFactoryExtensions
    {
        public static InjectionFactoryObjectWrapper<T> CreateWrapper<T>(this IInjectionFactory<T> factory)
        {
            var item = factory.Create();
            return new InjectionFactoryObjectWrapper<T>(item, factory);
        }
    }

    public class InjectionFactoryObjectWrapper<T> : IDisposable
    {
        private readonly IInjectionFactory<T> _factory;
        public readonly T Item;

        public InjectionFactoryObjectWrapper(T item, IInjectionFactory<T> factory)
        {
            Item = item;
            _factory = factory;
        }

        void IDisposable.Dispose()
        {
            _factory.Destroy(Item);
        }
    }
}