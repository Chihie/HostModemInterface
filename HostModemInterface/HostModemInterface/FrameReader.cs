using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HostModemInterface
{
    class FrameReader
    {
        public Queue<byte> dataQueue;

        private SerialLayer serial;

        private Thread receiver;
        private Thread timer;

        private ReadState readState;

        private byte[] frame;
        private int frameIdx;
        private int dataLength;

        private void CheckQueue()
        {
            while (serial.applicationIsOn)
            {
                while(dataQueue.Count > 0)
                {
                    byte b = dataQueue.Dequeue();
                    ReadByte(b);
                }

                Thread.Sleep(1);
            }
        }

        private void FrameIsReady()
        {
            if(frame[0] == 0x02)
            {
                string data = "";
                for(int i = 7; i < frameIdx - 2; i++)
                    data += (char)frame[i];
                data += '\n';
                serial.outputData(data);

                // wyslij ACK po odebraniu poprawnej ramki
                serial.serialPort.Write(new byte[] { 0x06 }, 0, 1);
            }
            readState = ReadState.BEGIN;
        }

        private void ReadByte(byte b)
        {
            if (readState == ReadState.BEGIN)
            {
                frameIdx = 0;
                frame[frameIdx++] = b;

                if (frame[0] != 0x06 && frame[0] != 0x15 &&
                    frame[0] != 0x02 && frame[0] != 0x03 &&
                    frame[0] != 0x3F)
                    frameIdx = 0;
                else
                {
                    if (frame[0] == 0x06 || frame[0] == 0x15)
                        FrameIsReady();
                    else if (frame[0] == 0x02 || frame[0] == 0x03 || frame[0] == 0x3F)
                    {
                        readState = ReadState.LENGTH;
                        StartTimer(10);
                    }
                }

            }
            else if (readState == ReadState.LENGTH)
            {
                frame[frameIdx++] = b;
                dataLength = b;
                if (frame[0] == 0x3F)
                {
                    readState = ReadState.BEGIN;
                    StopTimer();
                    FrameIsReady();
                }
                else
                {
                    readState = ReadState.CC;
                    StartTimer(10);
                }
            }
            else if (readState == ReadState.CC)
            {
                frame[frameIdx++] = b;
                readState = frame[1] != 0x00 ? ReadState.DATA : ReadState.CHECKSUM_1;
                StartTimer(10);
            }
            else if (readState == ReadState.DATA)
            {
                frame[frameIdx++] = b;
                dataLength--;
                StartTimer(10);

                if (dataLength == 0)
                    readState = ReadState.CHECKSUM_1;
            }
            else if (readState == ReadState.CHECKSUM_1)
            {
                frame[frameIdx++] = b;
                StartTimer(10);
                readState = ReadState.CHECKSUM_2;
            }
            else if (readState == ReadState.CHECKSUM_2)
            {
                frame[frameIdx++] = b;
                StopTimer();

                // sprawdzanie sumy kontrolnej
                int sum = 0;
                for (int i = 1; i < frameIdx - 2; i++)
                    sum += frame[i];
                int checksum = frame[frameIdx - 1];
                checksum <<= 8;
                checksum |= frame[frameIdx - 2];
                if (sum == checksum)
                {
                    Console.WriteLine("Poprawna suma kontrolna: " + sum + "_" + checksum);
                    FrameIsReady();
                }
                else
                    Console.WriteLine("Niepoprawna suma kontrolna: " + sum + "_" + checksum);
                readState = ReadState.BEGIN;
            }

            // w przypadku, gdy ktos bedzie probowal wyslac za dluga ramke, ma zresetowac caly proces
            if (frameIdx > 168)
            {
                frameIdx = 0;
                StartTimer(1);
            }
        }

        // watek, ktory zmniejsza tim o 1 co 1 ms. Jak dojdzie do 0 to resetuje proces odbioru ramki
        private long tim = 0;
        private void TimerUpdate()
        {
            while (serial.applicationIsOn)
            {
                if(tim > 0)
                {
                    tim--;
                    if (tim == 0)
                    {
                        Console.WriteLine("Odebrano niepoprawna ramke");
                        readState = ReadState.BEGIN;
                    }
                }

                Thread.Sleep(1);
            }
        }

        private void StartTimer(long time)
        {
            tim = time;
        }

        private void StopTimer()
        {
            tim = 0;
        }

        public void Start()
        {
            receiver = new Thread(CheckQueue);
            receiver.Start();

            timer = new Thread(TimerUpdate);
            timer.Start();
        }

        public FrameReader(SerialLayer serialLayer)
        {
            serial = serialLayer;
            dataQueue = new Queue<byte>();
            readState = ReadState.BEGIN;
            frame = new byte[180]; // 180 bajtow to maksymalny rozmiar ramki
        }
    }
}
