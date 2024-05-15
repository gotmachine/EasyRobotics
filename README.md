# EasyRobotics

### Overview

EasyRobotics is an **inverse kinematics** (IK) controller for the KSP Breaking Grounds DLC rotation and hinge servo parts. It allow to setup an **IK chain** made of several **servos** and of an **effector**. The controller adjust the servos target angle so the effector automatically reach a position and/or rotation in space which can be either manually defined or automatically set from a **target** part.

EasyRobotics is available from the Part Action Window (PAW) for the KAL-1000 part, as well as any modded part featuring the stock robotic controller.

### Download and installation

Compatible with **KSP 1.12.3** to **1.12.5** - Available on CKAN

**Manual installation**

- Go to the **[GitHub release page](https://github.com/gotmachine/EasyRobotics/releases)** and download the file named `EasyRobotics_x.x.x.zip`
- Open the downloaded *.zip archive
- Open the `GameData` folder of your KSP installation
- Delete any existing `EasyRobotics` folder in your `GameData` folder
- Copy the `EasyRobotics` folder found in the archive into your `GameData` folder

### License

MIT

### User manual

All controls are available from the KAL-1000 Part Action Window.

#### IK chain setup
The first step for using the IK controller is to select which servos will be used, as well as which part is to be used as the effector.
This can be done from the `IK Configuration` section, both in the editor and in flight :

### Changelog

#### 1.0.0 - 15/05/2024
