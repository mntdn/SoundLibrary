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
    public class Sound
    {
        public class Buffer
        {
            public int Buffersize { get; private set; }
            public Int16[] Data { get; set; }

            public Buffer(int buffersize)
            {
                Buffersize = buffersize;
                Data = new Int16[Buffersize];
            }

            public void Fill(int frequency, SoundStructure.WaveFormat waveFormat)
            {
                double x;
                for (int i = 0; i < Buffersize; i++)
                {
                    x = Math.Sin(i * waveFormat.nChannels * Math.PI * (frequency) / (double)waveFormat.nSamplesPerSec);
                    Data[i] = (Int16)(Int16.MaxValue * x);
                }
            }

            public void Clean()
            {
                for (int i = 0; i < Buffersize-1; i++)
			    {
                    Data[i] = 0;
			    }
            }
        }
        
        private IntPtr hWaveOut;
        private SoundStructure.WaveFormat waveFormat = new SoundStructure.WaveFormat();
        private SoundStructure.WaveHdr WaveHeader = new SoundStructure.WaveHdr();
        private Buffer[] buffers = new Buffer[2];
        private int currentBuffer = 0;

        public static AutoResetEvent m_PlayEvent = new AutoResetEvent(false);
        
        internal static void WaveOutProc(IntPtr hdrvr, int uMsg, int dwUser, ref SoundStructure.WaveHdr wavhdr, int dwParam2)
        {
            if (uMsg == SoundStructure.MM_WOM_DONE)
            {
                Debug.WriteLine("Son terminé");
                m_PlayEvent.Set();
            }
        }
        
        private SoundStructure.WaveDelegate m_BufferProc = new SoundStructure.WaveDelegate(WaveOutProc);

        public Sound()
        {
            for (int i = 0; i < 2; i++)
            {
                buffers[i] = new Buffer(441000);
            }
        }

        public bool OpenDevice(int samplesPerSecond)
        {
            waveFormat.wFormatTag = (short)SoundStructure.WaveFormats.Pcm;
            waveFormat.nChannels = 2;
            waveFormat.wBitsPerSample = 16;
            waveFormat.nSamplesPerSec = samplesPerSecond;
            waveFormat.nBlockAlign = (short)(waveFormat.nChannels * waveFormat.wBitsPerSample / 8);
            waveFormat.nAvgBytesPerSec = waveFormat.nSamplesPerSec * waveFormat.nBlockAlign;
            waveFormat.cbSize = 0;

            if (SoundStructure.waveOutOpen(out hWaveOut, 0, waveFormat, m_BufferProc, 0, SoundStructure.CALLBACK_FUNCTION) != SoundStructure.MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Sound card cannot be opened.");
                return false;
            }
            return true;
        }

        public bool CloseDevice()
        {
            if (SoundStructure.waveOutClose(hWaveOut) != SoundStructure.MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Sound card cannot be closed!");
                return false;
            }
            return true;
        }
        
        public void Stop()
        {
            m_PlayEvent.Set();
        }

        public void PlayBeep(int frequency)
        {
            for (int i = 0; i < 1; i++)
            {
                // Initalise le premier buffer    
                buffers[currentBuffer].Fill(frequency, waveFormat);

                // ** Create the wave header for our sound buffer **
                GCHandle m_HeaderDataHandle = GCHandle.Alloc(buffers[currentBuffer].Data, GCHandleType.Pinned);
                WaveHeader.lpData = m_HeaderDataHandle.AddrOfPinnedObject();
                WaveHeader.dwBufferLength = buffers[currentBuffer].Buffersize;
                WaveHeader.dwFlags = 0;
                WaveHeader.dwLoops = 0;

                // ** Prepare the header for playback on sound card **
                if (SoundStructure.waveOutPrepareHeader(hWaveOut, ref WaveHeader, Marshal.SizeOf(WaveHeader)) != SoundStructure.MMSYSERR_NOERROR)
                {
                    Debug.WriteLine("Error preparing Header!");
                    return;
                }

                // ** Play the sound! **
                m_PlayEvent.Reset();

                if (SoundStructure.waveOutWrite(hWaveOut, ref WaveHeader, Marshal.SizeOf(WaveHeader)) != SoundStructure.MMSYSERR_NOERROR)
                {
                    Debug.WriteLine("Error writing to sound card!");
                    return;
                }

                // ** Wait until sound finishes playing
                //m_PlayEvent.WaitOne();

                // ** Unprepare our wav header **
                if (SoundStructure.waveOutUnprepareHeader(hWaveOut, ref WaveHeader, Marshal.SizeOf(WaveHeader)) != SoundStructure.MMSYSERR_NOERROR)
                {
                    Debug.WriteLine("Error unpreparing header!");
                    return;
                }

                currentBuffer = currentBuffer == 0 ? 1 : 0;
            }
            
            // ** Release our event handle **
            //m_PlayEvent.Close();
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
