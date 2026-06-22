using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ProcessMonitor
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "command_groups.json");

        public static List<CommandGroup> LoadCommandGroups()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<List<CommandGroup>>(json) ?? new List<CommandGroup>();
                }
                return GetDefaultCommandGroups();
            }
            catch (Exception ex)
            {
                LogError($"加载配置失败: {ex.Message}");
                return GetDefaultCommandGroups();
            }
        }

        public static void SaveCommandGroups(List<CommandGroup> commandGroups)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };
                string json = JsonSerializer.Serialize(commandGroups, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                LogError($"保存配置失败: {ex.Message}");
            }
        }

        private static List<CommandGroup> GetDefaultCommandGroups()
        {
            return new List<CommandGroup>
            {
                new CommandGroup
                {
                    Name = "后端服务",
                    StartCommand = "cd C:\\Users\\15844\\WorkBuddy\\2026-06-11-09-40-23\\backend\r\n.venv\\Scripts\\Activate.ps1\r\nuvicorn app.main:app --reload --host 0.0.0.0 --port 8001",
                    StopCommand = "",
                    RestartCommand = ""
                }
            };
        }

        public static void LogError(string message)
        {
            try
            {
                File.AppendAllText("error.log", $"{DateTime.Now}: {message}\n");
            }
            catch { }
        }
    }
}