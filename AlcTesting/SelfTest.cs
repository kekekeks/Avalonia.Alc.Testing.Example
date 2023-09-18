using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace AlcTesting;

public class SelfTest
{
    private static int SomeStaticVar = 0;

    [Theory,
     InlineData(1),
     InlineData(2),
     InlineData(3),
    ]
    public void VerifyTestEnvironment(int pass)
    {
        Assert.Equal(0, SomeStaticVar);
        SomeStaticVar = pass;
        Button btn;
        var window = new Window()
        {
            Content = btn = new Button()
            {
                Content = "Test"
            },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(tb => tb.Text == "Test"));
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }
}