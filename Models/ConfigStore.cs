using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace TypingApp.Models
{
    public class AppConfig
    {
        public int TypingDelay { get; set; } = 10;
        public HotkeyConfig PasteHotkey { get; set; } = new HotkeyConfig 
        { 
            Key = Key.V, 
            Modifiers = ModifierKeys.Control | ModifierKeys.Shift 
        };
    }

    public class HotkeyConfig
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
    }

    public class ConfigStore
    {
        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public AppConfig Current { get; private set; }

        public ConfigStore()
        {
            Current = Load();
        }

        public AppConfig Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                    return new AppConfig();
                }
            }
            return new AppConfig();
        }

        public void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Current, options);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
