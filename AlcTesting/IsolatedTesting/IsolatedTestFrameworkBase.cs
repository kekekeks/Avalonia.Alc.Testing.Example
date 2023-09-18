using System.Diagnostics;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace IsolatedTestFramework;

public class IsolatedTestFramework : XunitTestFramework
{
    public IsolatedTestFramework(IMessageSink messageSink) : base(messageSink)
    {
        
    }

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        return new IsolatedExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
    }
}