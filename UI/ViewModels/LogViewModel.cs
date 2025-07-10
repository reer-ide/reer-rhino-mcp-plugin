using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using ReerRhinoMCPPlugin.UI.ViewModels.Base;
using ReerRhinoMCPPlugin.UI.ViewModels.Commands;

namespace ReerRhinoMCPPlugin.UI.ViewModels
{
    /// <summary>
    /// ViewModel for log display and management
    /// </summary>
    public class LogViewModel : ViewModelBase
    {
        private const int MaxLogEntries = 100;

        public LogViewModel()
        {
            LogEntries = new ObservableCollection<LogEntry>();
            ClearLogCommand = new RelayCommand(ClearLog);
            
            // Add welcome message
            AddLogEntry("INFO", "MCP Control Panel initialized");
        }

        #region Properties

        public ObservableCollection<LogEntry> LogEntries { get; }

        #endregion

        #region Commands

        public ICommand ClearLogCommand { get; }

        #endregion

        #region Public Methods

        public void AddLogEntry(string level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Level = level,
                Message = message,
                LevelColor = GetLevelColor(level)
            };

            LogEntries.Insert(0, entry);
            
            // Keep only last entries
            while (LogEntries.Count > MaxLogEntries)
            {
                LogEntries.RemoveAt(LogEntries.Count - 1);
            }
        }

        #endregion

        #region Private Methods

        private void ClearLog()
        {
            LogEntries.Clear();
            AddLogEntry("INFO", "Log cleared");
        }

        private IBrush GetLevelColor(string level)
        {
            return level switch
            {
                "ERROR" => Brushes.Red,
                "SUCCESS" => Brushes.Green,
                "COMMAND" => Brushes.Blue,
                "INFO" => Brushes.Gray,
                _ => Brushes.Black
            };
        }

        #endregion
    }

    /// <summary>
    /// Represents a single log entry
    /// </summary>
    public class LogEntry
    {
        public string Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public IBrush LevelColor { get; set; }
        public IBrush Background { get; set; } = Brushes.Transparent;
    }
}
