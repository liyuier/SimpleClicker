using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleClicker
{
    public partial class MainForm : Form
    {
        private bool isRunning = false;
        private int interval = 100; // 默认间隔100毫秒
        private MouseButtons clickButton = MouseButtons.Left;
        private Keys hotkey = Keys.F6; // 默认热键F6
        private bool isHotkeySetMode = false;
        private Keys tempHotkey = Keys.None; // 临时存储新热键
        private Thread clickThread = null;
        private CancellationTokenSource cancellationTokenSource = null;
        private bool lastHotkeyState = false; // 用于跟踪热键状态变化

        // Windows API 声明
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;

        private TextBox txtHotkey;
        private Button btnSetHotkey;
        private Button btnConfirmHotkey;
        private Button btnCancelHotkey;
        private Label lblStatus;
        private NumericUpDown nudInterval;
        private ComboBox cmbButton;

        // 配置文件路径
        private string configPath = Path.Combine(Application.StartupPath, "config.ini");

        public MainForm()
        {
            // 加载配置
            LoadConfig();
            
            // 直接初始化控件
            InitializeControls();
            
            // 启动全局键盘钩子
            StartGlobalHook();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
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
                                        interval = Math.Max(1, Math.Min(99999, parsedInterval));
                                    }
                                    break;
                                case "clickButton":
                                    if (int.TryParse(value, out int buttonValue))
                                    {
                                        clickButton = (MouseButtons)buttonValue;
                                    }
                                    break;
                                case "hotkey":
                                    if (int.TryParse(value, out int hotkeyValue))
                                    {
                                        hotkey = (Keys)hotkeyValue;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，使用默认值
                MessageBox.Show($"配置文件加载失败，使用默认设置: {ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                interval = 100;
                clickButton = MouseButtons.Left;
                hotkey = Keys.F6;
            }
        }

        private void SaveConfig()
        {
            try
            {
                string configContent = $"# SimpleClicker 配置文件\r\ninterval={interval}\r\nclickButton={(int)clickButton}\r\nhotkey={(int)hotkey}";
                File.WriteAllText(configPath, configContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeControls()
        {
            this.Text = "SimpleClicker - 鼠标连点器";
            this.Size = new System.Drawing.Size(400, 350);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // 间隔设置
            Label lblInterval = new Label()
            {
                Text = "连点间隔(毫秒):",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(120, 20)
            };

            nudInterval = new NumericUpDown()
            {
                Location = new System.Drawing.Point(150, 20),
                Size = new System.Drawing.Size(100, 20),
                Minimum = 1,
                Maximum = 99999,
                Value = interval
            };
            nudInterval.ValueChanged += (s, e) => 
            {
                interval = (int)nudInterval.Value;
                SaveConfig(); // 保存配置
            };

            // 连点按钮选择
            Label lblButton = new Label()
            {
                Text = "连点按键:",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(120, 20)
            };

            cmbButton = new ComboBox()
            {
                Location = new System.Drawing.Point(150, 60),
                Size = new System.Drawing.Size(100, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbButton.Items.Add("左键");
            cmbButton.Items.Add("右键");
            cmbButton.SelectedIndex = clickButton == MouseButtons.Left ? 0 : 1;
            cmbButton.SelectedIndexChanged += (s, e) =>
            {
                clickButton = cmbButton.SelectedIndex == 0 ? MouseButtons.Left : MouseButtons.Right;
                SaveConfig(); // 保存配置
            };

            // 热键设置
            Label lblHotkey = new Label()
            {
                Text = "启停热键:",
                Location = new System.Drawing.Point(20, 100),
                Size = new System.Drawing.Size(120, 20)
            };

            txtHotkey = new TextBox()
            {
                Location = new System.Drawing.Point(150, 100),
                Size = new System.Drawing.Size(100, 20),
                ReadOnly = true,
                Text = GetHotkeyString(hotkey) // 显示加载的热键
            };

            btnSetHotkey = new Button()
            {
                Text = "设置",
                Location = new System.Drawing.Point(260, 100),
                Size = new System.Drawing.Size(60, 20)
            };
            btnSetHotkey.Click += BtnSetHotkey_Click;

            btnConfirmHotkey = new Button()
            {
                Text = "确认",
                Location = new System.Drawing.Point(260, 130),
                Size = new System.Drawing.Size(60, 20)
            };
            btnConfirmHotkey.Click += BtnConfirmHotkey_Click;
            btnConfirmHotkey.Enabled = false;

            btnCancelHotkey = new Button()
            {
                Text = "取消",
                Location = new System.Drawing.Point(260, 160),
                Size = new System.Drawing.Size(60, 20)
            };
            btnCancelHotkey.Click += BtnCancelHotkey_Click;
            btnCancelHotkey.Enabled = false;

            // 状态显示
            lblStatus = new Label()
            {
                Text = "状态: 停止",
                Location = new System.Drawing.Point(20, 180),
                Size = new System.Drawing.Size(200, 20),
                ForeColor = System.Drawing.Color.Red
            };

            // 控制按钮
            Button btnStart = new Button()
            {
                Text = "开始连点",
                Location = new System.Drawing.Point(20, 220),
                Size = new System.Drawing.Size(80, 30)
            };
            btnStart.Click += (s, e) => StartClicking(lblStatus);

            Button btnStop = new Button()
            {
                Text = "停止连点",
                Location = new System.Drawing.Point(110, 220),
                Size = new System.Drawing.Size(80, 30)
            };
            btnStop.Click += (s, e) => StopClicking(lblStatus);

            // 信息提示
            Label lblInfo = new Label()
            {
                Text = "提示: 按下设置的热键可快速启停连点\n设置热键: 点击'设置' -> 按下组合键 -> 点击'确认'",
                Location = new System.Drawing.Point(20, 260),
                Size = new System.Drawing.Size(350, 40),
                ForeColor = System.Drawing.Color.Gray
            };

            // 添加所有控件到窗体
            this.Controls.AddRange(new Control[] {
                lblInterval, nudInterval,
                lblButton, cmbButton,
                lblHotkey, txtHotkey, btnSetHotkey, btnConfirmHotkey, btnCancelHotkey,
                lblStatus,
                btnStart, btnStop,
                lblInfo
            });
        }

        private void BtnSetHotkey_Click(object sender, EventArgs e)
        {
            isHotkeySetMode = true;
            tempHotkey = Keys.None;
            txtHotkey.Text = "按下组合键...";
            txtHotkey.BackColor = System.Drawing.Color.Yellow;
            btnSetHotkey.Enabled = false;
            btnConfirmHotkey.Enabled = false;
            btnCancelHotkey.Enabled = true;
        }

        private void BtnConfirmHotkey_Click(object sender, EventArgs e)
        {
            if (tempHotkey != Keys.None)
            {
                hotkey = tempHotkey;
                txtHotkey.Text = GetHotkeyString(hotkey);
                txtHotkey.BackColor = System.Drawing.SystemColors.Window;
                isHotkeySetMode = false;
                btnSetHotkey.Enabled = true;
                btnConfirmHotkey.Enabled = false;
                btnCancelHotkey.Enabled = false;
                
                SaveConfig(); // 保存配置
            }
        }

        private void BtnCancelHotkey_Click(object sender, EventArgs e)
        {
            txtHotkey.Text = GetHotkeyString(hotkey);
            txtHotkey.BackColor = System.Drawing.SystemColors.Window;
            isHotkeySetMode = false;
            btnSetHotkey.Enabled = true;
            btnConfirmHotkey.Enabled = false;
            btnCancelHotkey.Enabled = false;
        }

        private void StartGlobalHook()
        {
            // 启动一个线程来监听键盘事件
            Thread hookThread = new Thread(() =>
            {
                bool[] previousKeyStates = new bool[256]; // 存储之前的按键状态
                
                while (!this.IsDisposed)
                {
                    if (isHotkeySetMode)
                    {
                        // 检查当前所有按键状态
                        Keys currentCombo = Keys.None;
                        bool anyModifierPressed = false;
                        
                        // 检查修饰键
                        bool ctrlPressed = (GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0;
                        bool shiftPressed = (GetAsyncKeyState(Keys.ShiftKey) & 0x8000) != 0;
                        bool altPressed = (GetAsyncKeyState(Keys.Menu) & 0x8000) != 0;
                        
                        if (ctrlPressed) 
                        {
                            currentCombo |= Keys.Control;
                            anyModifierPressed = true;
                        }
                        if (shiftPressed) 
                        {
                            currentCombo |= Keys.Shift;
                            anyModifierPressed = true;
                        }
                        if (altPressed) 
                        {
                            currentCombo |= Keys.Alt;
                            anyModifierPressed = true;
                        }
                        
                        // 检查普通键
                        Keys detectedKey = Keys.None;
                        for (int i = 0; i < 256; i++)
                        {
                            short keyState = GetAsyncKeyState((Keys)i);
                            bool isCurrentPressed = (keyState & 0x8000) != 0;
                            
                            // 检测按键释放事件
                            if (previousKeyStates[i] && !isCurrentPressed)
                            {
                                // 如果有修饰键被按下，而普通键被释放，则保存组合键
                                if (anyModifierPressed && i >= 1 && i <= 254 && 
                                    (Keys)i != Keys.ControlKey && (Keys)i != Keys.ShiftKey && (Keys)i != Keys.Menu)
                                {
                                    detectedKey = (Keys)i;
                                    break;
                                }
                            }
                            
                            // 保存当前状态供下次比较
                            previousKeyStates[i] = isCurrentPressed;
                        }
                        
                        // 如果检测到组合键释放
                        if (detectedKey != Keys.None)
                        {
                            tempHotkey = detectedKey | currentCombo;
                            
                            // 更新UI
                            this.Invoke((MethodInvoker)delegate
                            {
                                txtHotkey.Text = GetHotkeyString(tempHotkey);
                                btnConfirmHotkey.Enabled = true;
                            });
                        }
                        else if (anyModifierPressed || ctrlPressed || shiftPressed || altPressed)
                        {
                            // 显示当前按下的组合键
                            string displayText = "";
                            if (ctrlPressed) displayText += "Ctrl + ";
                            if (shiftPressed) displayText += "Shift + ";
                            if (altPressed) displayText += "Alt + ";
                            
                            // 寻找当前按下的普通键
                            for (int i = 0; i < 256; i++)
                            {
                                if (i != (int)Keys.ControlKey && i != (int)Keys.ShiftKey && i != (int)Keys.Menu)
                                {
                                    if ((GetAsyncKeyState((Keys)i) & 0x8000) != 0)
                                    {
                                        displayText += ((Keys)i).ToString();
                                        break;
                                    }
                                }
                            }
                            
                            if (!string.IsNullOrEmpty(displayText) && !displayText.EndsWith(" + "))
                            {
                                this.Invoke((MethodInvoker)delegate
                                {
                                    txtHotkey.Text = displayText;
                                });
                            }
                        }
                    }
                    else
                    {
                        // 检查是否按下了热键（按下并抬起才算一次完整的操作）
                        bool currentHotkeyState = IsKeyPressed(hotkey);
                        
                        // 当热键从按下变为释放时（即按下并抬起），切换连点状态
                        if (lastHotkeyState && !currentHotkeyState)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                if (lblStatus != null)
                                {
                                    if (isRunning)
                                        StopClicking(lblStatus);
                                    else
                                        StartClicking(lblStatus);
                                }
                            });
                        }
                        
                        lastHotkeyState = currentHotkeyState;
                    }
                    
                    Thread.Sleep(10);
                }
            });
            hookThread.IsBackground = true;
            hookThread.Start();
        }

        private string GetHotkeyString(Keys key)
        {
            string result = "";
            
            if ((key & Keys.Control) == Keys.Control)
                result += "Ctrl + ";
            if ((key & Keys.Shift) == Keys.Shift)
                result += "Shift + ";
            if ((key & Keys.Alt) == Keys.Alt)
                result += "Alt + ";
            
            // 获取按键名称（去掉修饰符）
            Keys actualKey = key & ~Keys.Modifiers;
            result += actualKey.ToString();
            
            return result;
        }

        private bool IsKeyPressed(Keys hotkey)
        {
            Keys actualKey = hotkey & ~Keys.Modifiers;
            bool isCtrlPressed = ((hotkey & Keys.Control) == Keys.Control) ? 
                (GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0 : true;
            bool isShiftPressed = ((hotkey & Keys.Shift) == Keys.Shift) ? 
                (GetAsyncKeyState(Keys.ShiftKey) & 0x8000) != 0 : true;
            bool isAltPressed = ((hotkey & Keys.Alt) == Keys.Alt) ? 
                (GetAsyncKeyState(Keys.Menu) & 0x8000) != 0 : true;
            bool isActualKeyPressed = (GetAsyncKeyState(actualKey) & 0x8000) != 0;
            
            return isCtrlPressed && isShiftPressed && isAltPressed && isActualKeyPressed;
        }

        private void StartClicking(Label statusLabel)
        {
            if (isRunning) return;
            
            isRunning = true;
            statusLabel.Text = "状态: 运行中";
            statusLabel.ForeColor = System.Drawing.Color.Green;
            
            cancellationTokenSource = new CancellationTokenSource();
            
            clickThread = new Thread(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested && isRunning)
                {
                    try
                    {
                        await Task.Delay(interval, cancellationTokenSource.Token);
                        
                        if (!isRunning) break;
                        
                        // 执行点击操作
                        PerformMouseClick();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
            clickThread.IsBackground = true;
            clickThread.Start();
        }

        private void StopClicking(Label statusLabel)
        {
            if (!isRunning) return;
            
            isRunning = false;
            statusLabel.Text = "状态: 停止";
            statusLabel.ForeColor = System.Drawing.Color.Red;
            
            cancellationTokenSource?.Cancel();
        }

        private void PerformMouseClick()
        {
            if (clickButton == MouseButtons.Left)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }
            else if (clickButton == MouseButtons.Right)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveConfig(); // 关闭前保存配置
            StopClicking(null);
            base.OnFormClosing(e);
        }
    }

    // 程序入口
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}