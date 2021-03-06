﻿using BenchmarkDotNet.Attributes;
using Runner.Setup;
using System.Collections.Generic;
using Unity;
using Unity.Builder;

namespace Runner.Tests
{
    [BenchmarkCategory("Basic")]
    [Config(typeof(BenchmarkConfiguration))]
    public class Compiled
    {
        IUnityContainer _container;
        object _syncRoot = new object();

        [IterationSetup]
        public virtual void SetupContainer()
        {
            _container = new UnityContainer(Unity.UnityContainer.BuildStrategy.Compiled);

            _container.RegisterType<Poco>();
            _container.RegisterType<IFoo, Foo>();
            _container.RegisterType<IFoo, Foo>("1");
            _container.RegisterFactory<IFoo>("2", c => new Foo());
        }

        [Benchmark(Description = "Resolve<IUnityContainer>               ")]
        public object UnityContainer() => _container.Resolve(typeof(IUnityContainer), null);

        [Benchmark(Description = "Compiled<object> (unregistered)")]
        public object Unregistered() => _container.Resolve(typeof(object), null);

        [Benchmark(Description = "Compiled<Poco>   (registered)")]
        public object Transient() => _container.Resolve(typeof(Poco), null);

        [Benchmark(Description = "Compiled<IService>   (registered)")]
        public object Mapping() => _container.Resolve(typeof(IFoo), null);

        [Benchmark(Description = "Compiled<IService>      (legacy)")]
        public object LegacyFactory() => _container.Resolve(typeof(IFoo), "2");

        [Benchmark(Description = "Compiled<IService[]>   (registered)")]
        public object Array() => _container.Resolve(typeof(IFoo[]), null);

        [Benchmark(Description = "Compiled<IEnumerable<IService>>   (registered)")]
        public object Enumerable() => _container.Resolve(typeof(IEnumerable<IFoo>), null);
    }
}
