using GetYourDeps;

namespace GetYourDeps.Tests;

public class UnitTestDependencyManager
{
    [Fact]
    public void AddSingletonTest() {
        var sm = new DependencyManager();
        sm.RegisterSingleton(_ => "Hello, world!");
        Assert.Equal("Hello, world!", sm.GetDependency<string>());
        Assert.Same(sm.GetDependency<string>(), sm.GetDependency<string>());
    }
    
    [Fact]
    public void AddInstancedTest() {
        var sm = new DependencyManager();
        sm.RegisterInstanced(_ => new string("Hello, world!")); // new string because otherwise it would return the same instance
        Assert.Equal("Hello, world!", sm.GetDependency<string>());
        Assert.NotSame(sm.GetDependency<string>(), sm.GetDependency<string>());
    }
}