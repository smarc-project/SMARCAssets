# SMARC Unity Assets
This is a package containing all of the SMARC Unity assets and scripts including vehicles, dynamics, sensors and more. 

## Quick start
If you:
- Are new to Unity
- Want to avoid installation
- Just want a quick demo of the project

We suggest starting with the pre-configured project: [Standard](https://github.com/smarc-project/SMARCUnityStandard)

If you wish to add higher graphical fidelity, [enable HDRP](Runtime/HDRP/README.md) 

You can find more detailed instructions on using all available vehicles, sensors, connections, UIs etc. [here](./Documentation/README.md).

## Help!
If at any point, you needed help, got the help, and the help was not about using Unity (Good example:"How do I make my vehicle buoyant?". Bad example: "How do I move this object in the editor?"), please [open an issue in github](https://github.com/smarc-project/SMARCUnityAssets/issues) (and tag it with "Documentation" if you can). 
That way, we can identify gaps in our documentation and hopefully add them in for the next person.


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
