using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleClicker
{
    public partial class MainForm : Form
    {
        private bool isRunning = false;
        private AppConfig config;
        private bool isHotkeySetMode = false;
        private Keys tempHotkey = Keys.None; // 临时存储新热键
        private Thread clickThread = null;
        private CancellationTokenSource cancellationTokenSource = null;
        private GlobalKeyboardHook keyboardHook;

        private TextBox txtHotkey;
        private Button btnSetHotkey;
        private Button btnConfirmHotkey;
        private Button btnCancelHotkey;
        private Label lblStatus;
        private NumericUpDown nudInterval;
        private ComboBox cmbButton;

        public MainForm()
        {
            // 加载配置
            config = ConfigManager.Load();
            
            // 初始化控件
            InitializeControls();
            
            // 启动全局键盘钩子
            keyboardHook = new GlobalKeyboardHook(
                config,
                OnHotkeyToggle,
                UpdateHotkeyDisplay,
                () => isHotkeySetMode,
                OnKeyDetected
            );
        }

        private void OnHotkeyToggle(bool state)
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

        private void UpdateHotkeyDisplay(string text)
        {
            this.Invoke((MethodInvoker)delegate
            {
                txtHotkey.Text = text;
            });
        }

        private void OnKeyDetected(Keys key)
        {
            tempHotkey = key;
            this.Invoke((MethodInvoker)delegate
            {
                txtHotkey.Text = GetHotkeyString(tempHotkey);
                btnConfirmHotkey.Enabled = true;
            });
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
                Value = config.Interval
            };
            nudInterval.ValueChanged += (s, e) => 
            {
                config.Interval = (int)nudInterval.Value;
                ConfigManager.Save(config); // 保存配置
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
            cmbButton.SelectedIndex = config.ClickButton == MouseButtons.Left ? 0 : 1;
            cmbButton.SelectedIndexChanged += (s, e) =>
            {
                config.ClickButton = cmbButton.SelectedIndex == 0 ? MouseButtons.Left : MouseButtons.Right;
                ConfigManager.Save(config); // 保存配置
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
                Text = GetHotkeyString(config.Hotkey) // 显示加载的热键
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
                config.Hotkey = tempHotkey;
                txtHotkey.Text = GetHotkeyString(config.Hotkey);
                txtHotkey.BackColor = System.Drawing.SystemColors.Window;
                isHotkeySetMode = false;
                btnSetHotkey.Enabled = true;
                btnConfirmHotkey.Enabled = false;
                btnCancelHotkey.Enabled = false;
                
                ConfigManager.Save(config); // 保存配置
            }
        }

        private void BtnCancelHotkey_Click(object sender, EventArgs e)
        {
            txtHotkey.Text = GetHotkeyString(config.Hotkey);
            txtHotkey.BackColor = System.Drawing.SystemColors.Window;
            isHotkeySetMode = false;
            btnSetHotkey.Enabled = true;
            btnConfirmHotkey.Enabled = false;
            btnCancelHotkey.Enabled = false;
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
                        await Task.Delay(config.Interval, cancellationTokenSource.Token);
                        
                        if (!isRunning) break;
                        
                        // 执行点击操作
                        MouseController.Click(config.ClickButton);
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ConfigManager.Save(config); // 关闭前保存配置
            keyboardHook?.Dispose();
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