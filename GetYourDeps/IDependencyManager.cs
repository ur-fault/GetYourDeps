namespace GetYourDeps;

class DependencyAlreadyRegisteredException : Exception
{
    public Type ServiceType { get; }
    public DependencyAlreadyRegisteredException(Type serviceType) => ServiceType = serviceType;

    public override string Message => $"Service of type {ServiceType.FullName} is already registered.";
}

public interface IDependencyManager : IDependencyProvider
{
    IDependencyManager RegisterSingleton<T>(Func<IDependencyManager, T> factory, bool allowReregister = false)
        where T : class;

    IDependencyManager RegisterInstanced<T>(Func<IDependencyManager, T> factory, bool allowReregister = false)
        where T : class;
}