# ioSender Touch - a gcode sender based on Terjeio  [ioSender](https://github.com/terjeio/ioSender) for grblHAL

Goal was to create a application that will run on a cheap mini PC with a UI design that is touch monitor friendly, and will not require a mouse or keyboard to use.
<br><br>
---

Changes in UI 
![Home Screen](media/HomeScreen.png)
![Home Screen](media/Tool.png)

Probing carried over from ioSender 
![Probe Screen](media/Probe.png)

Double clicking Text Fields in Utility -> Macro tab brings up virtual keyboard 
![Utility](media/Utility_macro.png)

Material Surface Feature 
<br>
Saves settings to app config and Gcode to NC file named QuickSurface 
![Surface](media/Surface.png)

Creates Button on  Home Screen in Utility Section to run the job 
![Surface](media/Surface2.png)

![Surface](media/Surface3.png)

<br><br>
---

### Priority issues and work
~~Reset button and logic needs to be added~~ (8/26/23 Implemented needs more testing)
<br>
~~Virtual keyboard support for probing ui (8/26/23 Implemented)~~
<br>
~~Finish utility section for material surfacing~~ (8/22/23 Implemented needs more testing)
<br>
-Create axis alignment/squaring helper under utility 
<br>
-Add support for portrait orientation 
<br><br>
