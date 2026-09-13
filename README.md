# Time Server Stress Test (Version 2 - 2026-09-13)

## OVERVIEW

Time Server Stress Test is a Windows desktop utility for authorized stress testing of NTP time servers on private networks.

Based on configurable settings the application sends concurrent UDP NTPv4 requests to a selected server, reports testing results in real time, and optionally creates a report in .pdf format and .csv extract file.

![Screenshot](/Misc/screenshot.jpg)

## KEY FEATURES

- **NTP endpoint support**: Test an IP address or host name with a configurable UDP port `123` used by default.
- **Configurable:**
> - **Test duration**
> - **Max requests/second**
> - **Concurrent requests** 
> - **Test mode:** (Paced or Saturated)
> - **Test type:** run a single test (one NTP request only); a single stress test; or multiple stress tests automatically one after the other.
- **Live results**: View various result in real time as the tests runs.
-  **Save the final results** to either or both a:
> - **report:** with your own notes [example](Misc/sample_report.pdf) (.pdf format)
> - **csv file extract:** [example](Misc/sample_report.csv) - documented
    [here](Misc/Stress_Test_CSV_values.md)


## GETTING STARTED

**Option 1:**

&emsp;&emsp;1.1 Download the zipped file containing the current version of this program's executable found at:

&emsp;&emsp;&emsp;&emsp;https://github.com/roblatour/TimeServerStressTest/releases/latest 

&emsp;&emsp;1.2 unzip all its contents into a single folder and double click on the
TimeServerStressTest.exe program to run it.

&emsp;&emsp;**Notes:** 
>> The TimeServerStressTest.exe while verified on Github is unsigned, so you 
may get a Windows security alert
>>
>>However, you may build it yourself (see Option 2 directly below) should you have concerns

**Option 2:**

&emsp;&emsp;2.1 Clone this repository and open `TimeServerStressTest.slnx` in Visual Studio 2026 or later

&emsp;&emsp;2.2 Build and run the `TimeServerStressTest` project

**Options 1 and 2 (continued):**

3. Enter the host name or IP address of an NTP server that you're authorized to test
   
4. Specify a port if it differs from the default NTP port (`123`)
   
5. Specify a test duration in seconds (or use the default) 
   
6. (Optionally for Single tests) Specify the number of concurrent tests that should be run (or use the default)
   
7. Click either **Start a single test**, **Start a single stress test** or **Start multiple stress tests"** and if prompted the **Confirm** button to confirm that you are authorized to test the server

8. Review the live test results, or select **Stop** to end the test early
   
9. Optionally, click **Create Report** to create and view a report and/or the cvs extract


## AUTHORIZED USE ONLY

**Use this application only to test time servers that you own or are explicitly authorized to test. Do not direct stress tests any public NTP servers, public time-server pools, or any other system without permission. Such testing may disrupt services and may cause your IP address to be blocked or banned.**

## OPEN SOURCE & LICENSE

Time Server Stress Test is open source and distributed under the [MIT License](LICENSE). You are free to use, modify, and distribute the program.

---

## Support Time Server Stress Test

To help support Time Server Stress Test, or to just say thanks, you're welcome to 'buy me a coffee'<br><br>
[<img alt="buy me  a coffee" width="200px" src="https://cdn.buymeacoffee.com/buttons/v2/default-blue.png" />](https://www.buymeacoffee.com/roblatour)

---

## ABOUT THE AUTHOR

Created by Rob Latour. Check out more projects at [github.com/roblatour](https://github.com/roblatour?tab=repositories).

---

Copyright © 2026, Rob Latour
