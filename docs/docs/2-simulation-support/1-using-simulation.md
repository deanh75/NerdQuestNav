---
title: Using Simulation
---
# Using QuestNav with WPILib Simulation

QuestNav supports connecting directly to your development computer for testing with WPILib robot simulation. This feature allows you to develop and test your robot code using QuestNav pose data without needing a physical robot.

:::tip
Simulation mode is perfect for validating robot code during the off-season or when the robot isn't available.
:::

## Prerequisites

Before setting up simulation mode, ensure you have:

1. **Quest headset** with QuestNav installed and configured
2. **Development computer** with WPILib and your robot code
3. **USB cable** for connecting Quest to computer
4. **ADB (Android Debug Bridge)** installed on your computer

:::note
ADB should already be installed if you followed the [Device Setup](../getting-started/device-setup) guide, as it's required for enabling developer mode and installing QuestNav.
:::

## Setting Up Simulation Connection

### Step 1: Connect Quest to Computer

1. Connect your Quest headset to your development computer using a USB cable
2. Put on the headset and allow USB debugging when prompted
3. Select "Always allow from this computer" for convenience

### Step 2: Configure ADB Port Forwarding

Open a command prompt or terminal on your computer and run:

```bash
adb reverse tcp:5810 tcp:5810
```

This command creates a reverse port forward, allowing the Quest to connect to NetworkTables running on your computer.

:::tip
The `adb reverse` command forwards connections from the Quest's port 5810 to your computer's port 5810, which is the standard NetworkTables port used by WPILib simulation.
:::

### Step 3: Start WPILib Simulation

1. Open your robot project in VS Code
2. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac) to open the command palette
3. Type "WPILib: Simulate Robot Code" and select it

:::note
Make sure your robot code includes QuestNav integration as described in the [Robot Code Setup](../getting-started/robot-code) section. The simulation will use the same NetworkTables communication.
:::

### Step 4: Connect QuestNav to Simulation

1. Launch QuestNav on your Quest headset
2. Tap the **"Connect to Sim"** button in the QuestNav interface
3. Verify the connection status shows as connected

:::info
The "Connect to Sim" button automatically sets the team number to "localhost", which configures QuestNav to connect to IP address 127.0.0.1 (your local computer) instead of a robot on the network.
:::

## How It Works

When you use simulation mode, QuestNav changes its connection behavior:

1. **Normal Mode**: Connects to `10.TE.AM.2` (robot RoboRIO)
2. **Simulation Mode**: Connects to `127.0.0.1` (local computer)

The ADB reverse port forwarding creates a tunnel that allows the Quest to reach your computer's NetworkTables server as if it were connecting to a robot.

```
Quest (127.0.0.1:5810) → ADB Reverse → Computer (localhost:5810) → WPILib Sim
```

## Using Pose Data in Simulation

Your robot code will receive the same QuestNav pose data in simulation as it would on a real robot.
Since you're getting real tracking data from the Quest, you can physically move around your development space to test different robot positions and orientations in simulation.



## Troubleshooting Simulation Issues

### Quest Not Connecting to Simulation

**Check ADB Connection:**
```bash
adb devices
```
Should show your Quest device as connected.

**Verify Port Forwarding:**
```bash
adb reverse --list
```
Should show `tcp:5810 tcp:5810` in the output.

**Reset Port Forwarding:**
If having issues, try resetting:
```bash
adb reverse --remove tcp:5810
adb reverse tcp:5810 tcp:5810
```

### NetworkTables Connection Issues

1. **Verify WPILib Simulation is Running**
    - Check that robot simulation started successfully
    - Look for NetworkTables server messages in console

2. **Check Firewall Settings**
    - Ensure port 5810 isn't blocked by firewall
    - Add exception for WPILib simulation if needed

3. **Try Different USB Port/Cable**
    - Some USB ports may not support data transfer
    - Use a USB cable marked for data, not just charging

### Quest Shows Wrong IP Address

If QuestNav shows an IP other than 127.0.0.1 after clicking "Connect to Sim":

1. Force close and restart QuestNav app
2. Verify ADB reverse command was executed successfully
3. Try disconnecting and reconnecting USB cable

:::warning
If the "Connect to Sim" button doesn't change the target IP to 127.0.0.1, the simulation connection won't work. Make sure you're using the latest version of QuestNav that includes this feature.
:::

## Disconnecting from Simulation

When finished with simulation:

1. **Stop WPILib Simulation** in VS Code
2. **Close QuestNav** or switch back to normal team number
3. **Remove Port Forwarding** (optional):
   ```bash
   adb reverse --remove tcp:5810
   ```

:::tip
You don't need to remove port forwarding between sessions, but it's good practice to clean up when completely finished with simulation work.
:::

## Video Guide
[Placeholder for Simulation Video Guide]

## Next Steps

Once you've tested your robot code in simulation, you can deploy it to your physical robot with confidence. The same QuestNav integration code will work seamlessly in both environments.

For deploying to a physical robot, return to the [Robot Code Setup](../getting-started/robot-code) section to ensure proper NetworkTables configuration for robot operation.