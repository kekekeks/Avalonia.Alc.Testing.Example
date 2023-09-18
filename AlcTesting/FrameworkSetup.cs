using Avalonia;
using Avalonia.Themes.Simple;
using IsolatedTestFramework;
using AlcTesting;
using Xunit;
[assembly: TestFramework("IsolatedTestFramework.IsolatedTestFramework", "AlcTesting")]
[assembly: IsolatedTestFrameworkCallbacks(typeof(AvaloniaAlcTestFrameworkCallbacks<SimpleApp>))]

namespace AlcTesting;
class SimpleApp : Application
{
    public SimpleApp()
    {
        Resources.MergedDictionaries.Add(new SimpleTheme());
    }
}