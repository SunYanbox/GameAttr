using Microsoft.Extensions.Logging;

namespace GameAttr;

/// <summary>
/// Configures Microsoft.Extensions.Logging for the GameAttr library based on environment variables.
///
/// Environment variables:
/// <list type="bullet">
///   <item><c>GAMEATTR_LOG_CONSOLE</c> — set to "false" to disable console logging (default: "true")</item>
///   <item><c>GAMEATTR_LOG_FILE</c> — set to "true" to enable file logging to <c>gameattr.log</c> (default: "false")</item>
/// </list>
/// </summary>
public static class AttrLoggingConfiguration
{
    private const string EnvConsole = "GAMEATTR_LOG_CONSOLE";
    private const string EnvFile = "GAMEATTR_LOG_FILE";
    private const string DefaultLogFileName = "gameattr.log";

    /// <summary>Create an <see cref="ILoggerFactory"/> configured from environment variables.</summary>
    public static ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            bool consoleEnabled = Environment.GetEnvironmentVariable(EnvConsole) != "false";
            bool fileEnabled = Environment.GetEnvironmentVariable(EnvFile) == "true";

            if (consoleEnabled)
            {
                builder.AddConsole();
            }

            if (fileEnabled)
            {
                builder.AddProvider(new SimpleFileLoggerProvider(DefaultLogFileName));
            }
        });
    }

    /// <summary>Simple file logger provider that writes log entries to a text file.</summary>
    private sealed class SimpleFileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;

        internal SimpleFileLoggerProvider(string filePath)
        {
            _filePath = filePath;
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(_filePath, categoryName);

        public void Dispose() { }

        private sealed class FileLogger : ILogger
        {
            private readonly string _filePath;
            private readonly string _categoryName;

            public FileLogger(string filePath, string categoryName)
            {
                _filePath = filePath;
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] [{_categoryName}] {formatter(state, exception)}";
                if (exception is not null)
                    line += $"\n{exception}";

                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
        }
    }
}
