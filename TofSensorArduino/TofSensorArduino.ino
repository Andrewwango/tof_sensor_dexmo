#include <Wire.h>
#include "VL6180X.h"

uint8_t NSensors = 3;

VL6180X vxSensors [3];
uint8_t tofRanges [5] = {0,0,0,0,0};
uint8_t tofCaches [5] = {0,0,0,0,0};
uint8_t tofStatuses [5] = {0,0,0,0,0};
uint8_t shdnPins [5] = {4,2,3,0,0};
uint8_t vxAddresses [5] = {0x30, 0x31, 0x32, 0,0};

unsigned long time;
unsigned long dt;
unsigned long dt2;

void setupSensor(VL6180X *sensor, uint8_t shdn_pin, uint8_t intended_address)
{
  delay(200);
  digitalWrite(shdn_pin, HIGH);
  delay(200);
  while (! (*sensor).begin(intended_address)) 
  {
    Serial.println("ConnectionError at I2C intended " + String(intended_address) + " set " + String((*sensor).readAddress()));
    delay(500);
    while (1);
  }
  //Serial.println("yay" + String((*sensor).readAddress()));
  
}

void readSensorsSimult()
{
  //Read range simultaneously
  for (uint8_t i=0; i<NSensors; i++)
  {
    vxSensors[i].singleShot_waitSensorReady();
  }
  for (uint8_t i=0; i<NSensors; i++)
  {
    vxSensors[i].singleShot_startRange();
  }  

  //Poll each sensor in turn to check if result is ready
  boolean readys [NSensors] = {false};
  boolean allReady;
  while (true) {
    allReady = true;
    for (uint8_t i=0; i<NSensors; i++)
    {
      if (readys[i] == false) {allReady = false;}
    }

    if (allReady) {break;}

    for (uint8_t i=0; i<NSensors; i++)
    {
      if (!readys[i])
      {
        readys[i] = vxSensors[i].singleShot_checkResultReady();
      }
    }
  }

  for (uint8_t i=0; i<NSensors; i++)
  {
    tofRanges[i] = vxSensors[i].singleShot_readResult();
  }
}

void showErrors(uint8_t stat) 
{
  if  ((stat >= VL6180X_ERROR_SYSERR_1) && (stat <= VL6180X_ERROR_SYSERR_5)) { Serial.println("System error");}
  else if (stat == VL6180X_ERROR_ECEFAIL) {Serial.println("ECE failure");}
  else if (stat == VL6180X_ERROR_NOCONVERGE) {Serial.println("No convergence");}
  else if (stat == VL6180X_ERROR_RANGEIGNORE) {Serial.println("Ignoring range");}
  else if (stat == VL6180X_ERROR_SNR) {Serial.println("Signal/Noise error");}
  else if (stat == VL6180X_ERROR_RAWUFLOW) {Serial.println("Raw reading underflow");}
  else if (stat == VL6180X_ERROR_RAWOFLOW) {Serial.println("Raw reading overflow");}
  else if (stat == VL6180X_ERROR_RANGEUFLOW) {Serial.println("Range reading underflow");}
  else if (stat == VL6180X_ERROR_RANGEOFLOW) {Serial.println("Range reading overflow");}
}

void setup() 
{
  Serial.begin(115200);
  
  for (uint8_t i=0; i<NSensors; i++)
  {
    // Shutdown all sensors
    pinMode(shdnPins[i], OUTPUT);
    digitalWrite(shdnPins[i], LOW);
    vxSensors[i] = VL6180X();
  }

  // Setup sensors
  for (uint8_t j=0; j<NSensors; j++)
  {
    setupSensor(&vxSensors[j], shdnPins[j], vxAddresses[j]);
  }
  
  
  //setupSensor(&(vxSensors[2]), 3, 0x33);
  
  //setupSensor(&(vxSensors[1]), 2, 0x30);
  //setupSensor(&(vxSensors[0]), 4, 0x32);
  
}

void loop() 
{

  //time = millis();

  readSensorsSimult();
  String outString;
  
  delay(20);
  for (uint8_t i=0; i<NSensors; i++)
  {
    tofStatuses[i] = vxSensors[i].readRangeStatus();
    if (tofStatuses[i] == VL6180X_ERROR_NONE)
    {
      tofCaches[i] = tofRanges[i]; 
    }
    else
    {
      tofStatuses[i] = VL6180X_ERROR_NONE;
      tofRanges[i] = tofCaches[i];
    }

    outString += String(tofRanges[i]);
    if (i != NSensors-1) { outString += "," ;}
  }

  //showErrors(status1);

  Serial.println(outString);

  //dt = millis() - time;
  //Serial.println("parallel: " + String(dt));
}
