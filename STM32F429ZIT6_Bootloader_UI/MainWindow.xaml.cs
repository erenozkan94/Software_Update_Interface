using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace UI_5_10KW
{
    public partial class MainWindow : Window
    {
        private TaskCompletionSource<string> _bootloaderResponseTcs;
        private SerialPort _serialPort;
        private readonly List<Border> MenuList = new List<Border>();
        private bool IsMaximize = false;

        public MainWindow()
        {
            InitializeComponent();
            MenuList.Add(SettingsPage);
            MenuList.Add(UpdatePage);
            InitializeBaudRates();
            LoadPorts();
            SwitchPage(SettingsPage);
        }

        private void InitializeBaudRates()
        {
            if (CmbBaud != null)
            {
                CmbBaud.ItemsSource = new List<int> { 9600, 115200, 2000000 };
                CmbBaud.SelectedIndex = 2;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject dep) where T : DependencyObject
        {
            if (dep == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                if (child is T t) yield return t;

                foreach (var c in FindVisualChildren<T>(child))
                    yield return c;
            }
        }

        private void LoadPorts()
        {
            if (CmbPorts == null) return;

            var ports = SerialPort.GetPortNames().ToList();
            string priorityPort = "";

            var sorted = ports.OrderByDescending(p => p == priorityPort).ThenBy(p => p).ToList();
            CmbPorts.ItemsSource = sorted;

            if (sorted.Count > 0)
                CmbPorts.SelectedIndex = 0;
            else
                TxtStatus.Text = "Status: No port found";
        }

        private void RefreshPorts_Click(object sender, RoutedEventArgs e) => LoadPorts();

        private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo { FileName = "devmgmt.msc", UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void SwitchPage(Border activePage)
        {
            foreach (var page in MenuList) page.Visibility = Visibility.Hidden;
            activePage.Visibility = Visibility.Visible;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => SwitchPage(SettingsPage);
        private void UpdateButton_Click(object sender, RoutedEventArgs e) => SwitchPage(UpdatePage);

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) ChangeWindowState();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void FullScreenButton_Click(object sender, RoutedEventArgs e) => ChangeWindowState();

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();
            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void ChangeWindowState()
        {
            if (IsMaximize)
            {
                WindowState = WindowState.Normal;
                Width = 1080; Height = 720;
                IsMaximize = false;
            }
            else
            {
                WindowState = WindowState.Maximized;
                IsMaximize = true;
            }
        }

        private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTheme == null || CmbTheme.SelectedItem == null) return;
            if (CmbTheme.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                ApplyTheme(item.Tag.ToString());
            }
        }

        private void ApplyTheme(string theme)
        {
            if (theme == "Light")
            {
                SetResColor("BgDark", Color.FromRgb(240, 242, 245));
                SetResColor("BgPanel", Color.FromRgb(255, 255, 255));
                SetResColor("SidebarBg", Color.FromRgb(255, 255, 255));
                SetResColor("CardBg", Color.FromRgb(255, 255, 255));
                SetResColor("CardHeader", Color.FromRgb(230, 235, 240));
                SetResColor("TextPrimary", Color.FromRgb(30, 30, 30));
                SetResColor("TextSecondary", Color.FromRgb(100, 100, 100));
                SetResColor("BorderColor", Color.FromRgb(200, 200, 200));
                SetResColor("DangerColor", Color.FromRgb(234, 150, 33));
                SetResColor("WarningColor", Color.FromRgb(241, 197, 70));
            }
            else
            {
                SetResColor("BgDark", Color.FromRgb(27, 34, 40));
                SetResColor("BgPanel", Color.FromRgb(30, 30, 30));
                SetResColor("SidebarBg", Color.FromRgb(15, 42, 46));
                SetResColor("CardBg", Color.FromRgb(45, 45, 48));
                SetResColor("CardHeader", Color.FromRgb(55, 55, 60));
                SetResColor("TextPrimary", Color.FromRgb(230, 230, 230));
                SetResColor("TextSecondary", Color.FromRgb(160, 160, 160));
                SetResColor("BorderColor", Color.FromRgb(11, 14, 17));
                SetResColor("DangerColor", Color.FromRgb(234, 150, 33));
                SetResColor("WarningColor", Color.FromRgb(97, 78, 24));
            }
        }

        private void SetResColor(string key, Color color)
        {
            var newBrush = new SolidColorBrush(color);
            if (this.Resources.Contains(key)) this.Resources[key] = newBrush;
            else if (Application.Current.Resources.Contains(key)) Application.Current.Resources[key] = newBrush;
            else this.Resources[key] = newBrush;
        }

        private string ConvertIntToHex(string controlName, int byteLength)
        {
            var tb = this.FindName(controlName) as TextBox;
            if (tb != null && long.TryParse(tb.Text, out long val))
            {
                if (tb.Name == "_0037") val *= 1000000;
                byte[] bytes = BitConverter.GetBytes(val);
                return BitConverter.ToString(bytes, 0, byteLength).Replace("-", "");
            }
            return new string('0', byteLength * 2);
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
                Disconnect();
            else
                Connect();
        }

        private void Connect()
        {
            if (CmbPorts.SelectedItem == null || CmbBaud.SelectedItem == null)
            {
                MessageBox.Show("Please select Port and Baud Rate.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string portName = CmbPorts.SelectedItem.ToString();
            int baudRate = (int)CmbBaud.SelectedItem;

            try
            {
                _serialPort = new SerialPort(portName, baudRate);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                TxtStatus.Text = $"Status: {portName} @ {baudRate} Connected";
                SetConnectionStatus(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Status: Error";
            }
        }

        private void Disconnect()
        {
            try
            {
                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.DiscardInBuffer();
                        _serialPort.DiscardOutBuffer();
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch { }

            TxtStatus.Text = "Status: Disconnected";
            SetConnectionStatus(false);
        }

        private void SetConnectionStatus(bool connected)
        {
            if (ConnDot != null)
                ConnDot.Fill = connected ? (Brush)this.Resources["SuccessColor"] : (Brush)this.Resources["TextSecondary"];

            if (ConnText != null)
            {
                ConnText.Text = connected ? "Connected" : "Waiting for connection";
                ConnText.Foreground = connected ? (Brush)this.Resources["SuccessColor"] : (Brush)this.Resources["TextSecondary"];
            }
        }

        private string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            return string.Concat(bytes.Select(b => b.ToString("X2")));
        }

        public void SendCANData(string addrHex, string dataHex)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            string cleanDataHex = dataHex.Replace(" ", "").Replace("-", "").Replace(":", "");
            byte[] addr = StringToByteArray(addrHex.Replace(" ", ""));
            byte[] data = StringToByteArray(cleanDataHex);
            byte dlc = (byte)data.Length;

            if (dlc > 8)
            {
                MessageBox.Show($"Data length cannot be {dlc} bytes. Max 8 bytes.", "DLC Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            byte[] frame = new byte[5 + dlc];
            frame[0] = 0xAA;
            frame[1] = (byte)(0xC0 + dlc);

            if (addr.Length >= 2)
            {
                frame[2] = addr[1];
                frame[3] = addr[0];
            }
            else if (addr.Length == 1)
            {
                frame[2] = addr[0];
                frame[3] = 0x00;
            }

            for (int i = 0; i < dlc; i++)
                frame[4 + i] = data[i];

            frame[frame.Length - 1] = 0x55;

            try
            {
                _serialPort.Write(frame, 0, frame.Length);
            }
            catch (Exception ex)
            {
                Disconnect();
                MessageBox.Show("Transmission Error: " + ex.Message);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;

                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead < 5) return;

                byte[] buffer = new byte[bytesToRead];
                _serialPort.Read(buffer, 0, bytesToRead);

                List<string> hexList = new List<string>();
                foreach (byte b in buffer) hexList.Add(b.ToString("X2"));

                string rawData = string.Join(" ", hexList);
                System.Diagnostics.Debug.WriteLine($"Incoming Raw Data: {rawData}");

                if (hexList.Count >= 5 && hexList[0] == "AA" && hexList[hexList.Count - 1] == "55")
                {
                    string addr = hexList[3] + hexList[2];
                    System.Diagnostics.Debug.WriteLine($"Detected ID: {addr}");

                    List<string> data = new List<string>();
                    for (int i = 4; i < hexList.Count - 1; i++)
                        data.Add(hexList[i]);

                    Dispatcher.Invoke(() =>
                    {
                        ProcessIncomingMessage(addr, data);
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Incomplete or fragmented packet received!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General Error: {ex.Message}");
            }
        }

        public void ProcessIncomingMessage(string addr, List<string> dataHex)
        {
            if (dataHex == null || dataHex.Count == 0) return;

            try
            {
                switch (addr)
                {
                    case "0101":
                    case "0103":
                    case "0105":
                    case "0106":
                        _bootloaderResponseTcs?.TrySetResult(addr + "," + string.Join(",", dataHex));
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"Undefined or Unhandled ID Received: {addr}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Data processing error (ID: {addr}): {ex.Message}");
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
            try
            {
                return Enumerable.Range(0, hex.Length)
                                 .Where(i => i % 2 == 0)
                                 .Select(i => Convert.ToByte(hex.Substring(i, 2), 16))
                                 .ToArray();
            }
            catch { return Array.Empty<byte>(); }
        }

        private void BtnFileSelect_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Binary Files (*.bin)|*.bin";
            if (dlg.ShowDialog() == true)
            {
                TxtFilePath.Text = dlg.FileName;
                LblProgressStatus.Text = "File loaded, ready to send.";
            }
        }

        private async void BtnSendUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                MessageBox.Show("Please establish a connection first.", "No Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                BtnSendUpdate.IsEnabled = false;
                byte[] fileData = System.IO.File.ReadAllBytes(TxtFilePath.Text);

                // Protocol Step: 0x100 (Enter Bootloader)
                _bootloaderResponseTcs = new TaskCompletionSource<string>();
                SendCANData("0100", "1001000000000000");

                var response = await Task.WhenAny(_bootloaderResponseTcs.Task, Task.Delay(2000));
                if (response != _bootloaderResponseTcs.Task) throw new Exception("Device could not enter bootloader mode (0x101 not received).");

                string result = await (Task<string>)response;
                if (!(result.Split(',')[0] == "0101" && result.Split(',')[1] == "10" && result.Split(',')[2] == "01"))
                {
                    MessageBox.Show("Update not allowed by device.", "Communication Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else
                {
                    string[] r = result.Split(',');
                    LblBootVersion.Text = $"V{int.Parse(r[3])}.{int.Parse(r[4])}.{int.Parse(r[5])}";
                }

                // Protocol Step: 0x102 (Write Request)
                _bootloaderResponseTcs = new TaskCompletionSource<string>();
                SendCANData("0102", "1001000000000000");

                response = await Task.WhenAny(_bootloaderResponseTcs.Task, Task.Delay(60000));
                if (response != _bootloaderResponseTcs.Task) throw new Exception("Write permission not received (0x103 not received).");

                result = await (Task<string>)response;
                if (!(result.Split(',')[0] == "0103" && result.Split(',')[1] == "10" && result.Split(',')[2] == "01"))
                {
                    MessageBox.Show("Update not allowed (Write Permission Denied).", "Communication Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                LblProgressStatus.Text = "Update Started";

                // Protocol Step: 0x104 (Header & Data)
                byte[] fileBytes = System.IO.File.ReadAllBytes(TxtFilePath.Text);
                byte entireFileCrc = CalculateBootloaderCRC(fileBytes, fileBytes.Length - 1);
                byte[] fileSizeArea = BitConverter.GetBytes(fileBytes.Length);

                byte[] mainHeader = new byte[8];
                mainHeader[0] = 0x10;
                Array.Copy(fileSizeArea, 0, mainHeader, 1, 4);
                mainHeader[5] = entireFileCrc;

                SendCANData("0104", BytesToHex(mainHeader));
                await Task.Delay(4000);

                int totalPackets = (int)Math.Ceiling(fileBytes.Length / 8.0);

                for (int i = 0; i < totalPackets; i++)
                {
                    _bootloaderResponseTcs = new TaskCompletionSource<string>();

                    byte[] dataFrame = new byte[8];
                    for (int b = 0; b < 8; b++)
                    {
                        int dataIdx = (i * 8) + b;
                        dataFrame[b] = dataIdx < fileBytes.Length ? fileBytes[dataIdx] : (byte)0xFF;
                    }

                    SendCANData("0104", BytesToHex(dataFrame));

                    // Wait for 0x105 ACK for each packet
                    var packetWaitTask = await Task.WhenAny(_bootloaderResponseTcs.Task, Task.Delay(2000));

                    if (packetWaitTask == _bootloaderResponseTcs.Task)
                    {
                        string packetResult = await _bootloaderResponseTcs.Task;
                        string[] parts = packetResult.Split(',');

                        if (parts[0] == "0105" && parts[1] == "10" && parts[2] == "01")
                        {
                            double progress = ((double)(i + 1) / totalPackets) * 100;
                            UpdateProgressBar.Value = progress;
                            LblProgressPercent.Text = $"%{(int)progress}";
                            LblProgressStatus.Text = $"Packet {i + 1} / {totalPackets} confirmed.";
                        }
                        else
                        {
                            MessageBox.Show($"Unexpected ID or Data received: {parts[0]}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Timeout for Packet {i + 1}! Device did not send 0x105.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Final Step: 0x106 Final Confirmation
                _bootloaderResponseTcs = new TaskCompletionSource<string>();
                var ACKpacketWaitTask = await Task.WhenAny(_bootloaderResponseTcs.Task, Task.Delay(30000));

                if (ACKpacketWaitTask == _bootloaderResponseTcs.Task)
                {
                    string packetResult = await _bootloaderResponseTcs.Task;
                    string[] parts = packetResult.Split(',');

                    if (parts[0] == "0106" && parts[1] == "10" && parts[2] == "01")
                    {
                        MessageBox.Show("Update Completed Successfully. Starting New Firmware...", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Unexpected ID or Data received: {parts[0]}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Confirmation message not received. Please try the update process again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                BtnSendUpdate.IsEnabled = true;
            }
        }

        private byte CalculateBootloaderCRC(byte[] frame, int length)
        {
            ushort dummy = 0;
            for (int i = 0; i <= length; i++)
            {
                dummy += frame[i];
                if (dummy > 255)
                    dummy -= 255;
            }
            byte retWert = (byte)dummy;
            retWert ^= 0xFF;
            return retWert;
        }
    }
}