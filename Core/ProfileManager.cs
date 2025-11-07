using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ESP32_DS4_Controller_WPF.Core
{
    public class ProfileManager
    {
        private string profilesDirectory;
        private string currentProfileName = "Default";
        private Dictionary<string, Dictionary<string, int>> loadedProfiles = new Dictionary<string, Dictionary<string, int>>();

        public event Action<List<string>> OnProfilesChanged;
        public event Action<string> OnProfileLoaded;

        public ProfileManager()
        {
            profilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Profiles");
            if (!Directory.Exists(profilesDirectory))
                Directory.CreateDirectory(profilesDirectory);
            LoadAllProfiles();
        }

        public List<string> GetAllProfiles()
        {
            return Directory.GetFiles(profilesDirectory, "*.ini")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();
        }

        public void CreateProfile(string profileName)
        {
            string filePath = Path.Combine(profilesDirectory, $"{profileName}.ini");
            if (File.Exists(filePath))
                throw new Exception($"Profile '{profileName}' already exists");

            var defaultBinds = new Dictionary<string, int>
            {
                { "X_key", (int)System.Windows.Input.Key.J },
                { "Y_key", (int)System.Windows.Input.Key.I },
                { "B_key", (int)System.Windows.Input.Key.K },
                { "A_key", (int)System.Windows.Input.Key.L },
                { "L_key", (int)System.Windows.Input.Key.A },
                { "R_key", (int)System.Windows.Input.Key.D },
                { "UP_key", (int)System.Windows.Input.Key.W },
                { "D_key", (int)System.Windows.Input.Key.S },
                { "LB_key", (int)System.Windows.Input.Key.Q },
                { "RB_key", (int)System.Windows.Input.Key.E },
                { "LT_key", 0 },
                { "RT_key", 0 },
                { "MENU_key", (int)System.Windows.Input.Key.Tab },
                { "MENU2_key", (int)System.Windows.Input.Key.Return },
                { "GMENU_key", 0 }
            };

            SaveProfile(profileName, defaultBinds);
            loadedProfiles[profileName] = defaultBinds;
            OnProfilesChanged?.Invoke(GetAllProfiles());
        }

        public void DeleteProfile(string profileName)
        {
            string filePath = Path.Combine(profilesDirectory, $"{profileName}.ini");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                loadedProfiles.Remove(profileName);

                if (currentProfileName == profileName)
                {
                    var profiles = GetAllProfiles();
                    currentProfileName = profiles.Count > 0 ? profiles[0] : "Default";
                }
            }

            OnProfilesChanged?.Invoke(GetAllProfiles());
        }

        public void LoadProfile(string profileName)
        {
            string filePath = Path.Combine(profilesDirectory, $"{profileName}.ini");
            if (!File.Exists(filePath))
                throw new Exception($"Profile '{profileName}' not found");

            var binds = ParseIniFile(filePath);
            loadedProfiles[profileName] = binds;
            currentProfileName = profileName;
            OnProfileLoaded?.Invoke(profileName);
        }

        public Dictionary<string, int> GetCurrentProfileBinds()
        {
            if (loadedProfiles.ContainsKey(currentProfileName))
                return loadedProfiles[currentProfileName];
            return new Dictionary<string, int>();
        }

        public string GetCurrentProfileName()
        {
            return currentProfileName;
        }

        public void SaveCurrentProfile(Dictionary<string, int> binds)
        {
            SaveProfile(currentProfileName, binds);
            loadedProfiles[currentProfileName] = binds;
        }

        private void SaveProfile(string profileName, Dictionary<string, int> binds)
        {
            string filePath = Path.Combine(profilesDirectory, $"{profileName}.ini");
            var sb = new StringBuilder();
            sb.AppendLine("[Bindings]");
            foreach (var bind in binds)
            {
                sb.AppendLine($"{bind.Key}={bind.Value}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private void LoadAllProfiles()
        {
            foreach (var profilePath in Directory.GetFiles(profilesDirectory, "*.ini"))
            {
                string profileName = Path.GetFileNameWithoutExtension(profilePath);
                var binds = ParseIniFile(profilePath);
                loadedProfiles[profileName] = binds;
            }
        }

        private Dictionary<string, int> ParseIniFile(string filePath)
        {
            var binds = new Dictionary<string, int>();
            if (!File.Exists(filePath))
                return binds;

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                    continue;

                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    if (int.TryParse(parts[1].Trim(), out int vKey))
                    {
                        binds[key] = vKey;
                    }
                }
            }

            return binds;
        }

        public string ExportProfile()
        {
            var currentBinds = GetCurrentProfileBinds();
            var sb = new StringBuilder();
            sb.AppendLine("[Bindings]");
            foreach (var bind in currentBinds)
            {
                sb.AppendLine($"{bind.Key}={bind.Value}");
            }

            return sb.ToString();
        }

        public void ImportProfile(string profileData, string profileName)
        {
            var binds = new Dictionary<string, int>();
            var lines = profileData.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                    continue;

                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    if (int.TryParse(parts[1].Trim(), out int vKey))
                    {
                        binds[key] = vKey;
                    }
                }
            }

            CreateProfile(profileName);
            SaveProfile(profileName, binds);
            loadedProfiles[profileName] = binds;
            OnProfilesChanged?.Invoke(GetAllProfiles());
        }
    }
}
