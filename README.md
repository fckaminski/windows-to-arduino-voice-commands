# windows-to-arduino-voice-commands

Simple Windows application to listen to voice commands and send them to an Arduino or ESP8266 boards.

Commands to Arduino are sent via serial port. Commands to ESP8266 are sent via Firebase cloud-hosted NoSQL realtime database through your local wifi connection. 


## Firebase setup:
- Create your own realtime database with Firebase. Seee the chapter "Making a Real-time Database" from the tutorial
    https://medium.com/coinmonks/arduino-to-android-real-time-communication-for-iot-with-firebase-60df579f962

- In your Realtime Database, create the child/values pairs accondingly to the C# program, or alternatively, clicks the three dots menu, select the "Import JSON" option and then import the  "leds-export.json" file.

## Arduino setup:
- Connect three LEDs to three output pins.
- optional: connect a push-button to an input pin.
- Using Arduino IDE, open VoiceCommandsToArduino.ino project.
- Adjust the pins numbers used and upload the project to your board.
- Connect the board to the computer where VoiceToArduino.exe will run.

## ESP8266 setup:
- Connect three LEDs to three output pins.
- Using Arduino IDE, open VoiceCommandsToArduino.ino project.
- Install ESP8266 WiFi and Firebase ESP8266 Client libraries to your Arduino IDE.
- Adjust the output pins numbers used, your local WiFi SSID/password and your Firebase settings. Upload the project to your board.

## Desktop setup:
- Verify if microphone and speech recognition engine are working in your computer.
- Copy the "VoiceCommands.txt" configuration file to the application folder and adjust it to match your Firebase URL and authentication key.
- Run VoiceToArduino application (.NET Framework 4.5 required).
- Pick a serial port (for Arduino) or enable Firebase Cloud (for ESP8266). Start voice listening.


