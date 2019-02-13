using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HostModemInterface
{
    public delegate void OutputData(string Data);

    class SerialLayer
    {
        public SerialPort serialPort;
        public OutputData outputData;
        public bool applicationIsOn;

        private FrameReader frameReader;

        public void OpenPort(string name)
        {
            if(serialPort != null && serialPort.IsOpen)
                serialPort.Close();

            serialPort.PortName = name;
            serialPort.BaudRate = 57600;
            serialPort.Parity = Parity.None;
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Open();
        }

        public void ClosePort()
        {
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();
        }

        public void SendResetRequest()
        {
            byte[] frame = MakeFrame(0x3C, null); // 0x3C to command code dla resetu. Nie potrzeba danych
            SendFrame(frame);
        }

        // Wyslanie polecenia zmiany trybu
        public void SendChangeModeRequest(CommandEnum command)
        {
            byte[] frame = null;
            switch (command)
            {
                case CommandEnum.SET_DL:
                    frame = MakeFrame(0x08, new byte[] { 0x00, 0x11 }); 
                    break;
                case CommandEnum.SET_PHY:
                    frame = MakeFrame(0x08, new byte[] { 0x00, 0x10 });
                    break;
            }
            SendFrame(frame);
        }

        public void SendDataRequest(CommandEnum command, ModulationEnum modulation, bool FEC, byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            byte[] buffer = new byte[data.Length + 1];
            byte configuration = 0x01;

            switch (modulation)
            {
                case ModulationEnum.B_PSK:
                    configuration = 0x00;
                    break;
                case ModulationEnum.Q_PSK:
                    configuration <<= 4; // 0001 0000
                    break;
                case ModulationEnum.EIGHT_PSK:
                    configuration <<= 5; // 0010 0000
                    break;
                case ModulationEnum.BPSK_WITH_PNA:
                    configuration = 0x70; // 0111 0000
                    break;
            }

            if (FEC)
                configuration |= 0x40; // 0100 0000

            buffer[0] = configuration;

            for (int i = 0; i < data.Length; ++i)
                buffer[i+1] = data[i];

            byte[] frame = null;

            switch (command)
            {
                case CommandEnum.DL_DATA_REQUEST:
                    frame = MakeFrame(0x50, buffer);
                    break;
                case CommandEnum.PHY_DATA_REQUEST:
                    frame = MakeFrame(0x24, buffer);
                    break;
            }
            SendFrame(frame);
        }

        public void SendFrame(byte[] frame)
        {
            serialPort.RtsEnable = true;
            Thread.Sleep(10);
            serialPort.Write(frame, 0, frame.Length);
            serialPort.RtsEnable = false;

            // debugging
            string s = "";
            foreach(byte b in frame)
                s += string.Format("0x{0:X} ", b);
            Console.WriteLine("wysylam: " + s);
        }

        public static byte[] MakeFrame(byte command, byte[] data)
        {
            int idx = 0;

            int data_length = (data != null) ? data.Length : 0;

            byte[] frame = new byte[5 + data_length]; // minimalna dlugosc ramki to 5
            frame[idx++] = 0x02;                      // bajt startu ramki
            frame[idx++] = (byte)data_length;         // ilosc bajtow danych
            frame[idx++] = command;                   // command code

            if (data != null)
                foreach (byte data_byte in data)
                    frame[idx++] = data_byte;

            int checksum = 0;
            for (int i = 1; i < idx; i++)
                checksum += frame[i];

            frame[idx++] = (byte)(checksum & 0x00FF);
            frame[idx] = (byte)(checksum >> 8);

            return frame;
        }
        

        private void SerialReceive(object o, SerialDataReceivedEventArgs args)
        {
            SerialPort serial = (SerialPort)o;

            int size = serial.BytesToRead;
            byte[] received = new byte[size];
            serial.Read(received, 0, size);

            foreach(byte b in received)
            {
                // debugging
                //outputData("" + (char)b);

                // dodaj odebrany znak do kolejki
                frameReader.dataQueue.Enqueue(b);
            }
        }

        public SerialLayer()
        {
            serialPort = new SerialPort();
            applicationIsOn = true;

            serialPort.DataReceived += SerialReceive;

            frameReader = new FrameReader(this);
            frameReader.Start();
        }
    }
}
