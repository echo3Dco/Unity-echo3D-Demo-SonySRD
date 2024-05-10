# Unity-echo3D-Demo-SonySRD
Import an external 3D asset into a Sony Spatial Reality Display (SRD) project in Unity from the echo3D cloud.

![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/adbb23a8-9e7f-42f4-b0ba-fc666167f44f)

## Setup
* Built with [Unity 2021.3.25f1](https://unity3d.com/get-unity/download/archive) (Note: The echo3D Unity SDK requires 2020.3.25+).
* [Register](https://www.echo3d.com/signup?utm_term={keyword}&utm_campaign=sonysdr_tutorial&utm_source=medium&utm_medium=blog) for a FREE echo3D account.
* Clone this repo to view the sample project. The SDK has already been installed. Just upload the models to the echo3D console and add the API key and entry IDs (see below).
* Install the SRD Unity Plugin by following the instructions [here](https://www.sony.net/Products/Developer-Spatial-Reality-display/en/develop/Unity/Quickstart.html),

## echo3D Configuration
* [Upload](https://docs.echo3d.co/quickstart/add-a-3d-model) the 'Skyscraper' model from the Models folder in the Unity project to the echo3D console.
* Open the sample project in Unity
* Open the 'SRDisplaySimpleSample' scene under: Assets/SRDisplayUnityPlugin/Samples/1_SRDSimpleSample/Scenes/
  
![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/696d1bda-a6d1-46bc-a7cb-2c13c1eefc26)

* Select the 'echo3DHologram' object in the Hierarchy and add the [API key and entry ID](https://docs.echo3d.co/quickstart/access-the-console) in the Inspector.

![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/efad6cf2-5679-4e36-9a25-881e0662433e)

![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/8c4c6f0a-fb28-456c-a0c8-b526905c3a20)

![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/22b6ebda-b680-4450-82c9-963db809eb9b)

* In the echo3D console, double-click on the 'Skyscraper' card and go to '[Metadata](https://docs.echo3d.com/unity/transforming-content)'.
* Add 'scale' with value 20.

## SRD Configuration
* Adjust the SRDisplay by setting the Uniform scale the SRDisplay Manager to 100. Adjust UI elements as needed. If you intend to support both wallmount mode and standard mode, please toggle Is Wallmount Mode on the SRDisplay settings in the Inspector.
* Creating a mouse pointer as regular 2D mouse pointers won’t work on the SRD as the SRD is in 3D Space. Please use the Pointer script provided in the SRD Plugin to create a 3D mouse pointer.
  
![image](https://i.imgur.com/Y9KEBEe.png)

* Assign the mouse pointer event in the SRD Graphic Raycaster script according to the image below. The SRD Graphic Raycaster script should replace the Graphic Raycaster script on the Canvas.

![image](https://i.imgur.com/PbHgQeF.png)

* Adjust the Event System to help the SRD cameras detect controller input. In the Event System game object, replace the Standalone Input Module with the SRD Standalone Input Module from the SRD plugin. SRD Standalone Input Module extends Standalone Input Module.

![image](https://i.imgur.com/JIfsbmQ.png)

* Adjust UI view space to SRD to make sure 2D UI elements show up on the SRD. Adjust all UI elements to the SRD’s view space. Create a parent for the Canvas. Attach the SRD View Space Scale Follower to said parent.

![image](https://i.imgur.com/aWo7C60.png)

## Run
Connect your Sony SRD and click Play in Unity to watch the building appear.

No SRD? In the Unity taskbar, click _Echo3D > Load Editor Holograms_ to watch the building appear.

## Learn More
Refer to our [documentation](https://docs.echo3d.com/) or [YouTube channel](https://www.youtube.com/@echo3Dco) to learn more.
Refer to [Sony's Spatial Reality Display Developer Guide](https://www.sony.net/Products/Developer-Spatial-Reality-display/en/develop/Overview.html) docs to learn more.

## Support
Feel free to reach out at [support@echo3D.com](mailto:support@echo3D.com) or join our [support channel](https://go.echo3d.co/join) on Slack.

## Troubleshooting
Visit our troubleshooting guide [here](https://docs.echo3d.com/unity/troubleshooting).

## Screenshots
![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/de79b8ff-c00e-407f-bf8a-d4cde312c9ec)
![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/696d1bda-a6d1-46bc-a7cb-2c13c1eefc26)
![image](https://github.com/echo3Dco/Unity-echo3D-Demo-SonySRD/assets/51488480/3c162941-2c36-4d3d-88fe-f9c809863c95)

