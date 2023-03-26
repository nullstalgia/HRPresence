# HRPresence

Windows GATT heartrate monitor tool that pushes BPM to OpenSoundControl (OSC) for VRChat or ChilloutVR.
Simply connect any generic Bluetooth heartrate monitor to your computer and run the application!

```toml
# config.toml

# Restart if x seconds of no communication
time_out_interval = 4.0

# Wait x seconds before restarting in case of any errors
restart_delay = 4.0

# the port to send OSC data to
oscport = 9000
```

## OSC Parameters

| Parameter       | Path                               | Description                        |
| --------------- | ---------------------------------- | ---------------------------------- |
| `HR`            | `/avatar/parameters/HR`            | actual heartrate as int            |
| `onesHR`        | `/avatar/parameters/onesHR`        | ones digit                         |
| `tensHR`        | `/avatar/parameters/tensHR`        | tens digit                         |
| `hundredsHR`    | `/avatar/parameters/hundredsHR`    | hundreds digit                     |
| `floatHR`       | `/avatar/parameters/floatHR`       | maps 0:255 to 0.0:1.0             |
| `isHRBeat`      | `/avatar/parameters/isHRBeat`      | bool set when heart beats          |
| `isHRConnected` | `/avatar/parameters/isHRConnected` | bool set when HR monitor connected |
