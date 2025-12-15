using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SimpleClicker
{
    public class GlobalKeyboardHook
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        private bool _disposed = false;
        private Thread _hookThread;
        private AppConfig _config;
        private Action<bool> _onHotkeyToggle;
        private Action<string> _onUpdateHotkeyDisplay;
        private Func<bool> _isHotkeySetMode;
        private Action<Keys> _onKeyDetected;

        public GlobalKeyboardHook(AppConfig config, Action<bool> onHotkeyToggle, 
                                 Action<string> onUpdateHotkeyDisplay, Func<bool> isHotkeySetMode, Action<Keys> onKeyDetected)
        {
            _config = config;
            _onHotkeyToggle = onHotkeyToggle;
            _onUpdateHotkeyDisplay = onUpdateHotkeyDisplay;
            _isHotkeySetMode = isHotkeySetMode;
            _onKeyDetected = onKeyDetected;
            
            Start();
        }

        public void Start()
        {
            _hookThread = new Thread(HookLoop)
            {
                IsBackground = true
            };
            _hookThread.Start();
        }

        private void HookLoop()
        {
            bool[] previousKeyStates = new bool[256]; // 存储之前的按键状态
            bool lastHotkeyState = false; // 用于跟踪热键状态变化
            
            while (!_disposed)
            {
                if (_isHotkeySetMode())
                {
                    ProcessHotkeySetting(previousKeyStates);
                }
                else
                {
                    ProcessNormalMode(ref lastHotkeyState);
                }
                
                Thread.Sleep(10);
            }
        }

        private void ProcessHotkeySetting(bool[] previousKeyStates)
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
                Keys finalCombo = detectedKey | currentCombo;
                _onKeyDetected(finalCombo);
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
                    _onUpdateHotkeyDisplay(displayText);
                }
            }
        }

        private void ProcessNormalMode(ref bool lastHotkeyState)
        {
            // 检查是否按下了热键（按下并抬起才算一次完整的操作）
            bool currentHotkeyState = IsKeyPressed(_config.Hotkey);
            
            // 当热键从按下变为释放时（即按下并抬起），切换连点状态
            if (lastHotkeyState && !currentHotkeyState)
            {
                _onHotkeyToggle(true);
            }
            
            lastHotkeyState = currentHotkeyState;
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

        public void Dispose()
        {
            _disposed = true;
            _hookThread?.Join(100); // 等待线程结束，最多等待100ms
        }
    }
}