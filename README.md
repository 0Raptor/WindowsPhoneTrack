# Windows-Agent for Nextcloud/PhoneTrack

A console application meant to be run as a service that enables you to track your Windows devices with the [Nextcloud-App "PhoneTrack"](https://apps.nextcloud.com/apps/phonetrack).  
  
Compiled with: .NET Framework 4.7.2

## Installation

1. Setup and validate  
	- Download (or compile) executable ("WindowsPhoneTrack.exe") - further referred as EXE  
	- Place EXE inside empty folder on your boot drive  
	- Run EXE --> config ("WindowsPhoneTrack.conf") will be created beside it  
	- Open config, enter information as described below (enable verboseoutput), save file and exit  
	- Run EXE again to validate that a position is obtained and data send to your cloud  
		- Open Nextcloud/PhoneTrack and check that the send data can be displayed  
	- Consider disabling verboseoutput  
	- Create a backup of your configuration
2. Create a service that will be executed by the system on startup
	- Easiest way is to use the "Non-Sucking Service Manager"  
		- Download and install from their [website](https://nssm.cc/)  
		- Install using [Chocolatey](https://chocolatey.org/)  
			- `choco install nssm`  
	- Open CMD as admin  
	- Run `nssm install`  
		- GUI opens  
		- Use the "..."-button after "Path" and select the EXE  
		- Make sure "Startup directory" is the same the EXE and config are located in  
		- Enter a name (e.g. WindowsPhoneTrack) at "Service name" and hit "Install service"  
	- Run `nssm start WindowsPhoneTrack`  
	- Done  
 

## Configuration

Configuration is located in the .conf-file next to the executable.  
Parameters (each one must be specified except the 2nd):  
- *phonetrackuri*  
	- No default  
	- URL to your Nextcloud hosting the PhoneTrack-App  
		- `https://<YOUR_NC_URL>/apps/phonetrack/logGet/<TOKEN>/<DEVICE_NAME>`  
	- How to obtain  
		- Open Nextcloud and navigate to PhoneTrack  
		- (Optional) Click create session and enter a name  
		- Click on chain-icon of the session you want the device to be part of (normally the new created one)  
		- Copy value of "HTTP-GET-Link"-textbox into configuration file and remove everything after "MyName" (so it looks like the link above)  
		- Replace "MyName" with the name that the device should be listed as  
- *expectedanswer*  
	- No default  
	- May be left blank  
	- If this field is filled the application will check the response after the get-request against this value  
		- If the value differs a warning will be printed and the next position will be send ignoring the "minposchange" parameter  
	- My server responds: `{"done":1,"friends":[]}` on success - can be validated for your configuration in verbose-mode  
- *minposchange*  
	- Default: 100  
	- Meters the location of the device has to change until the application will send the new position to your server  
- *minaccuracy*  
	- Default: 120  
	- Minimal GPS-accuracy required  
	- The number represents the radius in meters around the obtained latitude and longitude the device may be inside: A lower number requires more precision  
		- Position will be dropped if radius is higher than specified here  
		- WARNING: Some devices may not be very accurate. Please start the application manually in verbose-mode a few times at different locations (including indoor) to get an estimate which maximal radius is suitable for your device.  
- *forceposupdate*
	- Default: 24  
	- Time in hours. The application will send a new position, ignoring *minposchange*, after this time expired  
	- Set to 0 to disable feature  
- *ignorealtifzero*  
	- Default: true  
	- Some devices may not offer an altitude with the position (or it is always zero)  
		- Set this option to "true" and the altitude will not be transmitted to the server if it is zero  
	- If your device offers an altitude-value you should disable this function, cause it is possible to be at 0 meters above sea level  
		- You can check your device if you disable this option, enable "verboseoutput" and check the URL the application displays `https://[...]&alt=<VALUE>&[...]`  
- *includebattery*  
	- Default: true  
	- If enabled the estimated charge remaining of your battery will be transmitted to the server  
		- If you have multiple batteries only the first one is taken into account  
- *verboseoutput*  
	- Default: false  
	- Display Info-Statesments in the console  
		- Obtained altitude and longitude  
		- Position dropped due to inaccuracy  
		- Position dropped due to less horizontal movement  
		- API-URL created from the collected data  
		- Status of the HTTP-GET-Request  
	- Should not be enabled when used as a service!  
If the application tells the configuration is invalid, don't hesitate to rename your current configuration, double-click the application and copy only your "phonetrackuri" in the new created config to make sure your settings aren't the cause.  

## License

This application is published undes GNU GNU GENERAL PUBLIC LICENSE Version 3 as refered in the [LICENSE-File](LICENSE).  

Copyright (C) 2022  0Raptor

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.

## Sources

The program was created with the support of the following resources
- https://gitlab.com/eneiluj/phonetrack-oc/-/wikis/userdoc#http-request
- https://stackoverflow.com/questions/4192971/in-powershell-how-do-i-convert-datetime-to-unix-time
- https://docs.microsoft.com/de-de/dotnet/api/system.device.location.geocoordinatewatcher?view=netframework-4.8 12.05.2022
- https://www.csharp-console-examples.com/csharp-console/get-laptop-battery-status-using-c-console/ 12.05.2022
- http://zuga.net/articles/cs-3-ways-to-make-an-http-get-request/ 14.05.2022