namespace GetYourDeps;

public class DependencyManager : IDependencyManager, IDisposable
{
    public enum DependencyLifetime
    {
        Singleton,
        Instanced,
        ThreadLocal,
    }

    private class DependencyItem
    {
        private DependencyItem(Type dependencyType, DependencyManager manager,
            Func<IDependencyManager, object>? factory, DependencyLifetime lifetime) {
            DependencyType = dependencyType;
            Factory = factory;
            Lifetime = lifetime;
            _manager = manager;
            _threadLocalInstances = new(() => Factory!(_manager));
        }

        private DependencyItem(Type dependencyType, DependencyManager manager, object instance,
            DependencyLifetime lifetime) {
            DependencyType = dependencyType;
            _cachedInstance = instance;
            Lifetime = lifetime;
            _manager = manager;
            _threadLocalInstances = new(() => Factory!(_manager));
        }

        public static DependencyItem CreateSingleton<T>(DependencyManager manager, Func<IDependencyManager, T> factory)
            where T : class => new(typeof(T), manager, factory, DependencyLifetime.Singleton);

        public static DependencyItem CreateSingleton<T>(DependencyManager manager, T instance)
            where T : class => new(typeof(T), manager, instance, DependencyLifetime.Singleton);

        public static DependencyItem CreateInstanced<T>(DependencyManager manager, Func<IDependencyManager, T> factory)
            where T : class => new(typeof(T), manager, factory, DependencyLifetime.Instanced);

        public static DependencyItem CreateInstanced<T>(DependencyManager manager, T instance)
            where T : class, ICloneable => new(typeof(T), manager, instance, DependencyLifetime.Instanced);

        public static DependencyItem CreateThreadLocal<T>(DependencyManager manager,
            Func<IDependencyManager, T> factory)
            where T : class => new(typeof(T), manager, factory, DependencyLifetime.ThreadLocal);

        public static DependencyItem CreateThreadLocal<T>(DependencyManager manager, T instance)
            where T : class, ICloneable => new(typeof(T), manager, instance, DependencyLifetime.ThreadLocal);

        public static DependencyItem Create<T>(DependencyManager manager, DependencyLifetime lifetime,
            T? instance = null, Func<IDependencyManager, T>? factory = null) where T : class {
            return lifetime switch {
                DependencyLifetime.Singleton => instance is not null
                    ? CreateSingleton(manager, instance)
                    : CreateSingleton(manager,
                        factory ?? throw new InvalidOperationException(
                            "Either factory or instance must be set to nonnull value")),
                DependencyLifetime.Instanced => instance is not null
                    ? CreateInstanced(manager, instance as ICloneable
                                               ?? throw new InvalidOperationException(
                                                   "Instance must implement ICloneable"))
                    : CreateInstanced(manager,
                        factory ?? throw new InvalidOperationException(
                            "Either factory or instance must be set to nonnull value")),
                DependencyLifetime.ThreadLocal => instance is not null
                    ? CreateThreadLocal(manager, instance as ICloneable
                                                 ?? throw new InvalidOperationException(
                                                     "Instance must implement ICloneable"))
                    : CreateThreadLocal(manager,
                        factory ?? throw new InvalidOperationException(
                            "Either factory or instance must be set to nonnull value")),
                _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
            };
        }

        public Type DependencyType { get; }
        public Func<IDependencyManager, object>? Factory { get; }
        public DependencyLifetime Lifetime { get; }

        private DependencyManager _manager;
        private object? _cachedInstance;
        private readonly ThreadLocal<object> _threadLocalInstances;

        public object GetInstance() {
            return Lifetime switch {
                DependencyLifetime.Singleton => _cachedInstance ??= Factory!.Invoke(_manager),
                DependencyLifetime.Instanced => _cachedInstance is ICloneable cl
                    ? cl.Clone()
                    : Factory!.Invoke(_manager),
                DependencyLifetime.ThreadLocal => _threadLocalInstances.Value ??
                                                  throw new NullReferenceException(nameof(_threadLocalInstances.Value)),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
    }

    private readonly Dictionary<Type, DependencyItem> _dependencyItems = new();
    private readonly Mutex _lock = new();

    public IDependencyProvider IP => this;
    public IDependencyManager IM => this;

    /// <summary>
    /// Shorthand for <see cref="GetDependency{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T Get<T>() where T : class => GetDependency<T>();

    public T GetDependency<T>() where T : class {
        return TryGetDependency<T>() ?? throw new DependencyNotRegisteredException(typeof(T));
    }

    public T? TryGetDependency<T>() where T : class {
        return RunLockedFunction(() =>
            _dependencyItems.TryGetValue(typeof(T), out var dependency) ? (T)dependency.GetInstance() : null);
    }

    public IDependencyManager RegisterSingleton<T>(Func<IDependencyManager, T> factory, bool allowReregister = false)
        where T : class {
        Register(factory, DependencyLifetime.Singleton, allowReregister);
        return this;
    }

    public IDependencyManager RegisterInstanced<T>(Func<IDependencyManager, T> factory, bool allowReregister = false)
        where T : class {
        Register(factory, DependencyLifetime.Instanced, allowReregister);
        return this;
    }

    public IDependencyManager RegisterThreadLocal<T>(Func<IDependencyManager, T> factory, bool allowReregister = false)
        where T : class {
        Register(factory, DependencyLifetime.ThreadLocal, allowReregister);
        return this;
    }

    private void Register<T>(Func<IDependencyManager, T> factory, DependencyLifetime lifetime,
        bool allowReregister) where T : class {
        RunLockedAction(() => {
            if (_dependencyItems.ContainsKey(typeof(T)) && !allowReregister)
                throw new DependencyAlreadyRegisteredException(typeof(T));

            _dependencyItems[typeof(T)] = DependencyItem.Create(this, lifetime, factory: factory);
        });
    }

    private void RunLockedAction(Action a) {
        try {
            _lock.WaitOne();
            a();
        }
        finally {
            _lock.ReleaseMutex();
        }
    }

    private T RunLockedFunction<T>(Func<T> f) {
        try {
            _lock.WaitOne();
            return f();
        }
        finally {
            _lock.ReleaseMutex();
        }
    }

    public void Dispose() {
        _lock.Dispose();
    }

    ~DependencyManager() {
        Dispose();
    }
}