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
        private static AutoResetEvent m_PlayEvent = new AutoResetEvent(false);

        internal class Buffer:IDisposable
        {
            private IntPtr hWaveOut;
            private SoundStructure.WaveHdr waveHeader;
            private GCHandle headerDataHandle;
            private GCHandle waveHeaderHandle;

            private int Id;

            private short[] Data;

            public bool isPlaying { get; private set; }

            private Random randomSeed;

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
                //m_PlayEvent.Close();
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
            /// <param name="volume">Puissance du son, compris entre 0 et 100</param>
            /// <returns></returns>
            public void FillSine(int frequency, int volume)
            {
                double x;
                int nChannels = 2;
                double nSamplesPerSec = 44100;
                for (int i = 0; i < waveHeader.dwBufferLength; i++)
                {
                    x = Math.Sin(i * nChannels * Math.PI * (frequency) / nSamplesPerSec);
                    Data[i] = (short)(((double)volume/100) * short.MaxValue * x);
                }
                //Debug.WriteLine(string.Format("{0} -- {1} -> {2}", Id, Data[0], Data[waveHeader.dwBufferLength - 1]));
                System.Runtime.InteropServices.Marshal.Copy(Data, 0, waveHeader.lpData, waveHeader.dwBufferLength);
            }

            public void FillSquare(int frequency, int volume)
            {
                short[] soundData = new short[waveHeader.dwBufferLength];
                double multiple = 2 * frequency / 44100.0f;
                short gain = (short)(((double)volume / 100) * short.MaxValue);
                for (int i = 0; i < waveHeader.dwBufferLength; i++)
                {
                    soundData[i] = (((i * multiple) % 2) - 1 > 0) ? gain : (short)-gain;
                }

                //Debug.WriteLine(string.Format("{0} -- {1} -> {2}", Id, soundData[0], soundData[waveHeader.dwBufferLength - 1]));
                System.Runtime.InteropServices.Marshal.Copy(soundData, 0, waveHeader.lpData, waveHeader.dwBufferLength);
            }

            public void FillNoise(int volume)
            {
                short[] soundData = new short[waveHeader.dwBufferLength];
                for (int i = 0; i < waveHeader.dwBufferLength; i++)
                {
                    soundData[i] = (short)(((double)volume / 100) * short.MaxValue * GetRandomNumber(-1, 1));
                }

                //Debug.WriteLine(string.Format("{0} -- {1} -> {2}", Id, soundData[0], soundData[waveHeader.dwBufferLength - 1]));
                System.Runtime.InteropServices.Marshal.Copy(soundData, 0, waveHeader.lpData, waveHeader.dwBufferLength);
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
                    //m_PlayEvent.Reset();
                    Debug.WriteLine(string.Format("{0} -- {1} commence à jouer", DateTime.Now.ToString("mm:ss.fff"), Id));
                    isPlaying = SoundStructure.waveOutWrite(hWaveOut, ref waveHeader, Marshal.SizeOf(waveHeader)) == SoundStructure.MMSYSERR_NOERROR;
                    return isPlaying;
                }
            }

            public void Stop()
            {
                m_PlayEvent.Set();
            }

            public void Wait()
            {
                //if (isPlaying)
                //{
                Debug.WriteLine(string.Format("{0} -- {1} Attends (WaitOne)", DateTime.Now.ToString("mm:ss.fff"), Id));
                    isPlaying = m_PlayEvent.WaitOne();
                //}
                //else
                //{
                //    Thread.Sleep(0);
                //    //Debug.WriteLine("-------");
                //}
            }

            public void OnCompleted()
            {
                Debug.WriteLine(string.Format("{0} -- {1} C'est fini (appel)", DateTime.Now.ToString("mm:ss.fff"), Id));
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
        public SoundStructure.WaveTypes waveType { get; set; }

        private Thread wThread;
        
        private SoundStructure.WaveDelegate m_BufferProc = new SoundStructure.WaveDelegate(Buffer.WaveOutProc);

        public Sound()
        {
        }

        public Sound(SoundStructure.WaveTypes w)
        {
            OpenDevice(44100);
            waveType = w;
            buffers = new Buffer[2];
            buffers[0] = new Buffer(mainWaveOut, 16384, 0);
            buffers[1] = new Buffer(mainWaveOut, 16384, 1);
            currentBuffer = 0;
            soundFrequency = 440;
        }

        public void Dispose()
        {
            soundPlaying = false;
            //CloseDevice();
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

        public void ChangeWaveType(SoundStructure.WaveTypes w)
        {
            for (int i = 0; i < 2; i++)
            {
                buffers[i].Clean();
            }
            waveType = w;
        }

        private void PlayThread()
        {
            while (soundPlaying)
            {
                //Debug.WriteLine(string.Format("{0} -- Fill {1} début", DateTime.Now.Ticks, currentBuffer));
                switch (waveType)
                {
                    case SoundStructure.WaveTypes.Sine:
                        buffers[currentBuffer].FillSine(soundFrequency, soundVolume);
                        break;
                    case SoundStructure.WaveTypes.Square:
                        buffers[currentBuffer].FillSquare(soundFrequency, soundVolume);
                        break;
                    case SoundStructure.WaveTypes.Noise:
                        buffers[currentBuffer].FillNoise(soundVolume);
                        break;
                    default:
                        buffers[currentBuffer].FillSine(soundFrequency, soundVolume);
                        break;
                }
                //Debug.WriteLine(string.Format("{0} -- Fill {1} fin", DateTime.Now.Ticks, currentBuffer));
                Debug.WriteLine(string.Format("{0} -- {1} Thread en attente", DateTime.Now.ToString("mm:ss.fff"), currentBuffer));
                if (buffers[0].isPlaying || buffers[1].isPlaying)
                    buffers[currentBuffer].Wait();
                //Debug.WriteLine(string.Format("{0} -- Play {1} début", DateTime.Now.Ticks, currentBuffer));
                //Debug.WriteLine(string.Format("{0}'{2} -- {1} Thread lancé", DateTime.Now.Second, currentBuffer, DateTime.Now.Millisecond));
                buffers[currentBuffer].Play(soundFrequency);
                //Debug.WriteLine(string.Format("{0} -- Play {1} fin", DateTime.Now.Ticks, currentBuffer));
                currentBuffer = currentBuffer == 1 ? 0 : 1;
            }
        }

        #region NativeCSharpVersion

        public SoundPlayer sp;
        public SoundPlayer sp2;
        public bool spIsPlaying;

        public MemoryStream SinStream(UInt16 frequency, int msDuration, UInt16 volume = 16383)
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
            return mStrm;
        }

        public void PlayCrapBeep(UInt16 frequency, int msDuration, UInt16 volume = 16383)
        {
            //var mStrm = new MemoryStream();
            //BinaryWriter writer = new BinaryWriter(mStrm);

            //const double TAU = 2 * Math.PI;
            //int formatChunkSize = 16;
            //int headerSize = 8;
            //short formatType = 1;
            //short tracks = 1;
            //int samplesPerSecond = 44100;
            //short bitsPerSample = 16;
            //short frameSize = (short)(tracks * ((bitsPerSample + 7) / 8));
            //int bytesPerSecond = samplesPerSecond * frameSize;
            //int waveSize = 4;
            //int samples = (int)((decimal)samplesPerSecond * msDuration / 1000);
            //int dataChunkSize = samples * frameSize;
            //int fileSize = waveSize + headerSize + formatChunkSize + headerSize + dataChunkSize;
            //// var encoding = new System.Text.UTF8Encoding();
            //writer.Write(0x46464952); // = encoding.GetBytes("RIFF")
            //writer.Write(fileSize);
            //writer.Write(0x45564157); // = encoding.GetBytes("WAVE")
            //writer.Write(0x20746D66); // = encoding.GetBytes("fmt ")
            //writer.Write(formatChunkSize);
            //writer.Write(formatType);
            //writer.Write(tracks);
            //writer.Write(samplesPerSecond);
            //writer.Write(bytesPerSecond);
            //writer.Write(frameSize);
            //writer.Write(bitsPerSample);
            //writer.Write(0x61746164); // = encoding.GetBytes("data")
            //writer.Write(dataChunkSize);

            //double theta = frequency * TAU / (double)samplesPerSecond;
            //// 'volume' is UInt16 with range 0 thru Uint16.MaxValue ( = 65 535)
            //// we need 'amp' to have the range of 0 thru Int16.MaxValue ( = 32 767)
            //double amp = volume >> 2; // so we simply set amp = volume / 2
            //for (int step = 0; step < samples; step++)
            //{
            //    short s = (short)(amp * Math.Sin(theta * (double)step));
            //    writer.Write(s);
            //}

            //mStrm.Seek(0, SeekOrigin.Begin);
            MemoryStream ms1 = SinStream(frequency, msDuration, volume);
            MemoryStream ms2 = SinStream(frequency, msDuration, volume);
            sp =  new SoundPlayer();
            sp2 = new SoundPlayer();
            //sp.LoadCompleted += sp_LoadCompleted;

            //for (int i = 0; i < 10; i++)
            //{
            //    sp.PlaySync();
            //    mStrm.Seek(0, SeekOrigin.Begin);
            //    sp.Stream = mStrm;
            //    sp.Load();
            //}
            sp.Stream = SinStream(frequency, msDuration, volume);
            sp2.Stream = SinStream(frequency, msDuration, volume);
            sp.Load();
            sp2.Load();
            sp.PlaySync();
            //Debug.WriteLine("tttttt");
            sp2.PlaySync();
            //spIsPlaying = false;
            //sp.Load();
            //for (int i = 0; i < 10; i++)
            //{
            //    sp.LoadAsync();
            //    spIsPlaying = true;
            //    while (spIsPlaying) { }
            //}
            //sp.Dispose();
            //writer.Close();
            //mStrm.Close();
        }

        private void sp_LoadCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (spIsPlaying && sp.IsLoadCompleted)
            {
                sp.PlaySync();
                spIsPlaying = false;
            }
        }
        #endregion
    }
}
