# Time Server Stress Test (Version 1.1 - 2026-08-22)

## OVERVIEW

Time Server Stress Test is a Windows desktop utility for authorized stress testing of NTP time servers on private networks.

The application sends concurrent UDP NTP requests to a selected server for a configurable duration and reports the total request count, requests per second, successful responses, and failed responses in real time.

![Screenshot](/Misc/screenshot.jpg)

## KEY FEATURES

- **NTP endpoint support**: Test an IP address or host name with a configurable UDP port `123` used by default.
- **Configurable test duration**: Run tests from 1 to 300 seconds.
- **Concurrent requests**: Uses from 1 to 100 concurrent workers to exercise the selected NTP server.
- **Run test**: Run a single test (one NTP request), a single stress test of multiple NTP requests for a specified duration involving a specified number of concurrent (running in parallel) tests, or automatically run multiple stress tests from concurrency 0 to 100, each for a specified duration.
- **Live results**: View total requests, requests per second, successful requests, failed requests, and remaining time while the test runs.
-  **Save results to a report**: Save results to a [report](Misc/sample_report.pdf) (.pdf format).

## GETTING STARTED

**Option 1:**

&emsp;&emsp;1.1 Download the zipped file containing the current version of this program's executable found at:

&emsp;&emsp;&emsp;&emsp;https://github.com/roblatour/TimeServerStressTest/releases/latest 

&emsp;&emsp;1.2 unzip and run the TimeServerStressTest.exe program

&emsp;&emsp;**Note:** The TimeServerStressTest.exe above program is unsigned, however

&emsp;&emsp;&emsp;&emsp;&emsp;you may also build it yourself (see Option 2 directly below)

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
   
9. Optionally, click **Create Report** to create and view a report


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
