namespace GetYourDeps;

public class DependencyNotRegisteredException : Exception
{
    public Type ServiceType { get; }
    public DependencyNotRegisteredException(Type serviceType) => ServiceType = serviceType;

    public override string Message => $"Service of type {ServiceType.FullName} is not registered.";
}

public interface IDependencyProvider
{
    /// <summary>
    /// Gets the service of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>Registered service of type <typeparamref name="T"/> from provider</returns>
    /// <exception cref="DependencyNotRegisteredException">If service of this type is not registered</exception>
    public T GetDependency<T>() where T : class;

    /// <summary>
    /// Tries to get the service of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>Returns service of type <typeparamref name="T"/> if registered otherwise <value>null</value></returns>
    T? TryGetDependency<T>() where T : class;
}