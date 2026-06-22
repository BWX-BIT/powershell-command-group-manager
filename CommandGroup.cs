using System.Windows.Media;

namespace ProcessMonitor
{
    public class CommandGroup : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name;
        private string _startCommand;
        private string _stopCommand;
        private string _restartCommand;
        private bool _isRunning;
        private System.Diagnostics.Process _process;
        private System.Diagnostics.Process _parentProcess;
        private string _output = "";
        private readonly object _outputLock = new object();

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string StartCommand
        {
            get => _startCommand;
            set { _startCommand = value; OnPropertyChanged(); }
        }

        public string StopCommand
        {
            get => _stopCommand;
            set { _stopCommand = value; OnPropertyChanged(); }
        }

        public string RestartCommand
        {
            get => _restartCommand;
            set { _restartCommand = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Diagnostics.Process Process
        {
            get => _process;
            set 
            { 
                _process = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ProcessId));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Diagnostics.Process ParentProcess
        {
            get => _parentProcess;
            set 
            { 
                _parentProcess = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ParentProcessId));
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string Output
        {
            get => _output;
            set { _output = value; OnPropertyChanged(); }
        }

        public void AppendOutput(string text)
        {
            lock (_outputLock)
            {
                _output += text;
            }
            OnPropertyChanged(nameof(Output));
        }

        public int ProcessId => Process?.Id ?? 0;

        public int ParentProcessId => ParentProcess?.Id ?? 0;

        public bool HasChildProcess => Process != null && ParentProcess != null && Process.Id != ParentProcess.Id;

        public string StatusText => IsRunning ? "运行中" : "未运行";

        [System.Text.Json.Serialization.JsonIgnore]
        public Brush StatusColor => IsRunning ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray);

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}