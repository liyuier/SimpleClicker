using System.Windows.Forms;

namespace SimpleClicker
{
    public class AppConfig
    {
        public int Interval { get; set; } = 100;
        public MouseButtons ClickButton { get; set; } = MouseButtons.Left;
        public Keys Hotkey { get; set; } = Keys.F6;
    }

    public static class ConfigManager
    {
        private static readonly string configPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        public static AppConfig Load()
        {
            var config = new AppConfig();
            try
            {
                if (System.IO.File.Exists(configPath))
                {
                    string[] lines = System.IO.File.ReadAllLines(configPath);
                    foreach (string line in lines)
                    {
                        // 跳过空行和注释行
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                            continue;
                            
                        string[] parts = line.Split(new char[] {'='}, 2); // 最多分割成两部分
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();
                            
                            switch (key)
                            {
                                case "interval":
                                    if (int.TryParse(value, out int parsedInterval))
                                    {
                                        config.Interval = System.Math.Max(1, System.Math.Min(99999, parsedInterval));
                                    }
                                    break;
                                case "clickButton":
                                    if (int.TryParse(value, out int buttonValue))
                                    {
                                        config.ClickButton = (MouseButtons)buttonValue;
                                    }
                                    break;
                                case "hotkey":
                                    if (int.TryParse(value, out int hotkeyValue))
                                    {
                                        config.Hotkey = (Keys)hotkeyValue;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                // 如果加载失败，返回默认配置
                System.Windows.Forms.MessageBox.Show($"配置文件加载失败，使用默认设置: {ex.Message}", "提示", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
            return config;
        }

        public static void Save(AppConfig config)
        {
            try
            {
                string configContent = $"# SimpleClicker 配置文件\r\ninterval={config.Interval}\r\nclickButton={(int)config.ClickButton}\r\nhotkey={(int)config.Hotkey}";
                System.IO.File.WriteAllText(configPath, configContent);
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"保存配置失败: {ex.Message}", "错误", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}