using Xunit.Abstractions;

namespace IsolatedTestFramework;

[AttributeUsage(AttributeTargets.Assembly)]
public class IsolatedTestFrameworkCallbacksAttribute : Attribute
{
    public Type Type { get; }

    public IsolatedTestFrameworkCallbacksAttribute(Type type)
    {
        Type = type;
    }
}

public abstract class IsolatedTestFrameworkCallbacksBase
{
    public abstract IsolatedTestFrameworkCallbacksSessionBase CreateSession(Type type,
        string methodName, object[] arguments, ITestOutputHelper output);
}

public abstract class IsolatedTestFrameworkCallbacksSessionBase
{
    public virtual void OnCreatingTestClass()
    {
        
    }
    
    public virtual void OnRunTestMethod(Action executeTestMethod)
    {
        executeTestMethod();
    }
}