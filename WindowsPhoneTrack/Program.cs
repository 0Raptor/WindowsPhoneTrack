using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

/*
    Windows agent for tracking a device using the Nextcloud-App PhoneTrack
    Copyright (C) 2022  0Raptor

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace WindowsPhoneTrack
{
    class Program
    {
        //preset parameters
        static readonly string usragentname = "0Raptor_Windows-Agent"; //space %20, no special characters
        static readonly string emtpyConfig = "<conf>\n\t<phonetrackuri></phonetrackuri>\n\n\t" +
            "<expectedanswer></expectedanswer>\n\n\t<minposchange>100</minposchange>\n\t<minaccuracy>120</minaccuracy>\n\t" +
            "<forceposupdate>24</forceposupdate>\n\t<ignorealtifzero>true</ignorealtifzero>\n\t<includebattery>true</includebattery>\n\n\t" + 
            "<verboseoutput>false</verboseoutput>\n</conf>";

        //loaded configurable parameters
        static string phonetrackuri = "";
        static string expectedanswer = "";
        static int minposchange = 0;
        static int minaccuracy = 0;
        static UInt64 forceposupdate = 0;
        static bool ignorealtifzero = false;
        static bool includebattery = false;
        static bool verboseoutput = false;

        static void Main(string[] args)
        {
            //copyright info
            Console.WriteLine("WindowsPhoneTrack Copyright(C) 2022  0Raptor - GNU GENERAL PUBLIC LICENSE v3");
            Console.WriteLine("This program comes with ABSOLUTELY NO WARRANTY.");
            Console.WriteLine("");

            Console.WriteLine("Starting 0Raptor's Nextcloud/PhoneTrack-GPS-Tracker for Windows...");

            //Load configuration
            LoadConfig();

            //creating new Location-Object that will house a async service which will constantly check device's position with gps
            Location myLocation = new Location();
            myLocation.GetLocationEvent();

            //tell user how to end program
            Console.WriteLine("Enter any key to quit.");
            Console.ReadLine();
        }

        // This class loads configuration from file "WindowsPhoneTrack.conf"
        static void LoadConfig()
        {
            //config was not created yet
            if (!File.Exists("WindowsPhoneTrack.conf"))
            {
                Console.WriteLine("[ERROR] No configuration was found. Creating one...");
                File.WriteAllText("WindowsPhoneTrack.conf", emtpyConfig);
                Console.WriteLine("Exiting...");
                Environment.Exit(2);
            }

            Console.WriteLine("Loading configuration...");

            try
            {
                //Create XmlDocument-Object and load configuration file
                XmlDocument doc = new XmlDocument();
                doc.Load("WindowsPhoneTrack.conf");

                //Extract parameters
                phonetrackuri = doc.SelectSingleNode("conf/phonetrackuri").InnerText;
                expectedanswer = doc.SelectSingleNode("conf/expectedanswer").InnerText;
                minposchange = Convert.ToInt32(doc.SelectSingleNode("conf/minposchange").InnerText);
                minaccuracy = Convert.ToInt32(doc.SelectSingleNode("conf/minaccuracy").InnerText);
                forceposupdate = Convert.ToUInt64(doc.SelectSingleNode("conf/forceposupdate").InnerText);
                ignorealtifzero = Convert.ToBoolean(doc.SelectSingleNode("conf/ignorealtifzero").InnerText);
                includebattery = Convert.ToBoolean(doc.SelectSingleNode("conf/includebattery").InnerText);
                verboseoutput = Convert.ToBoolean(doc.SelectSingleNode("conf/verboseoutput").InnerText);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Failed to load configuration: " + ex.Message);
                Console.WriteLine("  Check settings in \"WindowsPhoneTrack.conf\" and consider consulting the README");
                Environment.Exit(1);
            }

            if (String.IsNullOrWhiteSpace(phonetrackuri)) //url to send data to has to be specified! above parameters have defaults
            {
                Console.WriteLine("[ERROR] Configuration is invalid! Field \"phonetrackuri\" has to be specified in \"WindowsPhoneTrack.conf\".");
                Environment.Exit(1);
            }

            Console.WriteLine("Loaded.");
        }

        class Location
        {
            GeoCoordinateWatcher watcher;

            public void GetLocationEvent()
            {
                this.watcher = new GeoCoordinateWatcher();
                this.watcher.PositionChanged += new EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>>(watcher_PositionChanged);
                bool started = this.watcher.TryStart(false, TimeSpan.FromMilliseconds(2000));
                if (!started)
                {
                    Console.WriteLine("[WARNING] GeoCoordinateWatcher timed out on start.");
                }
                else
                {
                    Console.WriteLine("Ready.");
                }
            }

            // Function to read battery status
            string GetBattery()
            {
                if (includebattery) //only access data if configured --> else return 0
                {
                    string charge = "0";

                    ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_Battery");

                    foreach (ManagementObject mo in mos.Get())
                    {
                        //Console.WriteLine("Battery Name\t:{0}", mo["Name"]);
                        //Console.WriteLine("Charge \t\t:{0}%", mo["EstimatedChargeRemaining"]);

                        charge = String.Format("{0}", mo["EstimatedChargeRemaining"]); //store parameter in string
                        break; //only interested in one result --> directly leave loop
                    }

                    return charge;
                }
                else
                {
                    return "0";
                }
            }

            void watcher_PositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
            {
                UpdatePosition(e.Position.Location.Latitude, e.Position.Location.Longitude, e.Position.Location.Altitude, e.Position.Location.HorizontalAccuracy);
            }

            GeoCoordinate lastPos; //keep last send position to track if device has been moved
            UInt64 lastTime; //log last time data has been send to the server

            void UpdatePosition(double Latitude, double Longitude, double Altitude, double Accuracy)
            {
                //get additional parameters
                UInt64 timestamp = Convert.ToUInt64((DateTime.UtcNow - DateTime.Parse("1/1/1970")).TotalSeconds); //timestamp in unix/ epoch format
                string charge = GetBattery();

                //check if accuracy is suitable
                //In this case: Higher accuracy = worse - accuracy is radius in meter the device may be inside
                if (Accuracy > minaccuracy) //Accuracy is more vague (higher radius) than required minimum accuracy
                {
                    if (verboseoutput) Console.WriteLine("[INFO] Dropped position due to unsuitable accuracy (Latitude: {0}, Longitude {1} - Accuracy {2})", Latitude, Longitude, Accuracy);
                    return;
                }

                //check if device traveld far enough (from last point) to commit new position (except first run or last send is to old)
                if (lastPos != null) //first run
                {
                    double distanceTraveled = lastPos.GetDistanceTo(new GeoCoordinate(Latitude, Longitude));
                    if (distanceTraveled < minposchange) //distance moved is to low
                    {
                        if (forceposupdate == 0 || (timestamp - lastTime) < (forceposupdate * 60 * 60)) //forcedupdate is diasabled OR time expired since last send is higher than max
                        {
                            if (verboseoutput) Console.WriteLine("[INFO] Dropped position due to minor position change (Latitude: {0}, Longitude {1} - Change {2})", Latitude, Longitude, distanceTraveled);
                            return;
                        }
                    }
                }

                if (verboseoutput) Console.WriteLine("[INFO] New position (Latitude: {0}, Longitude {1} - Accuracy {2})", Latitude, Longitude, Accuracy);

                //store current data for next comparison
                lastPos = new GeoCoordinate(Latitude, Longitude);
                lastTime = timestamp;

                //prepare link
                string url = phonetrackuri + "?timestamp=" + timestamp + "&lat=" + Latitude + "&lon=" + Longitude;
                if (!ignorealtifzero || Altitude != 0) //add altitude if it is not zero or it is configured to be added anyway
                    url += "&alt=" + Altitude;
                url += "&acc=" + Accuracy;
                if (includebattery) //add battery state if configured
                    url += "&bat=" + charge;
                url += "&useragent=" + usragentname;
                if (url.Contains(",")) //replace , with . if client is using comma as seperator
                    url = url.Replace(",", ".");

                //send data
                if (verboseoutput) Console.WriteLine("[INFO] Conneting to API using following URL: " + url);
                string res = HttpGet(url);
                if (verboseoutput && res != null) Console.WriteLine("[INFO] Connection result: " + res);

                //check answer (if configured)
                if (!String.IsNullOrWhiteSpace(expectedanswer) && res != expectedanswer)
                {
                    Console.WriteLine("[WARNING] Server responded with unexpected message \"{0}\". Expected \"{1}\"", res, expectedanswer);
                    lastPos = null; //reset lastPos so position will be sent as soon as connection is working as expected
                }
            }

            /// <summary>
            /// Function to get data from an URL
            /// </summary>
            /// <param name="uri">URL to connect server</param>
            /// <returns>Returned </returns>
            string HttpGet(string uri)
            {
                string content = null;
                try
                {
                    var wc = new WebClient();
                    content = wc.DownloadString(uri);
                } 
                catch (Exception ex)
                {
                    Console.WriteLine("[WARNING] Failed to connect to server: " + ex.Message);
                    lastPos = null; //reset lastPos so position will be sent as soon as connection can be established again
                }
                return content;
            }
        }
    }
}