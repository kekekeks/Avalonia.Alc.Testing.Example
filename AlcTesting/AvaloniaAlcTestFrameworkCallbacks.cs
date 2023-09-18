using Avalonia;
using Avalonia.Headless;
using IsolatedTestFramework;
using Xunit.Abstractions;

namespace AlcTesting;

public class AvaloniaAlcTestFrameworkCallbacks<TApp> : IsolatedTestFramework.IsolatedTestFrameworkCallbacksBase where TApp : Application, new()
{
    public override IsolatedTestFrameworkCallbacksSessionBase CreateSession(Type type, string methodName, object[] arguments,
        ITestOutputHelper output)
    {
        return new Session(type, methodName, arguments, output);
    }

    class Session : IsolatedTestFrameworkCallbacksSessionBase
    {
        private readonly Type _type;
        private readonly string _methodName;
        private readonly object[] _arguments;
        private readonly ITestOutputHelper _output;

        public Session(Type type, string methodName, object[] arguments, ITestOutputHelper output)
        {
            _type = type;
            _methodName = methodName;
            _arguments = arguments;
            _output = output;
        }

        public override void OnCreatingTestClass()
        {
            AppBuilder.Configure<TApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions()
                {
                    UseHeadlessDrawing = false
                })
                .UseSkia()
                .SetupWithoutStarting();
            base.OnCreatingTestClass();
        }
    }
}