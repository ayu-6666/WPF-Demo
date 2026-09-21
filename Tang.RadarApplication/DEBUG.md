# Tang.RadarApplication - demo verification

- [ ] Install .NET Framework 4.8 Developer Pack and Visual Studio WPF workload.
- [ ] Restore `CommunityToolkit.Mvvm` and `WPFDevelopers`.
- [ ] Run with no CAN hardware: simulator should show 8 targets and a rotating sweep.
- [ ] Install the matching Kvaser CANlib x64 runtime and implement the SDK-specific read loop in `KvaserCanDataSource.StartAsync`.
- [ ] Validate the radar CAN frame scaling against the hardware protocol before enabling live input.
- [ ] Configure `OPENAI_API_KEY` outside source control before using AI analysis.

The repository tools cannot compile or attach to local Kvaser hardware; the checks above are the required local debug steps.
