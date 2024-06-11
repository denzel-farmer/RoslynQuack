using System;
using System.Diagnostics;
using System.IO;
// TODO replace with an actual logger
namespace Quack
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public class Logger
    {
        private const string LogFileName = "log.txt";
        private readonly string outputDirectory;

        private string LogPrefix { get; set; }

        public Logger(string outputDirectory)
        {
            this.outputDirectory = outputDirectory;
            Info("NEW RUN: logger started");
            this.LogPrefix = "";
        }
        // TODO this is stupid and probably slow
        private void setPrefixToCallerClass()
        {
            var methodInfo = new StackTrace().GetFrame(3)?.GetMethod();
            LogPrefix = methodInfo?.ReflectedType?.Name ?? "";
        }
        public void Plain(LogLevel level, string message)
        {
            string logFilePath = Path.Combine(outputDirectory, LogFileName);
            setPrefixToCallerClass();
            // Only print info and higher to standard out
            if (level >= LogLevel.Info)
            {
                Console.Write(message);
            }

            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.Write(message);
            }
        }
        public void Log(LogLevel level, string message)
        {
            string logFilePath = Path.Combine(outputDirectory, LogFileName);
            string logEntry;
            if (LogPrefix != null)
            {
                logEntry = $"{DateTime.Now} [{level}][{LogPrefix}] {message}";
            }
            else
            {
                logEntry = $"{DateTime.Now} [{level}] {message}";
            }
            // Only print info and higher to standard out
            if (level >= LogLevel.Info)
            {
                Console.WriteLine(logEntry);
            }

            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.WriteLine(logEntry);
            }
        }

        public void Debug(string message)
        {
            setPrefixToCallerClass();
            Log(LogLevel.Debug, message);
        }

        public void Info(string message)
        {
            setPrefixToCallerClass();
            Log(LogLevel.Info, message);
        }

        public void Warn(string message)
        {
            setPrefixToCallerClass();
            Log(LogLevel.Warn, message);
        }

        public void Error(string message)
        {
            setPrefixToCallerClass();
            Log(LogLevel.Error, message);
        }

        public void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Log(LogLevel.Error, message);
                throw new Exception(message);
            }
        }
    }
}