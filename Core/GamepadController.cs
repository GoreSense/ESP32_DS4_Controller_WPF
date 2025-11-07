using System;

namespace ESP32_DS4_Controller_WPF.Core
{
    public class GamepadController
    {
        public const ushort BTN_SQUARE = 0x0001;
        public const ushort BTN_X = 0x0002;
        public const ushort BTN_CIRCLE = 0x0004;
        public const ushort BTN_TRIANGLE = 0x0008;
        public const ushort BTN_L1 = 0x0010;
        public const ushort BTN_R1 = 0x0020;
        public const ushort BTN_L2 = 0x0040;
        public const ushort BTN_R2 = 0x0080;

        private SerialComm serialComm;
        private GamepadCommand lastSentCommand;

        public GamepadController(SerialComm serial)
        {
            serialComm = serial;
            lastSentCommand = new GamepadCommand();
        }

        public void Start()
        {
        }

        public void Stop()
        {
            ResetGamepad();
        }

        public void SendCurrentState()
        {
            serialComm.SendGamepadCommand(lastSentCommand);
        }

        public GamepadCommand GetLastCommand()
        {
            return lastSentCommand;
        }

        public void SendStick(sbyte lx, sbyte ly, sbyte rx, sbyte ry)
        {
            lastSentCommand.LeftX = (byte)(lx + 128);
            lastSentCommand.LeftY = (byte)(ly + 128);
            lastSentCommand.RightX = (byte)(rx + 128);
            lastSentCommand.RightY = (byte)(ry + 128);
            serialComm.SendGamepadCommand(lastSentCommand);
        }

        public void CenterSticks()
        {
            lastSentCommand.LeftX = 128;
            lastSentCommand.LeftY = 128;
            lastSentCommand.RightX = 128;
            lastSentCommand.RightY = 128;
            serialComm.SendGamepadCommand(lastSentCommand);
        }

        public void PressButton(ushort button)
        {
            lastSentCommand.Buttons |= button;
            serialComm.SendGamepadCommand(lastSentCommand);
        }

        public void ReleaseButton(ushort button)
        {
            lastSentCommand.Buttons &= (ushort)~button;
            serialComm.SendGamepadCommand(lastSentCommand);
        }

        public void ReleaseTriggers()
        {
            lastSentCommand.L2 = 0;
            lastSentCommand.R2 = 0;
            serialComm.SendGamepadCommand(lastSentCommand);
        }

        public void SetTriggers(byte l2, byte r2)
        {
            lastSentCommand.L2 = l2;
            lastSentCommand.R2 = r2;
            serialComm.SendGamepadCommand(lastSentCommand);
        }

        public void ResetGamepad()
        {
            lastSentCommand = new GamepadCommand();
            serialComm.SendGamepadCommand(lastSentCommand);
        }
    }
}
