using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Expaceo.SoundLibrary.Base;
using System.Diagnostics;

namespace Expaceo.SoundLibrary
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Sound s;

        public class PianoKey
        {
            public Rectangle keyRectangle;
            public int Frequency { get; set; }
            public bool WhiteKey { get; private set; }
            private bool IsKeyPressed;
            
            public PianoKey(int frequency, int left, int top, bool isWhite)
            {
                Frequency = frequency;

                keyRectangle = new Rectangle();
                WhiteKey = isWhite;
                IsKeyPressed = false;

                if (isWhite)
                {
                    keyRectangle.Stroke = new SolidColorBrush(Colors.Black);
                    keyRectangle.StrokeThickness = 1;
                    keyRectangle.Fill = new SolidColorBrush(Colors.White);
                    keyRectangle.Width = 20;
                    keyRectangle.Height = 80;
                    Canvas.SetLeft(keyRectangle, left);
                    Canvas.SetTop(keyRectangle, top);
                    Canvas.SetZIndex(keyRectangle, 5);
                }
                else
                {
                    keyRectangle.Stroke = new SolidColorBrush(Colors.Black);
                    keyRectangle.StrokeThickness = 1;
                    keyRectangle.Fill = new SolidColorBrush(Colors.Black);
                    keyRectangle.Width = 10;
                    keyRectangle.Height = 50;
                    Canvas.SetLeft(keyRectangle, left);
                    Canvas.SetTop(keyRectangle, top);
                    Canvas.SetZIndex(keyRectangle, 10);
                }
                keyRectangle.MouseLeftButtonDown += PianoKeyPress;
                keyRectangle.MouseLeftButtonUp += PianoKeyRelease;
                keyRectangle.MouseLeave += PianoKeyMouseLeave;
            }

            private void PianoKeyPress(object sender, MouseButtonEventArgs e)
            {
                IsKeyPressed = true;
                keyRectangle.Fill = new SolidColorBrush(WhiteKey?Colors.Blue:Colors.White);
                s.PlayBeep(Frequency, s.soundVolume);
            }

            private void PianoKeyRelease(object sender, MouseButtonEventArgs e)
            {
                IsKeyPressed = false;
                keyRectangle.Fill = new SolidColorBrush(WhiteKey ? Colors.White : Colors.Black);
                s.Dispose();
            }

            private void PianoKeyMouseLeave(object sender, MouseEventArgs e)
            {
                if (IsKeyPressed)
                {
                    keyRectangle.Fill = new SolidColorBrush(WhiteKey ? Colors.White : Colors.Black);
                    s.Dispose();
                    IsKeyPressed = false;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            s = new Sound(SoundStructure.WaveTypes.Sine);
            s.soundVolume = 80;
            //s.OpenDevice(samplesPerSecond);

            // bool = true pour une touche blanche
            Dictionary<int, bool> Piano = new Dictionary<int, bool>();
            Piano.Add(262, true);
            Piano.Add(277, false);
            Piano.Add(294, true);
            Piano.Add(311, false);
            Piano.Add(330, true);
            Piano.Add(349, true);
            Piano.Add(370, false);
            Piano.Add(392, true);
            Piano.Add(415, false);
            Piano.Add(440, true);
            Piano.Add(466, false);
            Piano.Add(494, true);
            Piano.Add(523, true);
            Piano.Add(554, false);
            Piano.Add(587, true);
            Piano.Add(622, false);
            Piano.Add(659, true);
            Piano.Add(698, true);
            Piano.Add(740, false);
            Piano.Add(784, true);
            Piano.Add(831, false);
            Piano.Add(880, true);
            Piano.Add(932, false);
            Piano.Add(988, true);
            Piano.Add(1047, true);

            int left = 0;
            int top = 0;
            foreach (var keyFreq in Piano)
            {
                PianoKey p = new PianoKey(keyFreq.Key, keyFreq.Value?left:left-5, top, keyFreq.Value);
                Canvas_Piano.Children.Add(p.keyRectangle);
                left += keyFreq.Value?20:0;
            }
            this.KeyDown += new KeyEventHandler(OnButtonKeyDown);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (s != null)
                s.soundVolume = (int)slider_Volume.Value;
        }

        private void CB_WaveType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (s != null)
            {
                switch (((ComboBoxItem)CB_WaveType.SelectedItem).Content.ToString())
                {
                    case "Sinusoïdal":
                        s.ChangeWaveType(SoundStructure.WaveTypes.Sine);
                        break;
                    case "Carré":
                        s.ChangeWaveType(SoundStructure.WaveTypes.Square);
                        break;
                    case "Bruit":
                        s.ChangeWaveType(SoundStructure.WaveTypes.Noise);
                        break;
                    default:
                        s.ChangeWaveType(SoundStructure.WaveTypes.Sine);
                        break;
                }
            }
        }

        private void OnButtonKeyDown(object sender, KeyEventArgs e)
        {
            Debug.WriteLine(e.Key.ToString());
        }
    }
}
