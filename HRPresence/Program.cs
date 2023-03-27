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
        private static int rrInterval;

        private static int peakBPM = 0;
        private static DateTime peakTime;

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
                rrInterval = heart.RRIntervals != null && heart.RRIntervals.Length > 0 ? heart.RRIntervals[0] : 0;


                if (currentHR > peakBPM)
                {
                    peakBPM = currentHR;
                    peakTime = DateTime.Now;
                }

                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}".PadRight(32));
                Console.WriteLine($"{currentHR} BPM".PadRight(32));
                Console.WriteLine($"Peak: {peakBPM} BPM at {peakTime.ToShortTimeString()}".PadRight(32));

                lastUpdate = DateTime.Now;
                File.WriteAllText("rate.txt", $"{currentHR}");

                osc.Update(currentHR, rrInterval);
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
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine("Connecting...".PadRight(32));
                    while (true)
                    {
                        try
                        {
                            heartrate.InitiateDefault();
                            isHRConnected = true;
                            Console.WriteLine("Connected!");
                            break;
                        }
                        catch (Exception e)
                        {
                            isHRConnected = false;
                            osc.Clear();
                            Console.Write($"Failure while initiating heartrate service, retrying in {config.RestartDelay} seconds:\n");
                            Debug.WriteLine(e);
                            //Console.SetCursorPosition(0, 0);
                            Thread.Sleep((int)(config.RestartDelay * 1000));
                        }
                    }
                }

                Thread.Sleep(2000);
            }
        }
        private static void HeartBeat()
        {
            // Check if the heart rate sensor is disconnected
            if (!isHRConnected)
            {
                isHeartBeat = false;
                return;
            }

            isHeartBeat = true;

            // Use the class-level variable for the RR interval (in ms) as the wait time between heartbeats
            int waitTime = rrInterval;
            // If the RR interval is 0, use the old method of calculating the wait time
            if (rrInterval == 0)
                waitTime = defaultWaitTime(currentHR);

            new ExecuteInTime(waitTime, (eit) =>
            {
                osc.SendBeat();
                // Recursively call HeartBeat() to maintain the heartbeat loop
                HeartBeat();
            });
        }

        private static int defaultWaitTime(int currentHR)
        {
            float waitTime = 1 / ((currentHR - 0.1f) / 60);
            return (int)(waitTime * 1000);
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