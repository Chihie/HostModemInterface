using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HostModemInterface
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SerialLayer serialLayer;

        public MainWindow()
        {
            InitializeComponent();

            serialLayer = new SerialLayer();
            serialLayer.outputData = AddDataToReceivedTextBox;

            foreach (string port in SerialPort.GetPortNames())
                PortComboBox.Items.Add(port);

            PortComboBox.SelectedIndex = PortComboBox.Items.Count - 1;


            PHYRadioButton.Checked += ModeChanged;
            DLRadioButton.Checked += ModeChanged;

            QPSKRadioButton.Checked += ModulationChanged;
            BPSKPNARadioButton.Checked += ModulationChanged;
            BPSKRadioButton.Checked += ModulationChanged;
            EIGHTPSKRadioButton.Checked += ModulationChanged;


            DLRadioButton.IsChecked = true;
            BPSKRadioButton.IsChecked = true;
            CloseButton.IsEnabled = false;
        }

        private void AddDataToReceivedTextBox(string Data)
        {
            // dzieki dispatcherowi kazdy watek moze dodac tekst w dowolnej chwili
            Dispatcher.BeginInvoke((Action)(() => {
                ReceivedTextBox.Text += Data;
                ReceivedTextBox.ScrollToEnd();
            }));
        }
        
        private void ModeChanged(object sender, EventArgs e)
        {
            if(serialLayer.serialPort.IsOpen)
            {
                if ((bool)DLRadioButton.IsChecked)
                    serialLayer.SendChangeModeRequest(CommandEnum.SET_DL);
                else
                    serialLayer.SendChangeModeRequest(CommandEnum.SET_PHY);
            }
        }

        private void ModulationChanged(object sender, EventArgs e)
        {
            if((bool)EIGHTPSKRadioButton.IsChecked)
            {
                FECCheckBox.IsEnabled = false;
                FECCheckBox.IsChecked = false;
            }
            else if((bool)BPSKPNARadioButton.IsChecked)
            {
                FECCheckBox.IsEnabled = false;
                FECCheckBox.IsChecked = true;
            }
            else
                FECCheckBox.IsEnabled = true;
        }

        private void OpenClicked(object sender, RoutedEventArgs e)
        {
            serialLayer.OpenPort(PortComboBox.SelectedItem.ToString());
            OpenButton.IsEnabled = false;
            CloseButton.IsEnabled = true;

            DLRadioButton.IsChecked = true;
            Thread.Sleep(50);
            ModeChanged(null, null); // przejscie w tryb DL po otworzeniu portu
        }

        private void CloseClicked(object sender, RoutedEventArgs e)
        {
            serialLayer.ClosePort();
            OpenButton.IsEnabled = true;
            CloseButton.IsEnabled = false;
        }

        private void ResetClicked(object sender, RoutedEventArgs e)
        {
            if (serialLayer.serialPort.IsOpen)
            {
                serialLayer.SendResetRequest();
                
                DLRadioButton.IsChecked = true;
                Thread.Sleep(50);
                ModeChanged(null, null); // przejscie w tryb DL po resecie
            }
        }

        private void SendClicked(object sender, RoutedEventArgs e)
        {
            if (serialLayer.serialPort.IsOpen)
            {
                int len = ASCIITextBox.Text.Length;
                if(len > 0)
                {
                    byte[] data = new byte[len];
                    string msg = ASCIITextBox.Text;
                    for(int i = 0; i < len; ++i)
                        data[i] = (byte)msg[i];

                    CommandEnum cmd = CommandEnum.DL_DATA_REQUEST;
                    if ((bool)PHYRadioButton.IsChecked)
                        cmd = CommandEnum.PHY_DATA_REQUEST;

                    ModulationEnum mod = ModulationEnum.B_PSK;
                    if ((bool)QPSKRadioButton.IsChecked)
                        mod = ModulationEnum.Q_PSK;
                    else if ((bool)EIGHTPSKRadioButton.IsChecked)
                        mod = ModulationEnum.EIGHT_PSK;
                    else if ((bool)BPSKPNARadioButton.IsChecked)
                        mod = ModulationEnum.BPSK_WITH_PNA;

                    serialLayer.SendDataRequest(cmd, mod, (bool)FECCheckBox.IsChecked, data);
                }

            }
        }

        // kazdy znak wpisany w pole ASCII zostanie dodany w postaci heksadecymalnej w polu HEX
        private void ASCIITextBoxChanged(object sender, TextChangedEventArgs e)
        {
            if (ASCIITextBox.IsFocused)
            {
                HEXTextBox.Text = "";
                byte[] asciiBytes = Encoding.ASCII.GetBytes(ASCIITextBox.Text);

                foreach (byte b in asciiBytes)
                    HEXTextBox.Text += String.Format("{0:x} ", b);
            }
        }

        // kazdy znak wpisany w pole HEX zostanie dodany w odpowiedniej postaci w polu ASCII
        private void HEXTextBoxChanged(object sender, TextChangedEventArgs e)
        {
            if (HEXTextBox.IsFocused)
            {
                ASCIITextBox.Text = "";

                string[] hex_numbers = HEXTextBox.Text.Split(' ');
                foreach (string hex_string in hex_numbers)
                    if (hex_string != "")
                    {
                        try
                        {
                            int hex = Convert.ToInt32(hex_string, 16);
                            ASCIITextBox.Text += System.Convert.ToChar(hex);
                        }
                        catch (Exception)
                        {
                            ASCIITextBox.Text = "*WRONG INPUT*";
                        }
                    }
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            serialLayer.ClosePort();
            serialLayer.applicationIsOn = false;
        }
    }
}
