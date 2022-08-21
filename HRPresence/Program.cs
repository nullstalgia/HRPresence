using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Tomlyn;
using Tomlyn.Model;

namespace HRPresence
{
    internal class Config : ITomlMetadataProvider
    {
        public float TimeOutInterval { get; set; } = 3f;
        public float RestartDelay { get; set; } = 3f;
        public int OSCPort { get; set; } = 9000;
        public TomlPropertiesMetadata PropertiesMetadata { get; set; }
    }

    internal class Program
    {
        private static HeartRateService heartrate;
        private static HeartRateReading reading;
        private static OscService osc;

        private static DateTime lastUpdate = DateTime.MinValue;

        public static bool isHRConnected;
        private static bool isHeartBeat;
        private static int currentHR;

        private static void Main()
        {
            var config = new Config();
            if (File.Exists("config.toml"))
            {
                config = Toml.ToModel<Config>(File.OpenText("config.toml").ReadToEnd());
            }
            else
            {
                File.WriteAllText("config.toml", Toml.FromModel(config));
            }

            Console.CursorVisible = false;
            Console.WindowHeight = 4;
            Console.WindowWidth = 32;

            osc = new OscService();
            osc.Initialize(System.Net.IPAddress.Loopback, config.OSCPort);

            heartrate = new HeartRateService();
            heartrate.HeartRateUpdated += heart =>
            {
                reading = heart;
                currentHR = heart.BeatsPerMinute;

                Console.Write($"{DateTime.Now}\n{currentHR} BPM\n");
                Console.SetCursorPosition(0, 0);

                lastUpdate = DateTime.Now;
                File.WriteAllText("rate.txt", $"{currentHR}");

                osc.Update(currentHR);
                if (!isHeartBeat)
                    HeartBeat();
            };

            while (true)
            {
                if (DateTime.Now - lastUpdate > TimeSpan.FromSeconds(config.TimeOutInterval + 2))
                {
                    isHRConnected = false;
                    osc.Clear();
                }

                if (DateTime.Now - lastUpdate > TimeSpan.FromSeconds(config.TimeOutInterval))
                {
                    Console.Write("Connecting...");
                    Console.SetCursorPosition(0, 0);
                    while (true)
                    {
                        try
                        {
                            heartrate.InitiateDefault();
                            isHRConnected = true;
                            break;
                        }
                        catch (Exception e)
                        {
                            isHRConnected = false;
                            osc.Clear();
                            Console.Write($"Failure while initiating heartrate service, retrying in {config.RestartDelay} seconds:\n");
                            Debug.WriteLine(e);
                            Console.SetCursorPosition(0, 0);
                            Thread.Sleep((int)(config.RestartDelay * 1000));
                        }
                    }
                }

                Thread.Sleep(2000);
            }
        }

        private static void HeartBeat()
        {
            if (currentHR == 0 || !isHRConnected)
            {
                isHeartBeat = false;
                return;
            }
            isHeartBeat = true;

            // https://github.com/200Tigersbloxed/HRtoVRChat_OSC/blob/c73ae8224dfed35e743c0c436393607d5eb191e8/HRtoVRChat_OSC/Program.cs#L503
            // When lowering the HR significantly, this will cause issues with the beat bool
            // Dubbed the "Breathing Exercise" bug
            // There's a 'temp' fix for it right now, but I'm not sure how it'll hold up
            float waitTime = default(float);
            try { waitTime = 1 / ((currentHR - 0.1f) / 60); } catch (DivideByZeroException) { /*Just a Divide by Zero Exception*/ }
            new ExecuteInTime((int)(waitTime * 1000), (eit) =>
            {
                osc.SendBeat();
                HeartBeat();
            });
        }

        public class ExecuteInTime
        {
            public bool IsWaiting { get; private set; }
            private System.Timers.Timer _timer;

            public ExecuteInTime(int ms, Action<ExecuteInTime> callback)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Close();
                }
                _timer = new System.Timers.Timer(ms);
                _timer.AutoReset = false;
                _timer.Elapsed += (sender, args) =>
                {
                    callback.Invoke(this);
                    IsWaiting = false;
                    _timer.Stop();
                    _timer.Close();
                };
                _timer.Start();
                IsWaiting = true;
            }
        }
    }
}