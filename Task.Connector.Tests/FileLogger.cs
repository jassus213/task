using Task.Integration.Data.Models;

namespace Task.Connector.Tests
{
    /*
     * Лучше использовать структурные логи, чтобы при необходимости перейти на ELK (или подобное), можно было выполнять быстрый поиск
     * Логи должны быть формата Logger.Error("message", params);
     */
    public class FileLogger : ILogger
    {
        string _fileName;
        string _connectorName;

        public FileLogger(string fileName, string connectorName)
        {
            _fileName = fileName;
            _connectorName = connectorName;

            if (!File.Exists(_fileName))
            {
                using var stream = File.Create(_fileName);
            }
        }

        void Append(string text)
        {
            using var sw = File.AppendText(_fileName);
            sw.WriteLine(text);
        }

        public void Debug(string message) => Append($"{DateTime.Now}:{_connectorName}:DEBUG:{message}");

        public void Error(string message) => Append($"{DateTime.Now}:{_connectorName}:ERROR:{message}");
        public void Warn(string message) => Append($"{DateTime.Now}:{_connectorName}:WARNING{message}");
    }
}