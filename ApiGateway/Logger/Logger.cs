namespace ApiGateway.Logger
{
    public class Logger<T> : ILogger<T>
    {
        private readonly ILogger<T> _logger;
        public Logger(ILoggerFactory loggerFactory) 
        { 
            _logger = loggerFactory.CreateLogger<T>();
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _logger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}