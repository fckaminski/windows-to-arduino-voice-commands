/*******************************************************************************
* Arduino program to send/receive commands via serial
* Circuit: 1) Push-button connected to pin 2. 
* *        2) Red, green and blue LEDs connected to pins 4, 6 and 8.
*******************************************************************************/

char incomingdata;
int pin2_previous_scan;

void setup() {
   // put your setup code here, to run once:
   pinMode(4, OUTPUT);
   pinMode(6, OUTPUT);
   pinMode(8, OUTPUT);
   pinMode(2, INPUT);
   
   Serial.begin(9600); 
   pin2_previous_scan = false;

   digitalWrite(4, HIGH);
   digitalWrite(6, HIGH);
   digitalWrite(8, HIGH);
}

void loop() 
{
  // put your main code here, to run repeatedly:
   if (Serial.available() > 0)
   {  
      incomingdata = Serial.read();
    
      if (incomingdata == 'R') 
          digitalWrite(4, HIGH);  
      else if (incomingdata == 'r') 
          digitalWrite(4, LOW);  
      if (incomingdata == 'G') 
          digitalWrite(6, HIGH);  
      else if (incomingdata == 'g') 
          digitalWrite(6, LOW);  
      if (incomingdata == 'B') 
          digitalWrite(8, HIGH);  
      else if (incomingdata == 'b') 
          digitalWrite(8, LOW);     
   } 

   if(digitalRead(2) == 1 && pin2_previous_scan == 0)
       Serial.print("Input 2 has been activated");
       
   pin2_previous_scan = digitalRead(2);
   //delay(10);
}
