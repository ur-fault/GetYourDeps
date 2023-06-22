using System.Diagnostics;

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
        public DependencyItem(Type dependencyType, DependencyManager manager, Func<IDependencyManager, object> factory,
            DependencyLifetime lifetime) {
            DependencyType = dependencyType;
            Factory = factory;
            Lifetime = lifetime;
            _manager = manager;
            _threadLocalInstances = new(() => Factory(_manager));
        }

        public Type DependencyType { get; }
        public Func<IDependencyManager, object> Factory { get; }
        public DependencyLifetime Lifetime { get; set; }

        private DependencyManager _manager;
        private object? _singletonInstance;
        private readonly ThreadLocal<object> _threadLocalInstances;

        public object GetInstance(IDependencyManager manager) {
            return Lifetime switch {
                DependencyLifetime.Singleton => _singletonInstance ??= Factory(manager),
                DependencyLifetime.Instanced => Factory(manager),
                DependencyLifetime.ThreadLocal => _threadLocalInstances.Value ?? throw new NullReferenceException(),
                _ => throw new ArgumentOutOfRangeException()
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
            _dependencyItems.TryGetValue(typeof(T), out var dependency) ? (T)dependency.GetInstance(this) : null);
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

    /// <summary>
    /// Updates lifetime of registered dependency.
    ///
    /// Note: This method does NOT update or resets singleton or threadlocal instances.
    /// </summary>
    /// <param name="lifetime"></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="DependencyNotRegisteredException"></exception>
    public void UpdateLifetime<T>(DependencyLifetime lifetime) where T : class {
        if (!_dependencyItems.ContainsKey(typeof(T)))
            throw new DependencyNotRegisteredException(typeof(T));

        _dependencyItems[typeof(T)].Lifetime = lifetime;
    }

    private void Register<T>(Func<IDependencyManager, T> factory, DependencyLifetime lifetime,
        bool allowReregister) where T : class {
        RunLockedAction(() => {
            if (_dependencyItems.ContainsKey(typeof(T)) && !allowReregister)
                throw new DependencyAlreadyRegisteredException(typeof(T));

            _dependencyItems[typeof(T)] = new DependencyItem(typeof(T), this, factory, lifetime);
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