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
}