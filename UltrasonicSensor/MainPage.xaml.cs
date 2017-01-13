using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.Devices.Gpio;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using System.Net.Http;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UltrasonicSensor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private GpioPin echoPin;
        private GpioPin triggerPin;
        private Stopwatch duracionPulso;
        private CoreDispatcher dispatcher;
        private List<double> distArray;
        private bool status = true;
        private double avg = 0.0;

        private async void informChange()
        {
            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                    {
                       { "state", avg.ToString() }
                    };

                var content = new FormUrlEncodedContent(values);
                var baseUri = new Uri("http://192.168.43.207:3000/");
                var uri = new Uri(baseUri, "snmrtn/001");
                var response = await client.PostAsync(uri, content);
            }
        }

        private async void echoValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            if (e.Edge == GpioPinEdge.RisingEdge)
            {
                duracionPulso.Start();
            }
            else if (e.Edge == GpioPinEdge.FallingEdge)
            {
                duracionPulso.Stop();
                long microSeconds = duracionPulso.ElapsedTicks / (Stopwatch.Frequency / (1000L * 1000L));
                duracionPulso.Reset();
                double distancia = microSeconds * 0.01715;
                distArray.Add(distancia);
                if (distArray.Count == 100)
                {
                    avg = distArray.Average();
                    distArray.Clear();
                    informChange();
                }
                dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Distance.Text = distancia.ToString();
                });
            }
        }

        private void InitGPIO()
        {
            duracionPulso = new Stopwatch();
            var gpio_controller = GpioController.GetDefault();

            /* Show an error if there is no GPIO controller */
            if (gpio_controller == null)
            {
                throw new Exception("There is no GPIO controller on this device");
            }

            echoPin = gpio_controller.OpenPin(23);
            triggerPin = gpio_controller.OpenPin(24);

            /* GPIO state is initially undefined, so we assign a default value before enabling as output */
            triggerPin.Write(GpioPinValue.Low);
            echoPin.SetDriveMode(GpioPinDriveMode.Input);
            triggerPin.SetDriveMode(GpioPinDriveMode.Output);
            echoPin.ValueChanged += echoValueChanged;
        }

        public void triggerOut(object sender, object e)
        {
            triggerPin.Write(GpioPinValue.High);
            Task.Delay(TimeSpan.FromMilliseconds(0.01)).Wait();
            triggerPin.Write(GpioPinValue.Low);
        }

        public MainPage()
        {
            this.InitializeComponent();
            dispatcher = Windows.UI.Core.CoreWindow.GetForCurrentThread().Dispatcher;
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += triggerOut;
            distArray = new List<double>(100);
            InitGPIO();
            timer.Start();
        }
    }
}
