# MouseAndKeyboardToVJoy
This project has the scope to improve a keyboard and mouse's player experience when SimRacing.\
It was officially tested on **'Assetto Corsa'**, so it is not guaranteed that it will help on other sim racing games.

### Why was this created?
The project was born out of a technical limitation: existing emulation tools like FreePIE are restricted to x86 (32-bit) architectures, creating compatibility and stability issues with modern x64 (64-bit) vJoy drivers. This application is built natively in C# using WPF as a robust, 64-bit solution that interfaces directly with `vJoyInterface.dll`.

## How is it useful?
This projects gives you freedom of personalization of controls, which are not present in most of sim racing games.\
You can use **3 types control modes (you need to create for yourself)** based on your preferences:
* **Keyboard + Mouse (*Recommended*):** Throttle and Brake on the keyboard, Steering on the mouse.
* **Mouse Only:** Mouse is used entirely for Throttle, Brake and Steering.
* **Hybrid (*Advanced*):** Throttle mapped to the keyboard, while Brake and Steering are controlled via the mouse for a much more precise braking modulation in corners.
> **Advanced Customization:** The app also features deep configuration settings for steering linearity, deadzones, and pedal response curves to match your driving style perfectly.

## Important Prerequisites
To use this application, you must install the specific signed vJoy driver fork maintained by **BrunnerInnovation**. Without it, the application will not be able to feed inputs into the virtual joystick.

* **Download Driver:** You can get the official installer from the [BrunnerInnovation vJoy Releases](https://github.com/BrunnerInnovation/vJoy/releases/tag/v2.2.2.0).
* After this, you need to open **Configure vJoy** and turn on the **Enable vJoy** box that is in the left bottom corner and then you can close it.
* *Note: This project is built and optimized specifically around the 64-bit drivers provided by BrunnerInnovation.*

## Bug Reporting
While this software is continuously tested, bugs may still occur. If you encounter any unexpected behavior, crashes, or have suggestions for new features, please open an issue in the **Issues** tab or submit a report so I can look into it and push a fix!

## Where to start?
To start, you need to go to [Release](https://github.com/anatoliesir/mouse-and-keyboard-to-vjoy/releases/tag/v1.0.0), and then follow the instructions from there.

## Get the Author's Personal *Keyboard + Mouse* Settings
If you want to start with a fully tested and optimized configuration for keyboard and mouse, follow these steps:

1. Download the `presets.json` file from the `presets-examples` folder in this repository.
2. Press `Win + R` on your keyboard, type `%appdata%` and hit Enter.
3. Look for the `MouseToVJoy` folder.
4. Paste the downloaded `presets.json` file inside that folder, overwriting the existing one.
5. Restart the application and enjoy the optimized curves!
