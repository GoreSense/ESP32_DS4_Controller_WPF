using System;
using System.IO.Ports;
using System.Threading;

namespace ESP32_DS4_Controller_WPF.Core
{
    public class SerialComm
    {
        private SerialPort serialPort;
        public bool IsConnected { get; private set; }

        public event Action<string> OnLog;

        private Thread readThread;
        private bool shouldRead = false;

        public SerialComm()
        {
            IsConnected = false;
        }

        public bool Connect(string portName, int baudRate = 115200)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                    Disconnect();

                serialPort = new SerialPort(portName, baudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 100,
                    WriteTimeout = 100
                };

                serialPort.Open();
                IsConnected = true;

                // Запускаем поток чтения
                shouldRead = true;
                readThread = new Thread(ReadSerialData);
                readThread.Start();

                OnLog?.Invoke($"[Serial] Connected to {portName}");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                OnLog?.Invoke($"[Error] {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                shouldRead = false;

                if (readThread != null && readThread.IsAlive)
                    readThread.Join(500);

                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                    IsConnected = false;
                    OnLog?.Invoke("[Serial] Disconnected");
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Error] {ex.Message}");
            }
        }

        private void ReadSerialData()
        {
            while (shouldRead && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    if (serialPort.BytesToRead > 0)
                    {
                        string line = serialPort.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            OnLog?.Invoke($"[ESP32] {line.Trim()}");
                        }
                    }
                    Thread.Sleep(10);
                }
                catch { }
            }
        }

        public void SendGamepadCommand(GamepadCommand cmd)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                    return;

                byte[] data = cmd.ToBytes();

                string hexBytes = BitConverter.ToString(data).Replace("-", " ");
                OnLog?.Invoke($"[SEND] HEX: {hexBytes}");
                OnLog?.Invoke($"[SEND] LX:{(sbyte)(cmd.LeftX - 128):+0;-#} LY:{(sbyte)(cmd.LeftY - 128):+0;-#} RX:{(sbyte)(cmd.RightX - 128):+0;-#} RY:{(sbyte)(cmd.RightY - 128):+0;-#} Btn:{cmd.Buttons:X4}");

                serialPort.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Error] {ex.Message}");
            }
        }

        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }
    }

    public class GamepadCommand
    {
        public byte LeftX { get; set; } = 128;
        public byte LeftY { get; set; } = 128;
        public byte RightX { get; set; } = 128;
        public byte RightY { get; set; } = 128;
        public byte L2 { get; set; } = 0;
        public byte R2 { get; set; } = 0;
        public ushort Buttons { get; set; } = 0;

        public byte[] ToBytes()
        {
            byte[] buffer = new byte[11];
            buffer[0] = 0xFF;
            buffer[1] = LeftX;
            buffer[2] = LeftY;
            buffer[3] = RightX;
            buffer[4] = RightY;
            buffer[5] = L2;
            buffer[6] = R2;
            buffer[7] = (byte)(Buttons & 0xFF);
            buffer[8] = (byte)((Buttons >> 8) & 0xFF);

            byte checksum = buffer[0];
            for (int i = 1; i < 9; i++)
                checksum ^= buffer[i];
            buffer[9] = checksum;
            buffer[10] = 0xFE;

            return buffer;
        }
    }
}
