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
                s.PlayBeep(Frequency, 100);
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
            //SoundLibrary.Base.Sound.PlayBeep(440, 1000);
            int samplesPerSecond;
            int.TryParse(((ComboBoxItem)CB_SamplePerSec.SelectedItem).Content.ToString(), out samplesPerSecond);
            s = new Sound(samplesPerSecond);
            s.OpenDevice(samplesPerSecond);

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

            int left = 0;
            int top = 0;
            foreach (var keyFreq in Piano)
            {
                PianoKey p = new PianoKey(keyFreq.Key, keyFreq.Value?left:left-5, top, keyFreq.Value);
                Canvas_Piano.Children.Add(p.keyRectangle);
                left += keyFreq.Value?20:0;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int frequency;
            int volume;

            int.TryParse(TB_Frequency.Text, out frequency);
            int.TryParse(TB_Volume.Text, out volume);
            s.PlayBeep(frequency, volume);
        }

        private void Button_Stop_Click(object sender, RoutedEventArgs e)
        {
            s.Dispose();
        }

        private void TB_Volume_LostFocus(object sender, RoutedEventArgs e)
        {
            int volume;

            int.TryParse(TB_Volume.Text, out volume);
            s.soundVolume = volume;
        }
    }
}
