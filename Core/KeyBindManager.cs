using System;
using System.Collections.Generic;
using System.Linq;

namespace ESP32_DS4_Controller_WPF.Core
{
    public class KeyBindManager
    {
        public class KeyBinding
        {
            public string BindName { get; set; }
            public int VKey { get; set; }
            public ushort GamepadButton { get; set; }
            public byte BindType { get; set; }
            public int StickDirection { get; set; }
        }

        private Dictionary<int, KeyBinding> bindingsByVKey = new Dictionary<int, KeyBinding>();
        private Dictionary<string, KeyBinding> bindingsByName = new Dictionary<string, KeyBinding>();

        public event Action<KeyBinding> OnBindingChanged;

        public const byte BIND_TYPE_BUTTON = 0;
        public const byte BIND_TYPE_LEFT_STICK = 1;
        public const byte BIND_TYPE_RIGHT_STICK = 2;
        public const byte BIND_TYPE_TRIGGER = 3;

        public const int STICK_X_POSITIVE = 0;
        public const int STICK_X_NEGATIVE = 1;
        public const int STICK_Y_POSITIVE = 2;
        public const int STICK_Y_NEGATIVE = 3;

        public KeyBindManager()
        {
            InitializeDefaultBindings();
        }

        private void InitializeDefaultBindings()
        {
            // WASD для левого стика
            SetBinding("UP_key", (int)System.Windows.Input.Key.W, BIND_TYPE_LEFT_STICK, 0, STICK_Y_POSITIVE);
            SetBinding("D_key", (int)System.Windows.Input.Key.S, BIND_TYPE_LEFT_STICK, 0, STICK_Y_NEGATIVE);
            SetBinding("L_key", (int)System.Windows.Input.Key.A, BIND_TYPE_LEFT_STICK, 0, STICK_X_NEGATIVE);
            SetBinding("R_key", (int)System.Windows.Input.Key.D, BIND_TYPE_LEFT_STICK, 0, STICK_X_POSITIVE);

            // Кнопки
            SetBinding("X_key", (int)System.Windows.Input.Key.J, BIND_TYPE_BUTTON, GamepadController.BTN_X, 0);
            SetBinding("Y_key", (int)System.Windows.Input.Key.I, BIND_TYPE_BUTTON, GamepadController.BTN_SQUARE, 0);
            SetBinding("B_key", (int)System.Windows.Input.Key.K, BIND_TYPE_BUTTON, GamepadController.BTN_CIRCLE, 0);
            SetBinding("A_key", (int)System.Windows.Input.Key.L, BIND_TYPE_BUTTON, GamepadController.BTN_TRIANGLE, 0);

            // Плечевые кнопки
            SetBinding("LB_key", (int)System.Windows.Input.Key.Q, BIND_TYPE_BUTTON, GamepadController.BTN_L1, 0);
            SetBinding("RB_key", (int)System.Windows.Input.Key.E, BIND_TYPE_BUTTON, GamepadController.BTN_R1, 0);

            // Меню
            SetBinding("MENU_key", (int)System.Windows.Input.Key.Tab, BIND_TYPE_BUTTON, 0, 0);
            SetBinding("MENU2_key", (int)System.Windows.Input.Key.Return, BIND_TYPE_BUTTON, 0, 0);

            // Power кнопка (по умолчанию F1)
            SetBinding("Power", (int)System.Windows.Input.Key.F1, BIND_TYPE_BUTTON, 0, 0);
        }

        public void SetBinding(string bindName, int vKey, byte bindType, ushort gamepadButton, int stickDirection)
        {
            // Удаляем старую привязку если VKey уже занят
            if (bindingsByVKey.ContainsKey(vKey))
            {
                var oldBinding = bindingsByVKey[vKey];
                bindingsByName.Remove(oldBinding.BindName);
                bindingsByVKey.Remove(vKey);
            }

            var binding = new KeyBinding
            {
                BindName = bindName,
                VKey = vKey,
                GamepadButton = gamepadButton,
                BindType = bindType,
                StickDirection = stickDirection
            };

            bindingsByVKey[vKey] = binding;
            bindingsByName[bindName] = binding;

            OnBindingChanged?.Invoke(binding);
        }

        public KeyBinding GetBindingByVKey(int vKey)
        {
            return bindingsByVKey.ContainsKey(vKey) ? bindingsByVKey[vKey] : null;
        }

        public KeyBinding GetBindingByName(string bindName)
        {
            return bindingsByName.ContainsKey(bindName) ? bindingsByName[bindName] : null;
        }

        public List<KeyBinding> GetAllBindings()
        {
            return bindingsByVKey.Values.ToList();
        }

        public void ClearBinding(string bindName)
        {
            if (bindingsByName.ContainsKey(bindName))
            {
                var binding = bindingsByName[bindName];
                bindingsByVKey.Remove(binding.VKey);
                bindingsByName.Remove(bindName);
                OnBindingChanged?.Invoke(null);
            }
        }

        public string GetKeyName(KeyBinding binding)
        {
            if (binding == null)
                return "Not Set";
            return RawInputHandler.VKeyToString(binding.VKey);
        }

        public bool IsVKeyAlreadyBound(int vKey)
        {
            return bindingsByVKey.ContainsKey(vKey);
        }

        public KeyBinding GetConflictingBinding(int vKey)
        {
            return GetBindingByVKey(vKey);
        }
    }
}
