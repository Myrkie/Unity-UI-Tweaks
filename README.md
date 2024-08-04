# Unity-UI-Tweaks

## Overview
Unity-UI-Tweaks is a Unity package repository aimed at enhancing the usability and efficiency of Unity's user interface. This repository includes two Harmony patches that extend the functionality of Unity's built-in components:

1. **BlendShapeSearch**: This patch facilitates easy searching of blend shape names within Unity's Skin Mesh Renderer component. It simplifies the process of locating specific blend shapes.

2. **ConstraintTransformEditor**: This patch provides information on constraints that reference a given transform as a source. It helps identify which constraints are affecting a selected transform.

3. **AllowBlendshapeClamping**: This patch undoes a change done in VRChat SDK V3.5.0 which forces legacy blendshape clamping in editor, for people who use VRCFury blendshape baking this makes it impossible to increase blendshapes past intended values and then save the blendshapes on upload into an allowed range.

4. **DisableMouseJumping**: This will patch unity's mouse jumping feature introduced around unity 2022 and disable it, such as setting a slider and going off screen.

## Installation

### Option 1: Git Package (Recommended)
1. Ensure you have Git installed on your system and registered in the `$PATH`.
2. Copy the Git URL: https://github.com/Myrkie/Unity-UI-Tweaks.git #versiontag
3. Open Unity and navigate to `Window` > `Package Manager`.
4. Click on the `+` button in the top-left corner of the Package Manager window.
5. Select `Add package from git URL...`.
6. Paste the copied Git URL into the input field.
7. Click `Add` to import the package into your Unity project.



[UPMGit Extension] is recommended if you wish to seamlessly change versions(https://github.com/mob-sakai/UpmGitExtension)

![firefox_deAvgkySZN](https://github.com/Myrkie/Unity-UI-Tweaks/assets/20288698/172df39f-6044-4276-aa1c-10fb1cded7cf)


### Option 2: Manual Download
1. Download or clone the source code of this repository.
2. Extract the downloaded ZIP file into your unity your projects **Packages** folder


## Usage
After installation, you can start benefiting from the added functionalities provided by the Harmony patches:

- **BlendShapeSearch**: Within the Unity Editor, navigate to any GameObject with a Skinned Mesh Renderer component attached. In the Inspector window, you'll find an enhanced interface for searching and managing blend shape names under the Skinned Mesh Renderer component.

![Unity_UYKmKcdRrp](https://github.com/Myrkie/Unity-UI-Tweaks/assets/20288698/dedb23e8-fa8e-4833-96c3-2eed854f9406)



- **ConstraintTransformEditor**: When working with constraints in Unity, you can now easily identify which constraints reference a specific transform as a source. This information is displayed within the Unity Editor, aiding in the debugging and optimization of constraint-based animations and interactions.

![Unity_g7y47zO0g4](https://github.com/Myrkie/Unity-UI-Tweaks/assets/20288698/6df1769a-83b0-44e1-95ec-58fb8ec6914c)

- **AllowBlendshapeClamping**: This is an automatically applied patch and requires no user input.

- **DisableMouseJumping**: This is an automatically applied patch and requires no user input. 


## Compatibility
Unity-UI-Tweaks is compatible with Unity versions 2022.3.6f1. Ensure that your Unity project meets the minimum version requirements to utilize these enhancements.

## Requirements
Unity-UI-Tweaks requires harmony, this by default is included with VRChats SDK V3.0.0 and above as it includes harmony, if you are using an older version or plan to use Chillout VR download and add harmony to your projects asset folder from https://github.com/pardeike/Harmony/releases

## Contributing
Contributions to Unity-UI-Tweaks are welcome! If you have ideas for additional Harmony patches or improvements to existing ones, feel free to submit a pull request or open an issue on this repository.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgements
Unity-UI-Tweaks utilizes Harmony patches, an open-source library for patching Unity assemblies at runtime. Special thanks to the creators and contributors of Harmony for making these enhancements possible.
