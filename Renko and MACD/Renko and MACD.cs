using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace cAlgo.Robots
{
    #region Enumerations

    /// <summary>
    /// The Type of renko chart
    /// </summary>
    public enum RenkoChartTerm
    {
        ShortTerm,
        LongTerm
    }

    #endregion

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class RenkoandMACD : Robot
    {
        #region BOT INPUTS

        [Parameter("Renko Chart Mode")]
        public string RenkoChartMode { get; set; }

        [Parameter("IoTHub Name")]
        public string IoTHubName { get; set; }

        [Parameter("device ID")]
        public string DeviceId { get; set; }

        [Parameter("Shared AccessKey")]
        public string SharedAccessKey { get; set; }

        #endregion

        #region private members

        /// <summary>
        /// Azure IoT Hub Device client for cloud interaction
        /// </summary>
        private DeviceClient deviceClient;

        /// <summary>
        /// Device IoT Hub connection string
        /// </summary>
        private string connectionString;

        /// <summary>
        /// MacdCrossOver Indicator used in this Algo
        /// </summary>
        private MacdCrossOver MacdCrossOver { get; set; }

        /// <summary>
        /// Macd Long Cycle default = 26
        /// </summary>
        private int macdLongCycle = 26;

        /// <summary>
        /// Macd Shoty Cycle default = 12
        /// </summary>
        private int macdShortCycle = 12;

        /// <summary>
        /// Macd Signal Period default = 9
        /// </summary>
        private int macdSignalPeriod = 9;

        /// <summary>
        /// Symbol this Algo is on
        /// </summary>
        private string chartSymbol;

        /// <summary>
        /// Switch for controlling telemetry
        /// </summary>
        private static bool CanSendTelemetry = false;

        /// <summary>
        /// Switch for sending telemetry only once on startup
        /// </summary>
        private static bool SendTelemetryOnce = false;
        #endregion

        #region Robot methods

        /// <summary>
        /// Robot Initialization Logic
        /// </summary>
        protected override void OnStart()
        {
            // Connection string for device
            connectionString = "HostName=" + IoTHubName + ".azure-devices.net;DeviceId=" + DeviceId + ";SharedAccessKey=" + SharedAccessKey;

            // connect device client to IoTHub
            deviceClient = DeviceClient.CreateFromConnectionString(connectionString, TransportType.Mqtt);

            // Create handlers for the direct method calls
            deviceClient.SetMethodHandlerAsync("WatcherToSymbolCommand", WatcherToSymbolCommand, null).Wait();

            // Initialize MacdCrossOver Indicator
            MacdCrossOver = Indicators.MacdCrossOver(macdLongCycle, macdShortCycle, macdSignalPeriod);

            // Collect Chart properties
            chartSymbol = Chart.SymbolName;
        }

        /// <summary>
        /// Method Called on every new bar
        /// </summary>
        protected override void OnBar()
        {
            // Send Telemetry to IoT Hub
            if (CanSendTelemetry)
                SendTelemetry();
        }

        /// <summary>
        /// Robot on every new tick logic
        /// </summary>
        protected override void OnTick()
        {
            if (SendTelemetryOnce)
            {
                SendTelemetry();
                SendTelemetryOnce = false;
            }
        }

        /// <summary>
        /// Robot Deinitialization logic
        /// </summary>
        protected override void OnStop()
        {
            // Close Device Connection
            deviceClient.CloseAsync();
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Method for sending chart telemetry
        /// </summary>
        private void SendTelemetry()
        {
            // collect previous bar and macds
            var prevBar = Chart.Bars.Last(1);
            var prevMacdsignal = MacdCrossOver.Signal.Last(2);
            var lastMacdSignal = MacdCrossOver.Signal.Last(1);
            var prevMacd = MacdCrossOver.MACD.Last(2);
            var lastMacd = MacdCrossOver.MACD.Last(1);
            var barSize = (int)Math.Abs((prevBar.Close - prevBar.Open) / Symbol.PipSize);

            // buy signal
            if (prevBar.Close > prevBar.Open)
                if (prevMacd < prevMacdsignal && lastMacd > lastMacdSignal)
                {
                    // telemetry to send
                    var messageBody = new RenkoChartInformationDTO 
                    {
                        BarType = "up",
                        BarSize = barSize
                    };

                    // send telemetry
                    var messageJsonBody = JsonConvert.SerializeObject(messageBody);
                    var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                    message.Properties.Add("symbolname", chartSymbol);
                    message.Properties.Add("chart", "renko");
                    message.Properties.Add("renkomode", RenkoChartMode);
                    message.Properties.Add("trigger", "buy");
                    deviceClient.SendEventAsync(message);
                    return;
                }

            // sell signal
            if (prevBar.Close < prevBar.Open)
                if (prevMacd > prevMacdsignal && lastMacd < lastMacdSignal)
                {
                    // telemetry to send
                    var messageBody = new RenkoChartInformationDTO 
                    {
                        BarType = "down",
                        BarSize = barSize
                    };

                    // send telemetry
                    var messageJsonBody = JsonConvert.SerializeObject(messageBody);
                    var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                    message.Properties.Add("symbolname", chartSymbol);
                    message.Properties.Add("chart", "renko");
                    message.Properties.Add("renkomode", RenkoChartMode);
                    message.Properties.Add("trigger", "sell");
                    deviceClient.SendEventAsync(message);
                    return;
                }

            // regular bullish bar
            if (prevBar.Close > prevBar.Open)
            {
                // telemetry to send
                var messageBody = new RenkoChartInformationDTO 
                {
                    BarType = "up",
                    BarSize = barSize
                };

                // send telemetry
                var messageJsonBody = JsonConvert.SerializeObject(messageBody);
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                message.Properties.Add("symbolname", chartSymbol);
                message.Properties.Add("chart", "renko");
                message.Properties.Add("renkomode", RenkoChartMode);
                message.Properties.Add("trigger", "indeterminate");
                deviceClient.SendEventAsync(message);
                return;
            }

            // regular bearish bar
            if (prevBar.Close < prevBar.Open)
            {
                // telemetry to send
                var messageBody = new RenkoChartInformationDTO 
                {
                    BarType = "down",
                    BarSize = barSize
                };

                // send telemetry
                var messageJsonBody = JsonConvert.SerializeObject(messageBody);
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                message.Properties.Add("symbolname", chartSymbol);
                message.Properties.Add("chart", "renko");
                message.Properties.Add("renkomode", RenkoChartMode);
                message.Properties.Add("trigger", "indeterminate");
                deviceClient.SendEventAsync(message);
                return;
            }
        }

        /// <summary>
        /// handle device connection testing
        /// </summary>
        private static MethodResponse TestConnection()
        {
            var responseJson = JsonConvert.SerializeObject(new WatcherResponse 
            {
                response = "connected"
            });
            return new MethodResponse(Encoding.UTF8.GetBytes(responseJson), 200);
        }

        /// <summary>
        /// CanSend Telemetry Switch
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private static MethodResponse ToggleCanSendTelemetry(string command)
        {
            CanSendTelemetry = command == "starttelemetry" ? true : command == "stoptelemetry" ? false : CanSendTelemetry;
            SendTelemetryOnce = command == "starttelemetry" ? true : command == "stoptelemetry" ? false : false;

            string responseJson = command == "starttelemetry" ? JsonConvert.SerializeObject(new WatcherResponse 
            {
                response = "startedtelemetry"
            }) : command == "stoptelemetry" ? JsonConvert.SerializeObject(new WatcherResponse 
            {
                response = "stoppedtelemetry"
            }) : "";
            return new MethodResponse(Encoding.UTF8.GetBytes(responseJson), 200);
        }

        /// <summary>
        /// Handle Watcher To Symbol Command
        /// </summary>
        /// <summary>
        /// Watcher To Device Method Router
        /// </summary>
        private static Task<MethodResponse> WatcherToSymbolCommand(MethodRequest methodRequest, object userContext)
        {
            try
            {
                // command from watcher
                var command = JsonConvert.DeserializeObject<WatcherToSymbolCommandDTO>(methodRequest.DataAsJson);

                // Select Method to run based on Command from watcher
                switch (command.Command)
                {
                    // Test connection command
                    case "testconnection":
                        {
                            return Task.FromResult<MethodResponse>(TestConnection());
                        }
                    case "starttelemetry":
                        {
                            return Task.FromResult<MethodResponse>(ToggleCanSendTelemetry(command.Command));
                        }
                    case "stoptelemetry":
                        {
                            return Task.FromResult<MethodResponse>(ToggleCanSendTelemetry(command.Command));
                        }
                    default:
                        return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new WatcherResponse 
                        {
                            response = "nomatch"
                        })), 401));
                }
            } catch
            {
            }
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new WatcherResponse 
            {
                response = "nomatch"
            })), 401));
        }

        #endregion
    }
}

