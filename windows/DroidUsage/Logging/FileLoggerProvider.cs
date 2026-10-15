using System.Text;
using Microsoft.Extensions.Logging;

namespace DroidUsage.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxFileBytes = 1_000_000;

    private readonly string _filePath;
    private readonly object _gate = new();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(string line)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

                var info = new FileInfo(_filePath);
                if (info.Exists && info.Length > MaxFileBytes)
                {
                    File.Move(_filePath, _filePath + ".1", overwrite: true);
                }

                File.AppendAllText(_filePath, line, Encoding.UTF8);
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"{DateTimeOffset.Now:O} [{logLevel}] {_category}: {formatter(state, exception)}");
            if (exception != null)
            {
                sb.Append(Environment.NewLine).Append(exception);
            }

            sb.AppendLine();
            _provider.Write(sb.ToString());
        }
    }
}
