using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Forms;
using ESP32_DS4_Controller_WPF.Core;

namespace ESP32_DS4_Controller_WPF
{
    public partial class MainWindow : Window
    {
        private SerialComm serialComm;
        private GamepadController gamepadController;
        private RawInputHandler rawInputHandler;
        private KeyBindManager keyBindManager;
        private ProfileManager profileManager;
        private bool isConnected = false;
        private bool isEmulationActive = false;
        private int powerVKey = -1;
        private HashSet<int> blockedVKeys = new HashSet<int>();
        private int leftStickX = 0;
        private int leftStickY = 0;
        private int rightStickX = 0;
        private int rightStickY = 0;
        private System.Windows.Controls.TextBox currentBindingTextBox = null;

        // Mouse settings
        private const float MOUSE_SENSITIVITY = 0.5f;
        private const int MOUSE_DEADZONE = 15;
        private bool isMouseEmulatingRightStick = false;

        public MainWindow()
        {
            InitializeComponent();
            serialComm = new SerialComm();
            serialComm.OnLog += SerialComm_OnLog;
            gamepadController = new GamepadController(serialComm);
            keyBindManager = new KeyBindManager();
            profileManager = new ProfileManager();
            profileManager.OnProfilesChanged += ProfileManager_OnProfilesChanged;
            profileManager.OnProfileLoaded += ProfileManager_OnProfileLoaded;
            RefreshComPorts();
            RefreshProfiles();
            LogMessage("[System] Application started");
            DisplayCurrentBindings();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            rawInputHandler = new RawInputHandler(hwndSource.Handle);
            rawInputHandler.OnKeyEvent += RawInputHandler_OnKeyEvent;
            rawInputHandler.OnMouseMove += RawInputHandler_OnMouseMove;
            hwndSource.AddHook(WndProc);
            InitializePowerButton();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;
            if (msg == WM_INPUT)
            {
                rawInputHandler.ProcessRawInput(lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void InitializePowerButton()
        {
            var powerBinding = keyBindManager.GetBindingByName("Power");
            if (powerBinding != null)
            {
                powerVKey = powerBinding.VKey;
                LogMessage($"[Power] Bound to VKey: {powerVKey} ({RawInputHandler.VKeyToString(powerVKey)})");
            }
            else
            {
                LogMessage("[Power] WARNING: Power button not bound!");
            }
            UpdateBlockedVKeys();
        }

        private void UpdateBlockedVKeys()
        {
            blockedVKeys.Clear();
            var bindingNames = new[] { "X_key", "Y_key", "B_key", "A_key",
                "L_key", "R_key", "UP_key", "D_key",
                "LB_key", "RB_key", "LT_key", "RT_key",
                "MENU_key", "MENU2_key", "GMENU_key" };
            foreach (var name in bindingNames)
            {
                var binding = keyBindManager.GetBindingByName(name);
                if (binding != null && binding.VKey > 0)
                {
                    blockedVKeys.Add(binding.VKey);
                }
            }
            LogMessage($"[Power] Loaded {blockedVKeys.Count} blocked VKeys");
        }

        private void ToggleEmulation()
        {
            if (!isConnected)
            {
                LogMessage("[Error] ESP32 not connected!");
                return;
            }

            isEmulationActive = !isEmulationActive;
            if (isEmulationActive)
            {
                LogMessage("[Emulation] STARTED - Input blocked");
                gamepadController.SendStick(0, 0, 0, 0);
                isMouseEmulatingRightStick = true;
            }
            else
            {
                LogMessage("[Emulation] STOPPED - Input restored");
                gamepadController.ResetGamepad();
                leftStickX = 0;
                leftStickY = 0;
                rightStickX = 0;
                rightStickY = 0;
                isMouseEmulatingRightStick = false;
            }
        }

        private void RawInputHandler_OnKeyEvent(int vKey, bool isPressed)
        {
            Dispatcher.Invoke(() =>
            {
                if (vKey == powerVKey && isPressed)
                {
                    ToggleEmulation();
                    LogMessage($"[Power] Emulation toggled: {isEmulationActive}");
                    return;
                }

                if (currentBindingTextBox != null && isPressed)
                {
                    string keyName = RawInputHandler.VKeyToString(vKey);
                    currentBindingTextBox.Text = keyName;
                    string bindName = currentBindingTextBox.Name;
                    DeterminBindTypeAndSet(bindName, vKey);
                    OnKeyBindingChanged();
                    currentBindingTextBox.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(35, 35, 35));
                    System.Windows.Input.Keyboard.ClearFocus();
                    currentBindingTextBox = null;
                    LogMessage($"[Bind] {bindName} = {keyName} (VKey: {vKey})");

                    if (bindName == "Power")
                    {
                        powerVKey = vKey;
                    }
                    UpdateBlockedVKeys();
                    return;
                }

                if (isEmulationActive && blockedVKeys.Contains(vKey))
                {
                    var binding = keyBindManager.GetBindingByVKey(vKey);
                    if (binding != null)
                    {
                        ProcessKeyBinding(binding, isPressed);
                    }
                    return;
                }

                if (!this.IsActive)
                    return;

                if (!isEmulationActive)
                {
                    return;
                }
            });
        }

        private void RawInputHandler_OnMouseMove(int deltaX, int deltaY)
        {
            if (!isEmulationActive || !isMouseEmulatingRightStick)
                return;

            Dispatcher.Invoke(() =>
            {
                ApplyMouseToRightStick(deltaX, deltaY);
            });
        }

        private void ApplyMouseToRightStick(int deltaX, int deltaY)
        {
            int newRightStickX = (int)(deltaX * MOUSE_SENSITIVITY);
            int newRightStickY = (int)(deltaY * MOUSE_SENSITIVITY);

            if (Math.Abs(newRightStickX) < MOUSE_DEADZONE)
                newRightStickX = 0;
            if (Math.Abs(newRightStickY) < MOUSE_DEADZONE)
                newRightStickY = 0;

            newRightStickX = Math.Max(-100, Math.Min(100, newRightStickX));
            newRightStickY = Math.Max(-100, Math.Min(100, newRightStickY));

            rightStickX = newRightStickX;
            rightStickY = newRightStickY;

            gamepadController.SendStick((sbyte)leftStickX, (sbyte)leftStickY, (sbyte)rightStickX, (sbyte)rightStickY);
        }

        private void DeterminBindTypeAndSet(string bindName, int vKey)
        {
            byte bindType = KeyBindManager.BIND_TYPE_BUTTON;
            ushort button = 0;
            int stickDir = 0;

            switch (bindName)
            {
                case "X_key":
                    bindType = KeyBindManager.BIND_TYPE_BUTTON;
                    button = GamepadController.BTN_X;
                    break;
                case "Y_key":
                    bindType = KeyBindManager.BIND_TYPE_BUTTON;
                    button = GamepadController.BTN_SQUARE;
                    break;
                case "B_key":
                    bindType = KeyBindManager.BIND_TYPE_BUTTON;
                    button = GamepadController.BTN_CIRCLE;
                    break;
                case "A_key":
                    bindType = KeyBindManager.BIND_TYPE_BUTTON;
                    button = GamepadController.BTN_TRIANGLE;
                    break;
                case "UP_key":
                    bindType = KeyBindManager.BIND_TYPE_LEFT_STICK;
                    stickDir = KeyBindManager.STICK_Y_POSITIVE;
                    break;
                case "D_key":
                    bindType = KeyBindManager.BIND_TYPE_LEFT_STICK;
                    stickDir = KeyBindManager.STICK_Y_NEGATIVE;
                    break;
                case "L_key":
                    bindType = KeyBindManager.BIND_TYPE_LEFT_STICK;
                    stickDir = KeyBindManager.STICK_X_NEGATIVE;
                    break;
                case "R_key":
                    bindType = KeyBindManager.BIND_TYPE_LEFT_STICK;
                    stickDir = KeyBindManager.STICK_X_POSITIVE;
                    break;
                case "LB_key":
                    bindType = KeyBindManager.BIND_TYPE_BUTTON;
                    button = GamepadController.BTN_L1;
                    break;
                case "RB_key":
                    bindType = KeyBindManager.BIND_TYPE_BUTTON;
                    button = GamepadController.BTN_R1;
                    break;
                case "LT_key":
                    bindType = KeyBindManager.BIND_TYPE_TRIGGER;
                    button = 0;
                    break;
                case "RT_key":
                    bindType = KeyBindManager.BIND_TYPE_TRIGGER;
                    button = 1;
                    break;
                case "MENU_key":
                case "MENU2_key":
                case "GMENU_key":
                case "Power":
                    bindType = KeyBindManager.BIND_TYPE_BUTTON;
                    button = 0;
                    break;
            }

            keyBindManager.SetBinding(bindName, vKey, bindType, button, stickDir);
        }

        private void ProcessKeyBinding(KeyBindManager.KeyBinding binding, bool isPressed)
        {
            switch (binding.BindType)
            {
                case KeyBindManager.BIND_TYPE_BUTTON:
                    if (isPressed)
                        gamepadController.PressButton(binding.GamepadButton);
                    else
                        gamepadController.ReleaseButton(binding.GamepadButton);
                    break;
                case KeyBindManager.BIND_TYPE_LEFT_STICK:
                    ProcessStickBinding(binding, isPressed, isLeftStick: true);
                    break;
                case KeyBindManager.BIND_TYPE_RIGHT_STICK:
                    ProcessStickBinding(binding, isPressed, isLeftStick: false);
                    break;
                case KeyBindManager.BIND_TYPE_TRIGGER:
                    if (binding.GamepadButton == 0)
                    {
                        if (isPressed)
                            gamepadController.SetTriggers(255, (byte)gamepadController.GetLastCommand().R2);
                        else
                            gamepadController.SetTriggers(0, (byte)gamepadController.GetLastCommand().R2);
                    }
                    else if (binding.GamepadButton == 1)
                    {
                        if (isPressed)
                            gamepadController.SetTriggers((byte)gamepadController.GetLastCommand().L2, 255);
                        else
                            gamepadController.SetTriggers((byte)gamepadController.GetLastCommand().L2, 0);
                    }
                    break;
            }
        }

        private void ProcessStickBinding(KeyBindManager.KeyBinding binding, bool isPressed, bool isLeftStick)
        {
            if (isLeftStick)
            {
                switch (binding.StickDirection)
                {
                    case KeyBindManager.STICK_X_POSITIVE:
                        if (isPressed) leftStickX = 100;
                        else leftStickX = 0;
                        break;
                    case KeyBindManager.STICK_X_NEGATIVE:
                        if (isPressed) leftStickX = -100;
                        else leftStickX = 0;
                        break;
                    case KeyBindManager.STICK_Y_POSITIVE:
                        if (isPressed) leftStickY = -100;
                        else leftStickY = 0;
                        break;
                    case KeyBindManager.STICK_Y_NEGATIVE:
                        if (isPressed) leftStickY = 100;
                        else leftStickY = 0;
                        break;
                }
                gamepadController.SendStick((sbyte)leftStickX, (sbyte)leftStickY, (sbyte)rightStickX, (sbyte)rightStickY);
            }
            else
            {
                switch (binding.StickDirection)
                {
                    case KeyBindManager.STICK_X_POSITIVE:
                        if (isPressed) rightStickX = 100;
                        else rightStickX = 0;
                        break;
                    case KeyBindManager.STICK_X_NEGATIVE:
                        if (isPressed) rightStickX = -100;
                        else rightStickX = 0;
                        break;
                    case KeyBindManager.STICK_Y_POSITIVE:
                        if (isPressed) rightStickY = -100;
                        else rightStickY = 0;
                        break;
                    case KeyBindManager.STICK_Y_NEGATIVE:
                        if (isPressed) rightStickY = 100;
                        else rightStickY = 0;
                        break;
                }
                gamepadController.SendStick((sbyte)leftStickX, (sbyte)leftStickY, (sbyte)rightStickX, (sbyte)rightStickY);
            }
        }

        private void DisplayCurrentBindings()
        {
            var bindings = keyBindManager.GetAllBindings();
            foreach (var binding in bindings)
            {
                LoadBindingToTextBox(binding.BindName);
            }
        }

        private void LoadBindingToTextBox(string bindName)
        {
            var binding = keyBindManager.GetBindingByName(bindName);
            if (binding != null)
            {
                try
                {
                    var textBox = this.FindName(bindName) as System.Windows.Controls.TextBox;
                    if (textBox != null)
                    {
                        textBox.Text = RawInputHandler.VKeyToString(binding.VKey);
                    }
                }
                catch { }
            }
        }

        private void KeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null && currentBindingTextBox == null)
            {
                currentBindingTextBox = textBox;
                textBox.Text = "Press any key...";
                textBox.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 100, 0));
                LogMessage($"[Bind] Waiting for input on {textBox.Name}...");
                e.Handled = true;
            }
        }

        private void KeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            if (textBox != null)
            {
                if (textBox.Background is System.Windows.Media.SolidColorBrush brush &&
                    brush.Color == System.Windows.Media.Color.FromRgb(255, 100, 0))
                {
                    textBox.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(35, 35, 35));
                }

                if (textBox == currentBindingTextBox)
                {
                    currentBindingTextBox = null;
                }
            }
        }

        private void RefreshProfiles()
        {
            var profiles = profileManager.GetAllProfiles();
            ProfileListBox.Items.Clear();
            foreach (var profile in profiles)
            {
                ProfileListBox.Items.Add(profile);
            }

            if (ProfileListBox.Items.Count > 0)
            {
                ProfileListBox.SelectedIndex = 0;
            }
        }

        private void ProfileManager_OnProfilesChanged(List<string> profiles)
        {
            Dispatcher.Invoke(() => RefreshProfiles());
        }

        private void ProfileManager_OnProfileLoaded(string profileName)
        {
            Dispatcher.Invoke(() =>
            {
                LogMessage($"[Profile] Loaded: {profileName}");
                LoadBindingsFromProfile(profileName);
            });
        }

        private void LoadBindingsFromProfile(string profileName)
        {
            var binds = profileManager.GetCurrentProfileBinds();
            foreach (var bind in binds)
            {
                try
                {
                    var textBox = this.FindName(bind.Key) as System.Windows.Controls.TextBox;
                    if (textBox != null)
                    {
                        textBox.Text = RawInputHandler.VKeyToString(bind.Value);
                    }
                }
                catch { }
            }
        }

        private void SaveCurrentProfileBindings()
        {
            var binds = new Dictionary<string, int>();
            var bindNames = new[] { "X_key", "Y_key", "B_key", "A_key", "L_key", "R_key", "UP_key", "D_key",
                "LB_key", "RB_key", "LT_key", "RT_key", "MENU_key", "MENU2_key", "GMENU_key", "Power" };
            foreach (var bindName in bindNames)
            {
                var binding = keyBindManager.GetBindingByName(bindName);
                if (binding != null)
                {
                    binds[bindName] = binding.VKey;
                }
                else
                {
                    binds[bindName] = 0;
                }
            }
            profileManager.SaveCurrentProfile(binds);
        }

        private void ProfileListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProfileListBox.SelectedItem == null)
                return;

            string selectedProfile = ProfileListBox.SelectedItem.ToString();

            try
            {
                profileManager.LoadProfile(selectedProfile);
                LogMessage($"[Profile] Loaded: {selectedProfile}");
            }
            catch (Exception ex)
            {
                LogMessage($"[Error] Failed to load profile: {ex.Message}");
            }
        }

        private void NewProfileButton_Click(object sender, RoutedEventArgs e)
        {
            int profileCount = profileManager.GetAllProfiles().Count + 1;
            string newProfileName = $"Profile_{profileCount}";
            try
            {
                profileManager.CreateProfile(newProfileName);
                LogMessage($"[Profile] Created: {newProfileName}");
            }
            catch (Exception ex)
            {
                LogMessage($"[Error] {ex.Message}");
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileListBox.SelectedItem == null)
            {
                LogMessage("[Error] Select profile first");
                return;
            }

            string profileToDelete = ProfileListBox.SelectedItem.ToString();
            try
            {
                profileManager.DeleteProfile(profileToDelete);
                LogMessage($"[Profile] Deleted: {profileToDelete}");
            }
            catch (Exception ex)
            {
                LogMessage($"[Error] {ex.Message}");
            }
        }

        private void ExportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveCurrentProfileBindings();
                string exportData = profileManager.ExportProfile();
                System.Windows.Forms.Clipboard.SetText(exportData);
                LogMessage("[Profile] Exported to clipboard");
            }
            catch (Exception ex)
            {
                LogMessage($"[Error] {ex.Message}");
            }
        }

        private void ImportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string clipboardData = System.Windows.Forms.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(clipboardData))
                {
                    LogMessage("[Error] Clipboard is empty");
                    return;
                }

                int profileCount = profileManager.GetAllProfiles().Count + 1;
                string newProfileName = $"Imported_{profileCount}";
                profileManager.ImportProfile(clipboardData, newProfileName);
                LogMessage($"[Profile] Imported: {newProfileName}");
            }
            catch (Exception ex)
            {
                LogMessage($"[Error] {ex.Message}");
            }
        }

        private void OnKeyBindingChanged()
        {
            SaveCurrentProfileBindings();
        }

        private void RefreshComPorts()
        {
            string[] ports = SerialComm.GetAvailablePorts();
            ComPortCombo.Items.Clear();
            foreach (string port in ports)
            {
                ComPortCombo.Items.Add(port);
            }

            if (ComPortCombo.Items.Count > 0)
                ComPortCombo.SelectedIndex = ComPortCombo.Items.Count - 1;
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                DebugConsole.AppendText($"{DateTime.Now:HH:mm:ss} {message}\n");
                DebugConsole.ScrollToEnd();
            });
        }

        private void SerialComm_OnLog(string message)
        {
            LogMessage(message);
        }

        private void TestButton_Up(object sender, RoutedEventArgs e)
        {
            gamepadController.SendStick(0, -100, 0, 0);
        }

        private void TestButton_Down(object sender, RoutedEventArgs e)
        {
            gamepadController.SendStick(0, 100, 0, 0);
        }

        private void TestButton_Left(object sender, RoutedEventArgs e)
        {
            gamepadController.SendStick(-100, 0, 0, 0);
        }

        private void TestButton_Right(object sender, RoutedEventArgs e)
        {
            gamepadController.SendStick(100, 0, 0, 0);
        }

        private void TestButton_PressX(object sender, RoutedEventArgs e)
        {
            gamepadController.PressButton(GamepadController.BTN_X);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ComPortCombo.SelectedItem == null)
            {
                LogMessage("[Error] Select COM port first");
                return;
            }

            string comPort = ComPortCombo.SelectedItem.ToString();
            if (!isConnected)
            {
                bool success = serialComm.Connect(comPort, 115200);
                if (success)
                {
                    gamepadController.Start();
                    ((System.Windows.Controls.Button)sender).Content = "Disconnect";
                    isConnected = true;
                    ComPortCombo.IsEnabled = false;
                }
            }
            else
            {
                gamepadController.Stop();
                serialComm.Disconnect();
                ((System.Windows.Controls.Button)sender).Content = "Connect";
                isConnected = false;
                ComPortCombo.IsEnabled = true;
                isEmulationActive = false;
                isMouseEmulatingRightStick = false;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            gamepadController?.Stop();
            serialComm?.Disconnect();
            this.Close();
        }

        private void DebugConsole_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
        }
    }
}
