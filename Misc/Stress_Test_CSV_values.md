# Stress Test CSV Values

- **`Concurrent Requests`**: Number of additional concurrent UDP sockets configured for the test. The stress test always uses one base socket, so `0` uses one socket in total.
- **`Total Requests`**: Number of requests successfully submitted to a UDP socket during the test.
- **`Requests/Second`**: `Total Requests` divided by the send-phase duration. Single-request tests display `N/A`.
- **`Successes`**: Correlated NTP replies received within the configured grace period, measured from the request's send timestamp.
- **`Failures`**: Correlated NTP replies received after the configured grace period.
- **`Losses`**: Requests still outstanding when the drain phase ends.
- **`Success Rate`**: `Successes` divided by `Total Requests`, expressed as a percentage.
- **`Successful Requests/Second`**: `Requests/Second` multiplied by `Success Rate` (converted from a percentage to a decimal value >= 1). 
- **`Started`**: Local timestamp recorded immediately before the stress test starts.
- **`Ended`**: Local timestamp recorded after the send and drain phases complete.
- **`Configured Requests/Second`**: Aggregate configured request rate across all worker sockets. Single-request tests display `N/A`.
- **`Test Mode`**: Sender scheduling mode: `Paced` enforces the shared minimum inter-send interval, while `Saturation` sends according to the shared absolute request schedule without that pacing constraint.
- **`Send Phase Duration`**: Duration in seconds used to calculate the send rate. Duration-based runs report the configured duration; explicit request-total runs include the scheduling time through the last request's scheduled due time.
- **`Drain Phase Duration`**: Elapsed seconds after the last send during which replies are accepted. It ends when no requests remain outstanding or the required drain-period deadline is reached.
- **`Actual Sent Requests`**: Number of requests successfully submitted to a UDP socket; this is the same count as `Total Requests`.
- **`Actual Send Rate`**: `Actual Sent Requests` divided by `Send Phase Duration`.
- **`Matched Responses`**: Number of correlated replies counted as successes because they arrived within the grace period.
- **`Unmatched Responses`**: Received replies that are malformed, have an unknown request ID, or arrive after finalization. Duplicate replies are counted separately.
- **`Duplicate Responses`**: Valid replies whose request ID had already been matched by an earlier response.
- **`Timed-Out Outstanding Requests`**: Number of requests still outstanding when the drain phase ends; this is the same count as `Losses`.
- **`Maximum Send Lateness (ms)`**: Largest delay in milliseconds between a request's scheduled due time and the timestamp recorded before it is sent.
- **`Average Send Lateness (ms)`**: Mean delay in milliseconds between scheduled due times and the timestamps recorded before sends.
- **`Minimum Observed Inter-Send Gap (us)`**: Shortest measured interval in microseconds between consecutive sends across all workers.
- **`Maximum Observed Inter-Send Gap (ms)`**: Longest measured interval in milliseconds between consecutive sends across all workers.
- **`Sends Violating Minimum Gap`**: In `Paced` mode, count of consecutive sends whose measured gap was shorter than the configured aggregate interval; `Saturation` mode does not increment this metric.
- **`Schedule Recovery Duration (ms)`**: Reserved pacing-recovery metric; the current stress test writes `0`.
- **`Sender Blocked by Outstanding Window Duration (ms)`**: Reserved outstanding-window metric; the current stress test writes `0`.
- **`Grace Period (ms)`**: Maximum elapsed time after a request is sent for its correlated reply to count as a success.
- **`Required Drain Period (ms)`**: Maximum drain duration after the final send while outstanding replies are accepted.

## Notes: 

1. Concurrent requests are emulated by running multiple parallel threads. Depending on the clock speed and number of CPU cores, RAM speed, and other such hardware specs that impact on performance, stress test results will vary from computer to computer.

2. For stress tests where the number of concurrent requests + 1 exceeds the number of cores on the computer running the stress test,
   it is normal to see a drop in the total number `Total Requests` being reported (as individual cores begin processing multiple stress test threads).
   
3. To fine tune maximum successful requests/second, set (on the main window of the program) 'Maximum requests/second' slightly higher than you suspect the server can handle.

4. For best results, avoid running other workloads on the computer while it is running stress tests.
   
5. Concurrent requests = 0 means only one thread on your computer is running requests; concurrent requests = 1 means one thread is running requests and one additional thread is running concurrently (in parallel) - for a total of two threads running in total; concurrent requests = 2 means one thread is running requests and two additional threads are running concurrently - for a total of three threads running in total; etc.
    
6. When selecting `Max Request/Second` and `Duration` in the main window it is possible to ask for what would be a limiting or unreasonable stress testing combination. For example if `Max Request/Second` is 10 and `Duration` is 20, stress testing will be limited in its effectiveness.  On the other hand if you select `Max Request/Second` at 50,000 and `Duration` at 10 seconds it may be unreasonable to expect to achieve that goal.  In this situation the program will stress test the best it can within in the duration allotted.

7. Assuming a setting of `Maximum request / seconds` that exceeds the server's ability to successfully handle all requests sent to it, `Successful Requests/Second` is the most telling metric with respect to the maximum requests/second a server can handle. 
