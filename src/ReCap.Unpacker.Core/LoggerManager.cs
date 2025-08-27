using System;

namespace ReCap.Unpacker.Core
{
    public enum LogLevel { Fine = 10, Info = 20, Warning = 30, Severe = 40 }

    public sealed class SimpleLogger
    {
        private readonly string _name;
        private readonly Func<LogLevel> _levelProvider;

        internal SimpleLogger(string name, Func<LogLevel> levelProvider)
        {
            _name = name;
            _levelProvider = levelProvider;
        }

        private void Write(LogLevel level, string message)
        {
            if ((int)level < (int)_levelProvider()) return;
            Console.WriteLine($"[{level.ToString().ToUpper()}] {message}");
        }

        public void Fine(string msg)    => Write(LogLevel.Fine, msg);
        public void Info(string msg)    => Write(LogLevel.Info, msg);
        public void Warning(string msg) => Write(LogLevel.Warning, msg);
        public void Severe(string msg)  => Write(LogLevel.Severe, msg);
    }

    public static class LoggerManager
    {
        private static LogLevel _current = LogLevel.Info;

        public static void Initialize(bool debug)
        {
            _current = debug ? LogLevel.Fine : LogLevel.Info;
        }

        public static SimpleLogger GetLogger(string name) =>
            new(name, () => _current);

        public static SimpleLogger GetLogger(Type t) =>
            GetLogger(t.FullName ?? t.Name);

        public static SimpleLogger GetLogger<T>() =>
            GetLogger(typeof(T));
    }
}
