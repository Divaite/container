using System;
using System.Diagnostics;
using System.Reflection;
using Unity.Policy;
using Unity.Registration;
using Unity.Resolution;
using Unity.Storage;

namespace Unity.Builder
{
    /// <summary>
    /// Represents the context in which a build-up or tear-down operation runs.
    /// </summary>
    [DebuggerDisplay("Resolving: {Registration.Type},  Name: {Registration.Name}")]
    public class BuilderContext : IResolveContext
    {
        #region Fields

        private readonly ResolverOverride[] _resolverOverrides;
        private readonly IPolicyList _list;

        #endregion

        #region Constructors

        public BuilderContext(UnityContainer container, InternalRegistration registration,
                              object existing, params ResolverOverride[] resolverOverrides)
        {
            Existing = existing;
            Lifetime = container._lifetimeContainer; 
            Registration = registration;
            Type = registration.Type;
            Name = registration.Name;

            _list = new PolicyList();
            if (null != resolverOverrides && 0 < resolverOverrides.Length)
                _resolverOverrides = resolverOverrides;
        }

        public BuilderContext(BuilderContext original, object existing)
        {
            Existing = existing;
            Lifetime = original.Lifetime;
            Registration = original.Registration;
            Type = original.Type;
            Name = original.Name;
            ParentContext = original;

            _list = original._list;
        }

        public BuilderContext(BuilderContext original, InternalRegistration registration)
        {
            Existing = null;
            Lifetime = original.Lifetime;
            Registration = registration;
            Type = Registration.Type;
            Name = Registration.Name;
            ParentContext = original;

            _list = original._list;
            _resolverOverrides = original._resolverOverrides;
        }

        #endregion


        #region INamedType

        public Type Type { get; set; }

        public string Name { get; }

        #endregion


        #region IResolveContext

        public IUnityContainer Container => Lifetime.Container;

        public object Existing { get; set; }

        public object Resolve(Type type, string name)
        {
            var registration = (InternalRegistration)((UnityContainer)Container).GetRegistration(type, name);
            var context = new BuilderContext(this, registration);

            ChildContext = context;

            var result = registration.BuildChain.ExecuteReThrowingPlan(ref context);

            ChildContext = null;

            return result;
        }

        public object Resolve(PropertyInfo property, string name, object value)
        {
            var context = this;

            // Process overrides if any
            if (null != _resolverOverrides)
            {
                // Check for property overrides
                for (var index = _resolverOverrides.Length - 1; index >= 0; --index)
                {
                    var resolverOverride = _resolverOverrides[index];

                    // Check if this parameter is overridden
                    if (resolverOverride is IEquatable<PropertyInfo> comparer && comparer.Equals(property))
                    {
                        // Check if itself is a value 
                        if (resolverOverride is IResolve resolverPolicy)
                        {
                            return resolverPolicy.Resolve(ref context);
                        }

                        // Try to create value
                        var resolveDelegate = resolverOverride.GetResolver<BuilderContext>(property.PropertyType);
                        if (null != resolveDelegate)
                        {
                            return resolveDelegate(ref context);
                        }
                    }
                }
            }

            // Resolve from injectors
            switch (value)
            {
                case PropertyInfo info when ReferenceEquals(info, property):
                    return Resolve(property.PropertyType, name);

                case ResolveDelegate<BuilderContext> resolver:
                    return resolver(ref context);

                case IResolve policy:
                    return policy.Resolve(ref context);

                case IResolverFactory factory:
                    var method = factory.GetResolver<BuilderContext>(Type);
                    return method?.Invoke(ref context);

                case object obj:
                    return obj;
            }

            // Resolve from container
            return Resolve(property.PropertyType, name);
        }

        public object Resolve(ParameterInfo parameter, string name, object value)
        {
            var context = this;

            // Process overrides if any
            if (null != _resolverOverrides)
            {
                // Check if this parameter is overridden
                for (var index = _resolverOverrides.Length - 1; index >= 0; --index)
                {
                    var resolverOverride = _resolverOverrides[index];

                    // If matches with current parameter
                    if (resolverOverride is IEquatable<ParameterInfo> comparer && comparer.Equals(parameter))
                    {
                        // Check if itself is a value 
                        if (resolverOverride is IResolve resolverPolicy)
                        {
                            return resolverPolicy.Resolve(ref context);
                        }

                        // Try to create value
                        var resolveDelegate = resolverOverride.GetResolver<BuilderContext>(parameter.ParameterType);
                        if (null != resolveDelegate)
                        {
                            return resolveDelegate(ref context);
                        }
                    }
                }
            }

            // Resolve from injectors
            // TODO: Optimize via overrides
            switch (value)
            {
                case ResolveDelegate<BuilderContext> resolver:
                    return resolver(ref context);

                case IResolve policy:
                    return policy.Resolve(ref context);

                case IResolverFactory factory:
                    var method = factory.GetResolver<BuilderContext>(Type);
                    return method?.Invoke(ref context);

                case Type type:     // TODO: Requires evaluation
                    if (typeof(Type) == parameter.ParameterType) return type;
                    break;

                case object obj:
                    return obj;
            }

            // Resolve from container
            return Resolve(parameter.ParameterType, name);
        }

        #endregion


        #region Public Members

        public readonly ILifetimeContainer Lifetime;

        public readonly INamedType Registration;

        public SynchronizedLifetimeManager RequiresRecovery;

        public bool BuildComplete;

        public BuilderContext ChildContext;

        public readonly BuilderContext ParentContext;

        #endregion


        #region  Policies

        public object Get(Type type, string name, Type policyInterface)
        {
            return _list.Get(type, name, policyInterface) ?? 
                   (type != Registration.Type || name != Registration.Name
                       ? ((UnityContainer) Container).GetPolicy(type, name, policyInterface)
                       : ((IPolicySet)Registration).Get(policyInterface));
        }

        public void Set(Type type, string name, Type policyInterface, object policy)
        {
            _list.Set(type, name, policyInterface, policy);
        }

        public void Clear(Type type, string name, Type policyInterface)
        {
            _list.Clear(type, name, policyInterface);
        }

        #endregion
    }
}