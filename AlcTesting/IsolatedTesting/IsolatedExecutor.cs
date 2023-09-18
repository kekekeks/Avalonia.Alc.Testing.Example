using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace IsolatedTestFramework;

class IsolatedExecutor : XunitTestFrameworkExecutor
{
    public IsolatedExecutor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink) 
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    protected override void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink outputSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        testCases = testCases.ToList();
        var bus = new MessageBus(outputSink);
        var asmCounter = new TestStatsCounter(null);
        var doRun = bus.QueueMessage(new TestAssemblyStarting(testCases, this.TestAssembly, DateTime.UtcNow,
            RuntimeInformation.RuntimeIdentifier, "Isolated ALC runner"));
        foreach (var collection in testCases.GroupBy(c => c.TestMethod.TestClass.TestCollection))
        {
            if(!doRun)
                break;
            var collectionCounter = new TestStatsCounter(asmCounter);
            doRun &= bus.QueueMessage(new TestCollectionStarting(collection, collection.Key));
            foreach (var testClass in collection.GroupBy(c => c.TestMethod.TestClass))
            {
                if(!doRun)
                    break;
                var classCounter = new TestStatsCounter(collectionCounter);
                doRun &= bus.QueueMessage(new TestClassStarting(testClass, testClass.Key));
                foreach (var testMethod in testClass.GroupBy(c => c.TestMethod))
                {
                    if(!doRun)
                        break;
                    var methodCounter = new TestStatsCounter(classCounter);
                    doRun &= bus.QueueMessage(new TestMethodStarting(testMethod, testMethod.Key));
                    foreach (var testCase in testMethod)
                    {
                        if(!doRun)
                            break;
                        var caseCounter = new TestStatsCounter(methodCounter);
                        doRun &= bus.QueueMessage(new TestCaseStarting(testCase));
                        var test = new XunitTest(testCase, testCase.DisplayName);

                        if (testCase.SkipReason != null)
                        {
                            caseCounter.Skipped();
                            doRun &= bus.QueueMessage(new TestSkipped(test, testCase.SkipReason));
                            continue;
                        }
                        
                        doRun &= bus.QueueMessage(new TestStarting(test));

                        var output = new TestOutputHelper();
                        output.Initialize(bus, test);
                        try
                        {
                            RemoteExecutor.ExecuteTest(
                                output,
                                test.DisplayName,
                                AssemblyInfo.Name,
                                testCase.TestMethod.TestClass.Class.Name,
                                testCase.TestMethod.Method.Name, testCase.TestMethodArguments,
                                () => bus.QueueMessage(new TestClassConstructionStarting(test)),
                                () => bus.QueueMessage(new TestClassConstructionFinished(test)),
                                (decimal time) =>
                                    doRun &= bus.QueueMessage(new TestPassed(test, time, output.Output)),
                                (decimal time, Exception ex) =>
                                    doRun &= bus.QueueMessage(new TestFailed(test, time, output.Output, ex)),
                                testCases.Count() == 1);
                        }
                        catch (Exception e)
                        {
                            doRun &= bus.QueueMessage(new TestFailed(test, caseCounter.ExecutionTime, output.Output, e));
                        }

                        doRun &= bus.QueueMessage(new TestFinished(test, caseCounter.ExecutionTime, output.Output));
                        output.Uninitialize();
                    }

                    doRun &= bus.QueueMessage(new TestMethodFinished(testMethod, testMethod.Key,
                        methodCounter.ExecutionTime, methodCounter.RunCount, methodCounter.FailedCount,
                        methodCounter.SkippedCount));
                    
                }

                doRun &= bus.QueueMessage(new TestClassFinished(testClass, testClass.Key,
                    classCounter.ExecutionTime, classCounter.RunCount, classCounter.FailedCount, classCounter.SkippedCount));
            }

            doRun &= bus.QueueMessage(new TestCollectionFinished(collection, collection.Key, 
                collectionCounter.ExecutionTime, collectionCounter.RunCount, collectionCounter.FailedCount, collectionCounter.SkippedCount));
        }

        doRun &= bus.QueueMessage(new TestAssemblyFinished(testCases, TestAssembly,
            asmCounter.ExecutionTime, asmCounter.RunCount, asmCounter.FailedCount, asmCounter.SkippedCount));
    }
}

class TestStatsCounter
{
    private readonly TestStatsCounter? _parent;
    private Stopwatch _st = Stopwatch.StartNew();

    public TestStatsCounter(TestStatsCounter? parent)
    {
        _parent = parent;
    }
    
    
    public int FailedCount { get; private set; }
    public int PassedCount { get; private set; }
    public int SkippedCount { get; private set; }
    public int RunCount => PassedCount + FailedCount;
    public decimal ExecutionTime => _st.Elapsed.Seconds;

    public void Failed()
    {
        _parent?.Failed();
        FailedCount++;
    }
    
    public void Skipped()
    {
        _parent?.Skipped();
        SkippedCount++;
    }

    public void Passed()
    {
        _parent?.Passed();
        PassedCount++;
    }
}