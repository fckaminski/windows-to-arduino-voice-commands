using System;
using System.Windows.Forms;
using System.IO.Ports;                // Communication through ports
using System.Collections.Generic;     // "Dictionary" type
using System.IO;                      // File reading
using System.Linq;                    // Select(x).ToArray()
using System.Diagnostics;             // Invoke programs
using System.Text.RegularExpressions; // Regex parsing
using System.Speech.Recognition;      //Voice recognition
using System.Speech.Synthesis;        //Voice speaking
using FireSharp.Config;               //Firebase connection https://github.com/ziyasal/FireSharp/
using FireSharp.Interfaces;
using FireSharp;
using FireSharp.Response;


namespace CsharpCode
{
    public partial class Form1 : Form
    {
        //Creating objects
        SerialPort myPort = new SerialPort();
        SpeechRecognitionEngine re = new SpeechRecognitionEngine();
        SpeechSynthesizer ss = new SpeechSynthesizer();
        Choices commands = new Choices();   //List of possible recognizable sentences are stored in this object

        //Dictionary for the <voice commands> <string to Arduino> pair 
        private Dictionary<string, string> commandsDict = new Dictionary<string, string>();

        //Dictionary for the <voice commands> <speech synth answers> pair 
        private Dictionary<string, string> speakDict = new Dictionary<string, string>();

        //Dictionary for the  <voice commands> <Firebase child/value> pair 
        private Dictionary<string, Tuple<string, string>> firebaseDict = new Dictionary<string, Tuple<string, string>>();

        //Firebase events lists
        private List<EventStreamResponse> EventsList = new List<EventStreamResponse>();

        //Paramaters read from file
        private float confidenceThreshold;
        private string firebasePath, firebaseAuth;

        IFirebaseClient firebase_arduino;

        //-------------------------------------------------------------------------------------------------------------------

        public Form1()
        {
            InitializeComponent();

            //Add available serial ports to the comboBox
            string[] portNames = SerialPort.GetPortNames();     //<-- Reads all available comPorts
            foreach (var portName in portNames)
                comboBox.Items.Add(portName);                  //<-- Adds Ports to combobox

            comboBox.Items.Add("No serial");                           //<-- Adds the "no serial" option
            comboBox.SelectedIndex = comboBox.Items.Count - 1;         //<-- Selects last entry as default


            btnStop.Enabled = false;
            btnStart.Enabled = true;
        }

        //------------- Read the parameters file and writes to dictionaries and parameters--------------
        private void TextFileParse()
        {
            string parameterHeader = "";
            string fileName = "VoiceCommands.txt";
            string[][] lines=null;

            commandsDict.Clear();
            speakDict.Clear();
            firebaseDict.Clear();

            try
            {
                //Read file as text and convert lines to an array of strings 
                lines = File.ReadAllLines(@fileName)
                                .Select(s => s.Split('\t'))
                                .ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            // Loop through the array and add the key/value pairs to the dictionary
            for (int i = 0; i < lines.Length; i++)
            {
                //Header for the voice commands/serial port commands pairs
                if (lines[i][0].Contains("--voice commands:string to Arduino"))
                    parameterHeader = "commands";

                //Header for the voice commands/voiced answer
                else if (lines[i][0].Contains("--voice commands:speech synth answers"))
                    parameterHeader = "speak";

                //Header for the general settings
                else if (lines[i][0].Contains("--parameters"))
                    parameterHeader = "settings";

                //Header for the voice commands/Firebase tree
                else if (lines[i][0].Contains("--voice commands:Firebase"))
                    parameterHeader = "firebase";

                //Adds pair key/value parsed by ":", in the dictionary corresponding to the preceding header
                else if (lines[i][0].IndexOf(':') > 0)
                {
                    /* Splits the string before and after ":". Regex was used instead of a simple "lines[i][0].Split(':')" to 
                      escape the ":" occurrences inside quotes, like the ones within the "Firebase_path" parameter  */
                    string[] colonParse = Regex.Split(lines[i][0], ":(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

                    switch (parameterHeader)
                    {

                        case "commands":
                            commandsDict.Add(colonParse[0], colonParse[1]);
                            break;
                        case "speak":
                            speakDict.Add(colonParse[0], colonParse[1]);
                            break;
                        case "firebase":
                            firebaseDict.Add(colonParse[0], new Tuple<string, string>(colonParse[1], colonParse[2]));
                            break;
                        case "settings":
                            switch (colonParse[0])
                            {
                                case "Confidence":
                                    string[] percentParse = colonParse[1].Split('%');
                                    confidenceThreshold = float.Parse(percentParse[0]) / 100;
                                    break;
                                case "Firebase_path":
                                    firebasePath = colonParse[1].Replace("\"", "");
                                    break;
                                case "Firebase_auth":
                                    firebaseAuth = colonParse[1].Replace("\"", ""); ;
                                    break;
                            }
                            break;
                    }
                }
            }
        }

        void initRecongnitonEngine()
        {
            //Reads the configuration file and fills the dictionaries
            TextFileParse();

            //Inserts the keys from the Arduino commands dictionary to the speech recognition list of choices
            foreach (KeyValuePair<string, string> entry in commandsDict)
                commands.Add(entry.Key);

            //Inserts the keys from the voice commands dictionary to the speech recognition list of choices
            foreach (KeyValuePair<string, string> entry in speakDict)
                commands.Add(entry.Key);

            commands.Add("Stop Listening");
            commands.Add("Exit Program");

            //Create Grammar object
            Grammar gr = new Grammar(new GrammarBuilder(commands));

            try
            {
                //For more information about below funtions refer to site https://docs.microsoft.com/en-us/dotnet/api/system.speech.recognition?view=netframework-4.7.2
                re.RequestRecognizerUpdate(); // Pause Speech Recognition Engine before loading commands
                re.LoadGrammarAsync(gr);
                re.SetInputToDefaultAudioDevice(); //Set input microphone
                re.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(re_SpeechRecognized);
                re.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error launching speech recognition engine. Check if the microphone connected to your computer " +
                    "is working properly and if your Windows has speech recognition.");
            }
        }

        //Event triggered when a sentence added to the "Grammar" is recognized
        void re_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string value = "";

            //Disregards recognition if confidence was below threshold or if it was spoken by the SpeechSynthesizer
            if (confidenceThreshold > e.Result.Confidence || ss.State.ToString() == "Speaking")
                return;

            //Relays corresponding char command to Arduino if recognized text is in the dictionary
            if (commandsDict.TryGetValue(e.Result.Text, out value) && comboBox.Text != "No serial")
                sendDataToArduino(value);

            //Relays corresponding char command to Firebase (synchronous write is faster)
            Tuple<string, string> tuplValue;
            if (firebaseDict.TryGetValue(e.Result.Text, out tuplValue) && checkBoxFirebase.Checked)
                firebase_arduino.Set(tuplValue.Item1, tuplValue.Item2);
                //await firebase_arduino.SetAsync("green_led", "ON");

            //Speaks answer if recognized text is in the dictionary
            if (speakDict.TryGetValue(e.Result.Text, out value))
                ss.SpeakAsync(value); // speech synthesis object is used for this purpose

            //Process voice commands
            switch (e.Result.Text)
            {
                case "Exit Program":
                    Application.Exit();
                    break;
                case "Stop Listening":
                    btnStop_Click(sender, e);
                    break;
            }

            //Shows commands in the text box
            txtCommands.Text += e.Result.Text.ToString();

            //Shows "confidence" of recognized sentence
            if (checkBoxConfid.Checked)
            {
                float confidencePercent = e.Result.Confidence * 100;
                txtCommands.Text += "   [" + confidencePercent.ToString("0") + "%]";
            }

            txtCommands.Text += Environment.NewLine;

            //Auto-scroll to the end of text box
            txtCommands.SelectionStart = txtCommands.Text.Length;
            txtCommands.ScrollToCaret();
        }

        void sendDataToArduino(string character)
        {
            if (comboBox.Text != "No serial")
                myPort.Write(character.ToString());
            //System.Threading.Thread.Sleep(100);
        }

        //Event triggered when data is received from Arduino via serial port. 
        void myPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //Allows some time to receive full string
            System.Threading.Thread.Sleep(200);

            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();

            ss.SpeakAsync(indata);

            //Since "txtCommands" are being accessed from another thread, it should be invoked like this:
            txtCommands.Invoke(new Action(() => txtCommands.Text += "[Serial]: " + indata + Environment.NewLine));
            txtCommands.Invoke(new Action(() => txtCommands.SelectionStart = txtCommands.Text.Length));
            txtCommands.Invoke(new Action(() => txtCommands.ScrollToCaret()));
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            //Stops speech recognition engine
            re.RecognizeAsyncStop();
            re.SpeechRecognized -= re_SpeechRecognized;

            ChangeText("Voice Recognition - Stopped");
            btnStop.Enabled = false;
            btnStart.Enabled = true;

            //Closes serial port
            if (comboBox.Text != "No serial")
            {
                myPort.DataReceived -= myPort_DataReceived;
                myPort.DiscardInBuffer();
                myPort.Close();
            }

            comboBox.Enabled = true;
            killFirebaseListeners();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            //Starts speech recognition engine
            initRecongnitonEngine();

            btnStop.Enabled = true;
            btnStart.Enabled = false;
            ChangeText("Voice Recognition - Started !");

            //Opens serial port ans creates event listener
            if (comboBox.Text != "No serial")
            {
                myPort.PortName = comboBox.Text; // Check in arduinoIDE what's your serial port name 
                myPort.BaudRate = 9600;          // This Rate is Same as arduino Serial.begin(9600) bits per second
                myPort.DataReceived += new SerialDataReceivedEventHandler(myPort_DataReceived);
                myPort.Open();
                myPort.DiscardInBuffer();
            }
            comboBox.Enabled = false;

            if (checkBoxFirebase.Checked)
                setFirebaseListeners();
        }

        //Opens configuration file on Notepad
        private void btnConfig_Click(object sender, EventArgs e)
        {
            Process process = new Process();

            process.StartInfo.FileName = "Notepad.exe";
            process.StartInfo.Arguments = @"VoiceCommands.txt";
            process.Start();
        }

        //Receives command from Firebase and relays it to Arduino
        //This is necessary if you have multiple instances of this application, but only one is connected to Arduino serial port
        private async void ListenToFirebaseStream(string child)
        {
            EventStreamResponse response = await firebase_arduino.OnAsync(child, changed: (sender, args, context) =>
            {
                //Since "txtCommands" are being accessed from another thread, it should be invoked like this:
                txtCommands.Invoke(new Action(() => txtCommands.Text += "[Firebase]: " + child + ":" + args.Data + Environment.NewLine));
                txtCommands.Invoke(new Action(() => txtCommands.SelectionStart = txtCommands.Text.Length));
                txtCommands.Invoke(new Action(() => txtCommands.ScrollToCaret()));

                firebaseToArduino(child, args.Data);
            });

            EventsList.Add(response);
        }

        private void firebaseToArduino(string child, string command)
        {
            //Searches the dictionary "firebaseDict" for the child/value pair received from Firebase
            var key = firebaseDict.FirstOrDefault(x => x.Value.Item1 == child && x.Value.Item2 == command).Key;

            //Sends command to Arduino corresponding to the key found
            string valueCommand = "";
            if (key != null)
                if (commandsDict.TryGetValue(key, out valueCommand))
                    sendDataToArduino(valueCommand);
        }

        private void checkBoxFirebase_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxFirebase.Checked)
            {
                firebase_arduino = GetFirebaseClient();
                if (!btnStart.Enabled)
                    setFirebaseListeners();
            }
            else
                killFirebaseListeners();
        }

        public IFirebaseClient GetFirebaseClient()
        {
            //Reads configuration file to load Firebase dictionary
            TextFileParse();

            IFirebaseConfig config = new FirebaseConfig
            {
                AuthSecret = firebaseAuth,
                BasePath = firebasePath
            };
            IFirebaseClient client = new FirebaseClient(config);
            return client;
        }

        private void setFirebaseListeners()
        {
            //Adds child/value Firebase pairs according to configuration into the list "listChildren"
            List<string> listChildren = new List<string>();
            foreach (KeyValuePair<string, Tuple<string, string>> entry in firebaseDict)
                listChildren.Add(entry.Value.Item1);

            //Creates an event to listen Firebase changes for each distinct child from the "listChildren"
            foreach (string entry in listChildren.Distinct())
            {
                ListenToFirebaseStream(entry);

                //Reads Firebase value for this child and relay it to Arduino (initial synchronization)
                FirebaseResponse response = firebase_arduino.Get(entry);
                String ledStatus = response.ResultAs<String>();
                firebaseToArduino(entry, ledStatus);
            }
        }

        //Kills existing Firebase events
        private void killFirebaseListeners()
        {
            foreach (EventStreamResponse entry in EventsList)
                entry.Dispose();
            EventsList.Clear();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
