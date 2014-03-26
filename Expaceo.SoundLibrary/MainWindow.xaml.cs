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
        public Sound s = new Sound();

        public MainWindow()
        {
            InitializeComponent();
            //SoundLibrary.Base.Sound.PlayBeep(440, 1000);

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int frequency;
            int samplesPerSecond;
            short bitsPerSample;
            int.TryParse(TB_Frequency.Text, out frequency);
            int.TryParse(((ListBoxItem)CB_SamplePerSec.SelectedItem).Content.ToString(), out samplesPerSecond);
            short.TryParse(((ListBoxItem)CB_BitsPerSample.SelectedItem).Content.ToString(), out bitsPerSample);
            s.PlayOKBeep(frequency, samplesPerSecond, bitsPerSample);
        }
    }
}
