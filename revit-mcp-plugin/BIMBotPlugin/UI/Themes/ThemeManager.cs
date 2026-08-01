using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;

namespace BIMBotPlugin.UI.Themes
{
    public enum ThemeMode
    {
        Dark,
        Light
    }

    /// <summary>
    /// Central manager for application theme mode, persistence, and dynamic updates.
    /// </summary>
    public static class ThemeManager
    {
        private static ThemeMode _currentTheme = ThemeMode.Dark;
        public static event EventHandler<ThemeMode>? ThemeChanged;

        static ThemeManager()
        {
            LoadTheme();
        }

        public static ThemeMode CurrentTheme
        {
            get => _currentTheme;
            set => SetTheme(value);
        }

        public static bool IsDarkMode => CurrentTheme == ThemeMode.Dark;

        public static void SetTheme(ThemeMode mode)
        {
            if (_currentTheme == mode) return;
            _currentTheme = mode;
            SaveTheme();
            ThemeChanged?.Invoke(null, _currentTheme);
        }

        public static void ApplyTheme(Window window)
        {
            if (window == null) return;
            if (CurrentTheme == ThemeMode.Light)
                LightTheme.Apply(window);
            else
                DarkTheme.Apply(window);
        }

        // Active Palette Properties
        public static SolidColorBrush BgCanvas => IsDarkMode ? DarkTheme.BgDark : LightTheme.BgDark;
        public static SolidColorBrush BgCard => IsDarkMode ? DarkTheme.BgCard : LightTheme.BgCard;
        public static SolidColorBrush BgCardHover => IsDarkMode ? DarkTheme.BgCardHover : LightTheme.BgCardHover;
        public static SolidColorBrush BgHeader => IsDarkMode ? DarkTheme.BgHeader : LightTheme.BgHeader;
        public static SolidColorBrush BgInput => IsDarkMode ? DarkTheme.BgInput : LightTheme.BgInput;
        public static SolidColorBrush BgFooter => IsDarkMode ? DarkTheme.BgFooter : LightTheme.BgFooter;

        public static SolidColorBrush BgAccent => IsDarkMode ? DarkTheme.BgAccent : LightTheme.BgAccent;
        public static SolidColorBrush BgAccentHover => IsDarkMode ? DarkTheme.BgAccentHover : LightTheme.BgAccentHover;
        public static SolidColorBrush BgCancel => IsDarkMode ? DarkTheme.BgCancel : LightTheme.BgCancel;
        public static SolidColorBrush BgCancelHover => IsDarkMode ? DarkTheme.BgCancelHover : LightTheme.BgCancelHover;

        public static SolidColorBrush BgWarning => IsDarkMode ? DarkTheme.BgWarning : LightTheme.BgWarning;
        public static SolidColorBrush BgInfo => IsDarkMode ? DarkTheme.BgInfo : LightTheme.BgInfo;
        public static SolidColorBrush BgDanger => IsDarkMode ? DarkTheme.BgDanger : LightTheme.BgDanger;
        public static SolidColorBrush BgDeep => IsDarkMode ? DarkTheme.BgDeep : LightTheme.BgDeep;

        public static SolidColorBrush FgPrimary => IsDarkMode ? DarkTheme.FgWhite : LightTheme.FgWhite;
        public static SolidColorBrush FgSecondary => IsDarkMode ? DarkTheme.FgLight : LightTheme.FgLight;
        public static SolidColorBrush FgDim => IsDarkMode ? DarkTheme.FgDim : LightTheme.FgDim;
        public static SolidColorBrush FgGreen => IsDarkMode ? DarkTheme.FgGreen : LightTheme.FgGreen;
        public static SolidColorBrush FgGold => IsDarkMode ? DarkTheme.FgGold : LightTheme.FgGold;
        public static SolidColorBrush FgWarning => IsDarkMode ? DarkTheme.FgWarning : LightTheme.FgWarning;

        public static SolidColorBrush BorderDim => IsDarkMode ? DarkTheme.BorderDim : LightTheme.BorderDim;
        public static SolidColorBrush BorderAccent => IsDarkMode ? DarkTheme.BorderAccent : LightTheme.BorderAccent;
        public static SolidColorBrush BorderFocus => IsDarkMode ? DarkTheme.BorderFocus : LightTheme.BorderFocus;

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BIMBotPlugin",
            "theme_settings.json");

        private static void SaveTheme()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var dto = new ThemeSettingsDto { Theme = _currentTheme.ToString() };
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(dto, Formatting.Indented));
            }
            catch { }
        }

        private static void LoadTheme()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var dto = JsonConvert.DeserializeObject<ThemeSettingsDto>(json);
                    if (dto != null && Enum.TryParse<ThemeMode>(dto.Theme, out var mode))
                    {
                        _currentTheme = mode;
                    }
                }
            }
            catch { }
        }

        private class ThemeSettingsDto
        {
            public string Theme { get; set; } = "Dark";
        }
    }
}
