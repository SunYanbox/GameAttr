using System;
using System.IO;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GameAttr.Tests;

[TestClass]
[TestSubject(typeof(AttrLoggingConfiguration))]
public class AttrLoggingConfigurationTest
{
    private const string EnvConsole = "GAMEATTR_LOG_CONSOLE";
    private const string EnvFile = "GAMEATTR_LOG_FILE";
    private const string LogFileName = "gameattr.log";

    [TestCleanup]
    public void Cleanup()
    {
        // Restore environment variables
        Environment.SetEnvironmentVariable(EnvConsole, null);
        Environment.SetEnvironmentVariable(EnvFile, null);

        // Clean up log file if it was created
        if (File.Exists(LogFileName))
            File.Delete(LogFileName);
    }

    [TestMethod]
    public void CreateLoggerFactory_Default_ReturnsLoggerFactory()
    {
        // No environment variables set — console enabled by default, file disabled
        ILoggerFactory factory = AttrLoggingConfiguration.CreateLoggerFactory();
        Assert.IsNotNull(factory);
        factory.Dispose();
    }

    [TestMethod]
    public void CreateLoggerFactory_ConsoleDisabled_ReturnsLoggerFactory()
    {
        Environment.SetEnvironmentVariable(EnvConsole, "false");

        ILoggerFactory factory = AttrLoggingConfiguration.CreateLoggerFactory();
        Assert.IsNotNull(factory);
        factory.Dispose();
    }

    [TestMethod]
    public void CreateLoggerFactory_FileEnabled_ReturnsLoggerFactory()
    {
        Environment.SetEnvironmentVariable(EnvFile, "true");

        ILoggerFactory factory = AttrLoggingConfiguration.CreateLoggerFactory();
        Assert.IsNotNull(factory);
        factory.Dispose();
    }

    [TestMethod]
    public void CreateLoggerFactory_FileEnabled_WritesLogFile()
    {
        Environment.SetEnvironmentVariable(EnvFile, "true");

        ILoggerFactory factory = AttrLoggingConfiguration.CreateLoggerFactory();
        ILogger logger = factory.CreateLogger("TestCategory");

        // FileLogger.IsEnabled only allows Warning+ levels
        logger.LogWarning("Test warning message");

        factory.Dispose();

        Assert.IsTrue(File.Exists(LogFileName), "Log file should exist after logging");
        string content = File.ReadAllText(LogFileName);
        Assert.IsTrue(content.Contains("Test warning message"), "Log file should contain the message");
        Assert.IsTrue(content.Contains("TestCategory"), "Log file should contain the category name");
        Assert.IsTrue(content.Contains("Warning"), "Log file should contain the log level");
    }

    [TestMethod]
    public void CreateLoggerFactory_BeginScope_DoesNotThrow()
    {
        // Covers BeginScope<TState> on FileLogger (line 65: => null).
        // The composed ILogger wraps scopes, so the returned value is non-null,
        // but the underlying FileLogger's BeginScope is still exercised.
        Environment.SetEnvironmentVariable(EnvFile, "true");

        ILoggerFactory factory = AttrLoggingConfiguration.CreateLoggerFactory();
        ILogger logger = factory.CreateLogger("TestCategory");

        IDisposable? scope = logger.BeginScope("test scope");
        Assert.IsNotNull(scope);
        // Dispose the scope — should not throw
        scope!.Dispose();

        factory.Dispose();
    }

    [TestMethod]
    public void CreateLoggerFactory_LogBelowWarning_DoesNotWriteFile()
    {
        // Covers the early-return in FileLogger.Log when !IsEnabled(logLevel) (line 77)
        // Information is below Warning — Log should return without writing.
        Environment.SetEnvironmentVariable(EnvFile, "true");

        ILoggerFactory factory = AttrLoggingConfiguration.CreateLoggerFactory();
        ILogger logger = factory.CreateLogger("TestCategory");

        logger.LogInformation("This should not appear in the file");

        factory.Dispose();

        Assert.IsFalse(File.Exists(LogFileName), "File should not be created when logging below Warning level");
    }

    [TestMethod]
    public void CreateLoggerFactory_LogWithException_WritesExceptionDetails()
    {
        // Covers the exception-not-null branch in FileLogger.Log (lines 80-81)
        Environment.SetEnvironmentVariable(EnvFile, "true");

        ILoggerFactory factory = AttrLoggingConfiguration.CreateLoggerFactory();
        ILogger logger = factory.CreateLogger("TestCategory");

        var ex = new InvalidOperationException("Something went wrong");
        logger.LogWarning(ex, "Warning with exception");

        factory.Dispose();

        Assert.IsTrue(File.Exists(LogFileName), "Log file should exist after logging");
        string content = File.ReadAllText(LogFileName);
        Assert.IsTrue(content.Contains("Warning with exception"), "Log file should contain the message");
        Assert.IsTrue(content.Contains("Something went wrong"), "Log file should contain the exception message");
        Assert.IsTrue(content.Contains("InvalidOperationException"), "Log file should contain the exception type");
    }

    [TestMethod]
    public void SimpleFileLoggerProvider_Dispose_DoesNotThrow()
    {
        // SimpleFileLoggerProvider is private sealed, so we access it via reflection
        // to exercise Dispose() directly. The framework's LoggerFactory.Dispose()
        // chain is not instrumented by coverlet, so the hit on line 52 cannot be
        // detected indirectly — this test calls it from instrumented test code.
        const string tempLog = "temp_provider_dispose.log";

        Type? providerType = typeof(AttrLoggingConfiguration)
            .GetNestedType("SimpleFileLoggerProvider", BindingFlags.NonPublic);
        Assert.IsNotNull(providerType, "SimpleFileLoggerProvider type should be found via reflection");

        object? provider = Activator.CreateInstance(providerType!,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new object[] { tempLog },
            null);
        Assert.IsNotNull(provider, "Provider instance should be created");

        // Dispose is a no-op { } — verify it doesn't throw
        ((IDisposable)provider!).Dispose();

        // Clean up temp file
        if (File.Exists(tempLog))
            File.Delete(tempLog);
    }
}
