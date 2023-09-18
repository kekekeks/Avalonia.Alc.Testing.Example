using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace IsolatedTestFramework;

class CallbackTestOutputHelper : ITestOutputHelper
{
    private readonly Action<string> _log;

    public CallbackTestOutputHelper(Action<string> log)
    {
        _log = log;
    }
    public void WriteLine(string message)
    {
        _log(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        var formatted = string.Format(format, args);
        WriteLine(formatted);
    }
}

internal class RemoteExecutor
{
    private static void ExecuteTestCore(
        Action<string> log,
        string assemblyName, string className, string methodName,
        object[] methodArguments,
        Action creatingClass, Action createdClass, 
        Action<decimal> testSuccess,
        Action<decimal, Exception> testFailed)
    {
        var output = new CallbackTestOutputHelper(log);
        var st = Stopwatch.StartNew();
        try
        {
            var ourAlc = AssemblyLoadContext.GetLoadContext(typeof(RemoteExecutor).Assembly)!;
            creatingClass();
            var asm = ourAlc.LoadFromAssemblyName(new AssemblyName(assemblyName));

            IsolatedTestFrameworkCallbacksBase? callbacks = null;
            var callbacksAttr = asm.GetCustomAttributes().OfType<IsolatedTestFrameworkCallbacksAttribute>().FirstOrDefault();
            if (callbacksAttr != null)
            {
                callbacks = (IsolatedTestFrameworkCallbacksBase)Activator.CreateInstance(callbacksAttr.Type)!;
            }

            var type = asm.GetType(className) ?? throw new TypeLoadException(
                $"Can't get type {className} from {asm.FullName}");
            
            var session = callbacks?.CreateSession(type, methodName, methodArguments, output);
            session?.OnCreatingTestClass();

            object? testClass = null;
            foreach (var ctor in type.GetConstructors())
                if (ctor.GetParameters().Length == 1 &&
                    ctor.GetParameters()[0].ParameterType == typeof(ITestOutputHelper))
                {
                    testClass = ctor.Invoke(new Object?[] { output });
                    break;
                }

            if (testClass == null)
                testClass = Activator.CreateInstance(type);

            createdClass();
            
            var method = type.GetMethod(methodName);
            var invoker = new Action(() => method.Invoke(testClass, methodArguments));
            if(session != null)
                session.OnRunTestMethod(invoker);
            else
                invoker();

            testSuccess(1);

        }
        catch (Exception e)
        {
            testFailed(st.Elapsed.Seconds, e);
        }
    }


    public static void ExecuteTest(
        ITestOutputHelper output,
        string displayName, string assemblyName, string className, string methodName,
        object[] methodArguments,
        Action creatingClass, Action createdClass, 
        Action<decimal> testSuccess,
        Action<decimal,Exception> testFailed,
        bool runWithoutAlc
        )
    {
        var currentAlc = AssemblyLoadContext.GetLoadContext(typeof(IsolatedExecutor).Assembly) ??
                         AssemblyLoadContext.Default;
        
        var alc = new IsolatedAlc(className + "::" + displayName, currentAlc, output);
        var isolatedAssembly = alc.LoadFromAssemblyPath(typeof(IsolatedExecutor).Assembly.Location);
        var isolatedExecutor = isolatedAssembly.GetType("IsolatedTestFramework.RemoteExecutor");


        void RunTestInAlc()
        {
            isolatedExecutor.GetMethod("ExecuteTestCore", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object?[]
                {
                    new Action<string>(output.WriteLine), assemblyName, className, methodName, methodArguments,
                    creatingClass, createdClass,
                    testSuccess,
                    testFailed
                });
        }

        void RunTestWithoutAlc() => RemoteExecutor.ExecuteTestCore(output.WriteLine, assemblyName, className,
            methodName, methodArguments,
            creatingClass, createdClass, testSuccess, testFailed);

        var cleanContext = ExecutionContext.Capture().CreateCopy();
        var oldSyncContext = SynchronizationContext.Current;
        var oldCultureInfo = CultureInfo.CurrentCulture;
        var oldUICultureInfo = CultureInfo.CurrentUICulture;
        var oldDefaultThreadCultureInfo = CultureInfo.DefaultThreadCurrentCulture;
        var oldDefaultThreadUICultureInfo = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentCulture =
                CultureInfo.DefaultThreadCurrentUICulture =
                    CultureInfo.InvariantCulture;
            
            ExecutionContext.Run(cleanContext, _ =>
            {
                if (runWithoutAlc)
                    RunTestWithoutAlc();
                else
                    RunTestInAlc();
            }, null);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCultureInfo;
            CultureInfo.CurrentUICulture = oldUICultureInfo;
            CultureInfo.DefaultThreadCurrentCulture = oldDefaultThreadCultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = oldDefaultThreadUICultureInfo;
            SynchronizationContext.SetSynchronizationContext(oldSyncContext);
        }
    }
}


class IsolatedAlc : AssemblyLoadContext
{
    private readonly AssemblyLoadContext _parentAlc;
    private readonly ITestOutputHelper _log;

    public IsolatedAlc(string name, AssemblyLoadContext parentAlc, ITestOutputHelper log) : base(name, true)
    {
        _parentAlc = parentAlc;
        _log = log;
    }
    
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        _log.WriteLine("Resolving: " + assemblyName);
        var def = _parentAlc.LoadFromAssemblyName(assemblyName);
        var location  = def.Location;
        _log.WriteLine("Loading from: " + location);
        var loaded = LoadFromAssemblyPath(location);
        return loaded;
    }
}