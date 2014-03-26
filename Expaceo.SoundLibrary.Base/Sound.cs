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
        [DllImport("winmm.dll")]
        public static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, WaveFormat lpFormat, WaveDelegate dwCallback, int dwInstance, int dwFlags);
        [DllImport("winmm.dll")]
        public static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);
        [DllImport("winmm.dll")]
        public static extern int waveOutWrite(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);
        [DllImport("winmm.dll")]
        public static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);
        [DllImport("winmm.dll")]
        public static extern int waveOutClose(IntPtr hWaveOut);

        public const int MMSYSERR_NOERROR = 0;
        public const int CALLBACK_FUNCTION = 0x00030000;

        public const int MM_WOM_OPEN = 0x3BB;
        public const int MM_WOM_CLOSE = 0x3BC;
        public const int MM_WOM_DONE = 0x3BD;

        public static AutoResetEvent m_PlayEvent = new AutoResetEvent(false);
        
        // callback function
        public delegate void WaveDelegate(IntPtr hdrvr, int uMsg, int dwUser, ref WaveHdr wavhdr, int dwParam2);

        internal static void WaveOutProc(IntPtr hdrvr, int uMsg, int dwUser, ref WaveHdr wavhdr, int dwParam2)
        {
            Debug.WriteLine(uMsg);
            if (uMsg == MM_WOM_DONE)
            {
                Debug.WriteLine("Son terminé");
                m_PlayEvent.Set();
            }
        }
        
        private WaveDelegate m_BufferProc = new WaveDelegate(WaveOutProc);

        public enum WaveFormats
        {
            Pcm = 1,
            Float = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public class WaveFormat
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WaveHdr
        {
            public IntPtr lpData; // pointer to locked data buffer
            public int dwBufferLength; // length of data buffer
            public int dwBytesRecorded; // used for input only
            public IntPtr dwUser; // for client's use
            public int dwFlags; // assorted flags (see defines)
            public int dwLoops; // loop control counter
            public IntPtr lpNext; // PWaveHdr, reserved for driver
            public int reserved; // reserved for driver
        }


        public void PlayOKBeep(int frequency, int samplesPerSecond, short bitsPerSample)
        {
            IntPtr hWaveOut;
            WaveFormat waveFormat = new WaveFormat();
            WaveHdr WaveHeader = new WaveHdr(); 
            int buffersize = 44100;

            char[] Data = new char[buffersize]; 
              
            double x;
            int i;
              
            waveFormat.wFormatTag = (short)WaveFormats.Pcm;
            waveFormat.nChannels = 2;
            waveFormat.wBitsPerSample = 8;
            waveFormat.nSamplesPerSec = samplesPerSecond;
            waveFormat.nBlockAlign = (short)(waveFormat.nChannels * waveFormat.wBitsPerSample / 8);
            waveFormat.nAvgBytesPerSec = waveFormat.nSamplesPerSec * waveFormat.nBlockAlign;    
            waveFormat.cbSize = 0;
                            
            // ** Open the audio device **
            if (waveOutOpen(out hWaveOut, 0, waveFormat, m_BufferProc, 0, CALLBACK_FUNCTION) != MMSYSERR_NOERROR) 
            {        
                Debug.WriteLine("Sound card cannot be opened.");
                return;
            }
              
            // ** Make the sound buffer **    
            for (i=0; i < buffersize; i++)
            {        
                x = Math.Sin(i * waveFormat.nChannels * Math.PI * (frequency) / (double)waveFormat.nSamplesPerSec); 
              
                // ** scale x to a range of 0-255 (signed char) for 8 bit sound reproduction **
                Data[i] = (char)(127*x+128);
            }
              
              
            // ** Create the wave header for our sound buffer **

            GCHandle m_HeaderDataHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            WaveHeader.lpData = m_HeaderDataHandle.AddrOfPinnedObject();
            WaveHeader.dwBufferLength=buffersize;
            WaveHeader.dwFlags=0;
            WaveHeader.dwLoops=0;
              
            // ** Prepare the header for playback on sound card **
            if (waveOutPrepareHeader(hWaveOut, ref WaveHeader, Marshal.SizeOf(WaveHeader)) != MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Error preparing Header!");
                return;
            }
              
            // ** Play the sound! **
            m_PlayEvent.Reset();

            if (waveOutWrite(hWaveOut, ref WaveHeader, Marshal.SizeOf(WaveHeader)) != MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Error writing to sound card!");
                return ;
            }
              
            // ** Wait until sound finishes playing
            m_PlayEvent.WaitOne();
              
            // ** Unprepare our wav header **
            if (waveOutUnprepareHeader(hWaveOut, ref WaveHeader, Marshal.SizeOf(WaveHeader)) != MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Error unpreparing header!");
                return;
            }
              
            // ** close the wav device **
            if (waveOutClose(hWaveOut) != MMSYSERR_NOERROR)
            {
                Debug.WriteLine("Sound card cannot be closed!");
                return;
            }

            // ** Release our event handle **
            //m_PlayEvent.Close();
        }

        public static void PlayBeep(UInt16 frequency, int msDuration, UInt16 volume = 16383)
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
