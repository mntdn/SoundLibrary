using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Expaceo.SoundLibrary.Base
{
    public class Sound:IDisposable
    {
        internal class Buffer:IDisposable
        {
            private IntPtr hWaveOut;
            private SoundStructure.WaveHdr waveHeader;
            private GCHandle headerDataHandle;
            private GCHandle waveHeaderHandle;

            private int Id;

            private short[] Data;

            private bool isPlaying;

            private Random randomSeed;

            private AutoResetEvent m_PlayEvent = new AutoResetEvent(false);

            public Buffer(IntPtr waveOutHandle, int buffersize, int id)
            {
                hWaveOut = waveOutHandle;

                Id = id;
                randomSeed = new Random();

                waveHeaderHandle = GCHandle.Alloc(waveHeader, GCHandleType.Pinned);
                waveHeader.dwUser = (IntPtr)GCHandle.Alloc(this);
                Data = new short[buffersize];
                headerDataHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
                waveHeader.lpData = headerDataHandle.AddrOfPinnedObject();
                waveHeader.dwBufferLength = buffersize;
                waveHeader.dwFlags = 0;
                waveHeader.dwLoops = 0;
                try
                {
                    SoundStructure.waveOutPrepareHeader(hWaveOut, ref waveHeader, Marshal.SizeOf(waveHeader));
                }
                catch
                {
                    Debug.WriteLine("Error preparing Header!");
                }
            }

            public void Dispose()
            {
                if (waveHeader.lpData != IntPtr.Zero)
                {
                    SoundStructure.waveOutUnprepareHeader(hWaveOut, ref waveHeader, Marshal.SizeOf(waveHeader));
                    waveHeaderHandle.Free();
                    waveHeader.lpData = IntPtr.Zero;
                }
                m_PlayEvent.Close();
                if (headerDataHandle.IsAllocated)
                    headerDataHandle.Free();
                GC.SuppressFinalize(this);
            }

            internal static void WaveOutProc(IntPtr hdrvr, int uMsg, int dwUser, ref SoundStructure.WaveHdr wavhdr, int dwParam2)
            {
                if (uMsg == SoundStructure.MM_WOM_DONE)
                {
                    //Debug.WriteLine("Son terminé");
                    GCHandle h = (GCHandle)wavhdr.dwUser;
                    Buffer buf = (Buffer)h.Target;
                    Debug.WriteLine(string.Format("{0} -- {1} a fini", DateTime.Now.Ticks, buf.Id));
                    buf.OnCompleted();
                }
            }
            
            public double GetRandomNumber(double minimum, double maximum)
            {
                return randomSeed.NextDouble() * (maximum - minimum) + minimum;
            }

            /// <summary>
            /// Remplit le buffer avec une onde sinusoïdale
            /// </summary>
            /// <param name="frequency">Fréquence (La = 440)</param>
            /// <param name="startPos">Position de départ, indiquer la dernière position renvoyée par Fill, ou bien 0 pour commencer</param>
            /// <param name="volume">Puissance du son, compris entre 0 et 100</param>
            /// <returns></returns>
            public int Fill(int frequency, int startPos, int volume)
            {
                //Stopwatch s = new Stopwatch();
                //s.Start();
                double x;
                int nChannels = 2;
                double nSamplesPerSec = 44100;
                //for (int i = 0; i < waveHeader.dwBufferLength; i++)
                //{
                //    x = Math.Sin(i * nChannels * Math.PI * (frequency) / nSamplesPerSec);
                //    Data[i] = (short)(short.MaxValue * x);
                //}
                int c = 0;
                int pos = startPos;
                short valueOrigin = (short)(short.MaxValue * Math.Sin(0 * nChannels * Math.PI * (frequency) / nSamplesPerSec));
                bool valueBackToOrigin = startPos == 0;
                while (c < waveHeader.dwBufferLength)
                {
                    x = Math.Sin(pos * nChannels * Math.PI * (frequency) / nSamplesPerSec);
                    Data[c] = (short)((volume/100) * short.MaxValue * x);
                    if (Data[c] == valueOrigin && !valueBackToOrigin)
                    {
                        pos = 0; valueBackToOrigin = true;
                    }
                    else
                    {
                        pos++;
                    }
                    c++;
                }
                //Debug.WriteLine(string.Format("{0} -- {1} -> {2}", Id, Data[0], Data[waveHeader.dwBufferLength - 1]));
                System.Runtime.InteropServices.Marshal.Copy(Data, 0, waveHeader.lpData, waveHeader.dwBufferLength);
                return pos == 0 ? pos : pos - 1;
                //s.Stop();
                //Debug.WriteLine(string.Format("{0} Filled in {1} ticks", Id, s.ElapsedTicks));
            }

            public int FillSquare(int frequency, int startPos, int volume)
            {
                //Stopwatch s = new Stopwatch();
                //s.Start();
                short[] soundData = new short[waveHeader.dwBufferLength];
                int peakLength = 100;
                int c = peakLength;
                bool negativeValue = false;
                for (int i = 0; i < waveHeader.dwBufferLength; i++)
                {
                    soundData[i] = (short)((volume / 100) * short.MaxValue * (negativeValue ? -1 : 1));
                    c--;
                    if (c == 0)
                    {
                        negativeValue = !negativeValue;
                        c = peakLength;
                    }
                }

                Debug.WriteLine(string.Format("{0} -- {1} -> {2}", Id, soundData[0], soundData[waveHeader.dwBufferLength - 1]));
                System.Runtime.InteropServices.Marshal.Copy(soundData, 0, waveHeader.lpData, waveHeader.dwBufferLength);
                return 1;
                //s.Stop();
                //Debug.WriteLine(string.Format("{0} Filled in {1} ticks", Id, s.ElapsedTicks));
            }

            public int FillNoise(int volume)
            {
                //Stopwatch s = new Stopwatch();
                //s.Start();
                short[] soundData = new short[waveHeader.dwBufferLength];
                for (int i = 0; i < waveHeader.dwBufferLength; i++)
                {
                    soundData[i] = (short)((volume / 100) * short.MaxValue * GetRandomNumber(-1, 1));
                }

                Debug.WriteLine(string.Format("{0} -- {1} -> {2}", Id, soundData[0], soundData[waveHeader.dwBufferLength - 1]));
                System.Runtime.InteropServices.Marshal.Copy(soundData, 0, waveHeader.lpData, waveHeader.dwBufferLength);
                return 1;
                //s.Stop();
                //Debug.WriteLine(string.Format("{0} Filled in {1} ticks", Id, s.ElapsedTicks));
            }

            public void Clean()
            {
                for (int i = 0; i < waveHeader.dwBufferLength - 1; i++)
			    {
                    Data[i] = 0;
			    }
            }

            public bool Play(int frequency)
            {
                lock (this)
                {
                    m_PlayEvent.Reset();
                    //Fill(frequency);
                    Debug.WriteLine(string.Format("{0} -- {1} commence à jouer", DateTime.Now.Ticks, Id));
                    int r = SoundStructure.waveOutWrite(hWaveOut, ref waveHeader, Marshal.SizeOf(waveHeader));
                    isPlaying = r == SoundStructure.MMSYSERR_NOERROR;
                    return isPlaying;
                }
            }

            public void Stop()
            {
                m_PlayEvent.Set();
            }

            public void Wait()
            {
                if (isPlaying)
                {
                    isPlaying = m_PlayEvent.WaitOne();
                }
                else
                {
                    Thread.Sleep(0);
                    //Debug.WriteLine("-------");
                }
            }

            public void OnCompleted()
            {
                m_PlayEvent.Set();
                isPlaying = false;
            }
        }
        
        private IntPtr mainWaveOut;
        private Buffer[] buffers;
        private int currentBuffer;
        private bool soundPlaying;
        private int soundFrequency;
        public int soundVolume { get; set; }

        private Thread wThread;
        
        private SoundStructure.WaveDelegate m_BufferProc = new SoundStructure.WaveDelegate(Buffer.WaveOutProc);

        public Sound()
        {
        }

        public Sound(int samplesPerSecond)
        {
            OpenDevice(samplesPerSecond);
            buffers = new Buffer[2];
            buffers[0] = new Buffer(mainWaveOut, 17640, 0);
            buffers[1] = new Buffer(mainWaveOut, 17640, 1);
            currentBuffer = 0;
            soundFrequency = 440;
        }

        public void Dispose()
        {
            soundPlaying = false;
            CloseDevice();
        }

        public bool OpenDevice(int samplesPerSecond)
        {
            SoundStructure.WaveFormat waveFormat = new SoundStructure.WaveFormat();
            waveFormat.wFormatTag = (short)SoundStructure.WaveFormats.Pcm;
            waveFormat.nChannels = 2;
            waveFormat.wBitsPerSample = 16;
            waveFormat.nSamplesPerSec = samplesPerSecond;
            waveFormat.nBlockAlign = (short)(waveFormat.nChannels * waveFormat.wBitsPerSample / 8);
            waveFormat.nAvgBytesPerSec = waveFormat.nSamplesPerSec * waveFormat.nBlockAlign;
            waveFormat.cbSize = 0;

            if (SoundStructure.waveOutOpen(out mainWaveOut, 0, waveFormat, m_BufferProc, 0, SoundStructure.CALLBACK_FUNCTION) != SoundStructure.MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Sound card cannot be opened.");
                return false;
            }
            return true;
        }

        public bool CloseDevice()
        {
            if (SoundStructure.waveOutClose(mainWaveOut) != SoundStructure.MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Sound card cannot be closed!");
                return false;
            }
            return true;
        }

        public void PlayBeep(int frequency, int volume)
        {
            soundPlaying = true;
            soundFrequency = frequency;
            soundVolume = volume;
            wThread = new Thread(new ThreadStart(PlayThread));
            wThread.Start();
        }
        
        private void PlayThread()
        {
            int pos = 0;
            //pos = buffers[currentBuffer].FillNoise();
            while (soundPlaying)
            {
                //Debug.WriteLine(string.Format("{0} -- Fill {1} début", DateTime.Now.Ticks, currentBuffer));
                pos = buffers[currentBuffer].Fill(soundFrequency, pos, soundVolume);
                //pos = buffers[currentBuffer].FillSquare(soundFrequency, pos, soundVolume);
                //pos = buffers[currentBuffer].FillNoise();
                //Stopwatch s = new Stopwatch();
                //s.Start();
                //Debug.WriteLine(string.Format("{0} -- Fill {1} fin", DateTime.Now.Ticks, currentBuffer));
                buffers[currentBuffer].Wait();
                //Debug.WriteLine(string.Format("{0} -- Play {1} début", DateTime.Now.Ticks, currentBuffer));
                buffers[currentBuffer].Play(soundFrequency);
                //Debug.WriteLine(string.Format("{0} -- Play {1} fin", DateTime.Now.Ticks, currentBuffer));
                //s.Stop();
                //Debug.WriteLine(string.Format("Buf {0} Played in {1} ticks", currentBuffer, s.ElapsedTicks));
                //Debug.WriteLine("Done");
                currentBuffer = currentBuffer == 1 ? 0 : 1;
            }
        }

        public static void PlayCrapBeep(UInt16 frequency, int msDuration, UInt16 volume = 16383)
        {
            var mStrm = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(mStrm);

            const double TAU = 2 * Math.PI;
            int formatChunkSize = 16;
            int headerSize = 8;
            short formatType = 1;
            short tracks = 1;
            int samplesPerSecond = 44100;
            short bitsPerSample = 16;
            short frameSize = (short)(tracks * ((bitsPerSample + 7) / 8));
            int bytesPerSecond = samplesPerSecond * frameSize;
            int waveSize = 4;
            int samples = (int)((decimal)samplesPerSecond * msDuration / 1000);
            int dataChunkSize = samples * frameSize;
            int fileSize = waveSize + headerSize + formatChunkSize + headerSize + dataChunkSize;
            // var encoding = new System.Text.UTF8Encoding();
            writer.Write(0x46464952); // = encoding.GetBytes("RIFF")
            writer.Write(fileSize);
            writer.Write(0x45564157); // = encoding.GetBytes("WAVE")
            writer.Write(0x20746D66); // = encoding.GetBytes("fmt ")
            writer.Write(formatChunkSize);
            writer.Write(formatType);
            writer.Write(tracks);
            writer.Write(samplesPerSecond);
            writer.Write(bytesPerSecond);
            writer.Write(frameSize);
            writer.Write(bitsPerSample);
            writer.Write(0x61746164); // = encoding.GetBytes("data")
            writer.Write(dataChunkSize);

            double theta = frequency * TAU / (double)samplesPerSecond;
            // 'volume' is UInt16 with range 0 thru Uint16.MaxValue ( = 65 535)
            // we need 'amp' to have the range of 0 thru Int16.MaxValue ( = 32 767)
            double amp = volume >> 2; // so we simply set amp = volume / 2
            for (int step = 0; step < samples; step++)
            {
                short s = (short)(amp * Math.Sin(theta * (double)step));
                writer.Write(s);
            }

            mStrm.Seek(0, SeekOrigin.Begin);
            SoundPlayer sp =  new SoundPlayer(mStrm);

            sp.Play();
            sp.Dispose();
            writer.Close();
            mStrm.Close();
        }
    }
}
