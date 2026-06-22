using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ProcessMonitor
{
    public partial class MainWindow : Window
    {
        private List<CommandGroup> commandGroups = new List<CommandGroup>();
        private CommandGroup currentGroup = null;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEventHandlers();
            LoadCommandGroups();
        }

        private void InitializeEventHandlers()
        {
            ProcessManager.OutputReceived += AppendOutput;
            ProcessManager.ProcessExited += OnProcessExited;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 终止所有运行中的进程
            foreach (var group in commandGroups.Where(g => g.IsRunning))
            {
                ProcessManager.KillProcess(group, null);
            }
        }

        private void LoadCommandGroups()
        {
            commandGroups = ConfigManager.LoadCommandGroups();
            CommandGroupList.ItemsSource = commandGroups;
            UpdateTabControl();
        }

        private void UpdateTabControl()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateTabControl);
                return;
            }

            // 只显示运行中的命令组
            var runningGroups = commandGroups.Where(g => g.IsRunning).ToList();
            OutputTabControl.ItemsSource = runningGroups;
            
            // 如果当前命令组正在运行，自动选中对应的Tab页
            if (currentGroup != null && currentGroup.IsRunning)
            {
                OutputTabControl.SelectedItem = currentGroup;
            }
        }

        private void AppendOutput(CommandGroup group, string text)
        {
            if (group == null)
                return;

            string timestampedText = $"[{System.DateTime.Now:HH:mm:ss}] {text}";

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => group.AppendOutput(timestampedText));
                return;
            }

            group.AppendOutput(timestampedText);
        }

        private void OnProcessExited(CommandGroup group)
        {
            UpdateCommandGroupStatus();
        }

        private void UpdateCommandGroupStatus()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateCommandGroupStatus);
                return;
            }

            CommandGroupList.Items.Refresh();
            UpdateTabControl();
        }

        private void CommandGroupList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            currentGroup = CommandGroupList.SelectedItem as CommandGroup;
            if (currentGroup != null)
            {
                GroupNameBox.Text = currentGroup.Name;
                StartCommandBox.Text = currentGroup.StartCommand;
                StopCommandBox.Text = currentGroup.StopCommand;
                RestartCommandBox.Text = currentGroup.RestartCommand;

                int index = commandGroups.IndexOf(currentGroup);
                if (index >= 0 && index < OutputTabControl.Items.Count)
                {
                    OutputTabControl.SelectedIndex = index;
                }
            }
        }

        private void OutputTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            CommandGroup selectedGroup = OutputTabControl.SelectedItem as CommandGroup;
            if (selectedGroup != null)
            {
                CommandGroupList.SelectedItem = selectedGroup;
            }
        }

        private void NewCommandGroup_Click(object sender, RoutedEventArgs e)
        {
            currentGroup = new CommandGroup();
            ClearInputFields();
            CommandGroupList.SelectedItem = null;
            GroupNameBox.Focus();
        }

        private void ClearInputFields()
        {
            GroupNameBox.Text = "";
            StartCommandBox.Text = "";
            StopCommandBox.Text = "";
            RestartCommandBox.Text = "";
        }

        private void ClearOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentGroup != null)
            {
                currentGroup.Output = "";
                UpdateTabControl();
            }
        }

        private void SaveCommandGroup_Click(object sender, RoutedEventArgs e)
        {
            string name = GroupNameBox.Text.Trim();
            string startCmd = StartCommandBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入命令组名称");
                return;
            }

            if (string.IsNullOrEmpty(startCmd))
            {
                MessageBox.Show("请输入启动命令");
                return;
            }

            if (currentGroup != null && commandGroups.Contains(currentGroup))
            {
                UpdateExistingGroup(name, startCmd);
            }
            else
            {
                CreateNewGroup(name, startCmd);
            }
        }

        private void UpdateExistingGroup(string name, string startCmd)
        {
            currentGroup.Name = name;
            currentGroup.StartCommand = startCmd;
            currentGroup.StopCommand = StopCommandBox.Text.Trim();
            currentGroup.RestartCommand = RestartCommandBox.Text.Trim();

            ConfigManager.SaveCommandGroups(commandGroups);
            UpdateTabControl();
            CommandGroupList.Items.Refresh();

            MessageBox.Show("保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CreateNewGroup(string name, string startCmd)
        {
            currentGroup = new CommandGroup
            {
                Name = name,
                StartCommand = startCmd,
                StopCommand = StopCommandBox.Text.Trim(),
                RestartCommand = RestartCommandBox.Text.Trim()
            };
            commandGroups.Add(currentGroup);

            ConfigManager.SaveCommandGroups(commandGroups);

            CommandGroupList.ItemsSource = null;
            CommandGroupList.ItemsSource = commandGroups;
            CommandGroupList.SelectedItem = currentGroup;

            UpdateTabControl();

            MessageBox.Show("保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteCommandGroup_Click(object sender, RoutedEventArgs e)
        {
            if (currentGroup != null)
            {
                if (currentGroup.IsRunning)
                {
                    MessageBox.Show("请先停止正在运行的命令组");
                    return;
                }

                commandGroups.Remove(currentGroup);
                CommandGroupList.ItemsSource = null;
                CommandGroupList.ItemsSource = commandGroups;
                ConfigManager.SaveCommandGroups(commandGroups);
                UpdateTabControl();

                ClearInputFields();
                currentGroup = null;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentGroup == null)
            {
                MessageBox.Show("请先选择一个命令组");
                return;
            }
            
            if (string.IsNullOrEmpty(currentGroup.StartCommand))
            {
                MessageBox.Show("启动命令不能为空");
                return;
            }
            
            AppendOutput(currentGroup, $"启动命令组: {currentGroup.Name}\n");
            ProcessManager.StartProcess(currentGroup, UpdateCommandGroupStatus);
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentGroup != null)
            {
                AppendOutput(currentGroup, $"停止命令组: {currentGroup.Name}\n");

                if (currentGroup.Process == null || currentGroup.Process.HasExited)
                {
                    AppendOutput(currentGroup, "没有运行中的进程\n");
                    return;
                }

                await ProcessManager.StopProcessAsync(currentGroup, UpdateCommandGroupStatus);
            }
            else
            {
                MessageBox.Show("请先选择一个命令组");
            }
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentGroup != null && !string.IsNullOrEmpty(currentGroup.StartCommand))
            {
                AppendOutput(currentGroup, $"重启命令组: {currentGroup.Name}\n");

                if (!string.IsNullOrEmpty(currentGroup.RestartCommand))
                {
                    ProcessManager.ExecuteCommand(currentGroup, currentGroup.RestartCommand);
                }
                else
                {
                    ProcessManager.KillProcess(currentGroup, UpdateCommandGroupStatus);
                    System.Threading.Thread.Sleep(500);
                    ProcessManager.StartProcess(currentGroup, UpdateCommandGroupStatus);
                }
            }
            else
            {
                MessageBox.Show("请先选择一个命令组");
            }
        }
    }
}