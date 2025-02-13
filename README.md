# SMARC Unity Assets
This is a package containing all of the SMARC Unity assets and scripts including vehicles, dynamics, sensors and more. 

If you:
- Are new to Unity
- Want to avoid installation
- Just want a quick demo of the project

I suggest starting with one of the pre-configured projects:
- [HDRP](https://github.com/smarc-project/SMARCUnityHDRP)
  - Better graphical fidelity, heavier on the GPU.
  - Includes the Unity Water Simulation, water is dynamic and waves exist.
- [Standard](https://github.com/smarc-project/SMARCUnityStandard)
  - Lesser graphical fidelity, but runs easier.
  - Does not have Unity Water Simulation (water is a plane).

You can find more detailed instructions on using these projects [here](./Documentation/README.md).

## Installation for non-SMaRC projects

We rely on a few non unity asset store packages, so follow the instructions below to install the package into your project.
It might seem like a lot of things to do, but all of the operations can be done in the Unity editor and should not take more than a few minutes.

We recommend using the the Editor Version **2023.1.13f1**. 


### Install Packages for Unity from Github

1. Open Package Manager window (Window | Package Manager)
2. Click `+` button on the upper-left of a window, and select "Add package from git URL..."
3. Enter the following URL and click `Add` button

```
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```
```
https://github.com/Unity-Technologies/ROS-TCP-Connector.git?path=/com.unity.robotics.ros-tcp-connector
```
```
https://github.com/Unity-Technologies/URDF-Importer.git?path=/com.unity.robotics.urdf-importer
```

### Install NuGet packages

Once you have installed NuGet, install the following packages.

1. Click the NuGet dropdown at the top of your Unity Editor (Sometimes requires editor restart to appear).
2. Manage NuGet packages
3. Search and install:
  *  MathNet.Numerics
  *  CoordinateSharp

### Configure the ROS Connector

Our codebase is scripted towards ROS 2. You will need to change some default settings to ensure your messages are compiled for ROS 2.
The code will work with ROS 1, but you will need to update the scripts to support ROS 1 messages yourself.

1. Open the ROS settings menu (Robotics | ROS Settings)
2. Change the `Protocol` to "ROS 2"
3. Click on "Apply"
4. Wait for the compilation to finish.

### Install SMARC Unity Assets

Once all the dependencies are installed and configured, you can install this package using the same method as before.

1. Open Package Manager window (Window | Package Manager)
2. Click `+` button on the upper-left of a window, and select "Add package from git URL..."
3. Enter the following URL and click `Add` button

```
https://github.com/smarc-project/SMARCUnityAssets
```
