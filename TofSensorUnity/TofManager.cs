using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;
using Dexmo.Unity;
using System.Text;
using System.IO;

namespace Dexmo.Unity
{
	/// <summary>
    /// TofManager reads ToF sensor data from serial and updates TofReading
    /// </summary>
	public class TofManager : MonoBehaviour {
        /// <summary>
        /// Reads serial data from Arduino
        /// </summary>
		private SerialPort sp;
		private static int nFingers = Enum.GetNames(typeof(FingerType)).Length;

        /// <summary>
        /// Array of smoothed sensor readings, continuously updated
        /// </summary>
		public static float[] TofReading {get; private set;}

        /// <summary>
        /// Array of unsmoothed sensor readings, continuously updated
        /// </summary>
		public static float[] RawReading {get; private set;}

        /// <summary>
        /// Array showing if sensor reading same as previous
        /// </summary>
		public static bool[] TofChanged {get; private set;}

        /// <summary>
        /// Array of previous sensor values
        /// </summary>
		private static float[] tofCache = new float[nFingers];

        /// <summary>
        /// Array of how far sensor value has fluctuated from previous, used for glitch removal
        /// </summary>		
		private static float[] tofMovements = new float[nFingers];

        /// <summary>
        /// Array storing bend values, updated by HandData
        /// </summary>
		public static float[] Graspness = new float[nFingers];

		/// <summary>
        /// Remove glitches from filtered data, returns smooth output
        /// </summary>
        /// <param name="_input">Input from filter</param>
		/// <param name="_anchor">Value to assign to, if fluctuation is small enough to be glitch</param>
		/// <param name="_fingerType">Current finger</param>
		private static float _removeGlitch(float _input, float _anchor, FingerType _fingerType)
		{
			// _fluc is fluctuation of new reading from previous
			float _fluc = _input - _anchor;
			float _absFluc = Mathf.Abs(_fluc);
			bool _flucNegative = (Mathf.Sign(_fluc) == -1f);

			// movement is how many consecutive steps away in the same direction the fluctuation has gone
			float _mvmt = tofMovements[(int)_fingerType];
			float _maxGlitchSize = 3f;
			
			if (_absFluc < 2) // = max noise when resting
			{
				if (Mathf.Abs(_mvmt) < _maxGlitchSize)
				{
					// Anchor if movement steps are consecutively small and random
					_input = _anchor;
					// Step movement based on glitch direction, only if glitch is big enough
					_mvmt = (_absFluc > 1) ? (_flucNegative ? _mvmt-1 : _mvmt+1) : _mvmt; 
				}
				else
				{
					// If stepped enough in same direction, release anchor, until fluctuation in other direction again
					_mvmt = (_mvmt<0) ? ((_flucNegative) ? _mvmt : 0) : ((_flucNegative) ? 0 : _mvmt);
				}
			}
			tofMovements[(int)_fingerType] = _mvmt;
			return _input;
		}

		/// <summary>
        /// Smooths new readings by filtering and smoothing, based on cache, and returns smoothed vals
        /// </summary>
        /// <param name="_unfilteredReadings">New incoming readings</param>
		private static float[] _cacheReturnFilteredReadings(float[] _unfilteredReadings)
		{
			float[] _tofxreading = new float[nFingers];
			for (int i =0; i < nFingers; i++)
			{
				// Apply exponential recursive smoothing filter
				float _k = 0.56f;
				_tofxreading[i] = (_k * tofCache[i]) + ((1f-_k) * _unfilteredReadings[i]);

				// Post process to remove glitches, using last cache value
				_tofxreading[i] = _removeGlitch(_tofxreading[i], tofCache[i], (FingerType)i);
			}
			return _tofxreading;
		}

		// Use this for initialization
		void Start () {
			// Start serial
			sp = new SerialPort("\\\\.\\COM18", 115200);
			sp.Open();
			sp.ReadTimeout = 1;

			TofReading = new float[nFingers];
			RawReading = new float[nFingers];
			TofChanged = new bool[nFingers];
		}
		
		// Update is called once per frame
		void Update () {
			
			// Every frame, read until end of serial buffer to get most up to date value
			string sp_read = "";
			bool _serialRead = false;
			while (true)
			{
				try 
				{
					sp_read = sp.ReadLine(); // reads first line in buffer
					_serialRead = true;
				}
				// Break when end of buffer reached
				catch (TimeoutException e) { break; }
			}

			// Parse serial data from Arduino
			string[] _sp_parsed = new string[0];
			if (_serialRead) _sp_parsed = sp_read.Split(',');
			
			// Check that serial is read and in correct format
			if (_serialRead && (_sp_parsed.Length > 1))
			{	
				for (int i=0 ; i< nFingers ; i++)
				{
					TofReading[i] =  (i < _sp_parsed.Length) ? Single.Parse(_sp_parsed[i]) : 0;
					RawReading[i] = TofReading[i];
					TofChanged[i] = Mathf.Approximately(TofReading[i], tofCache[i]) ? false : true;
				}
				
				float[] _filteredTofxReading = new float[nFingers];

				//Calculate filtered readings based on cache
				_cacheReturnFilteredReadings(TofReading).CopyTo(_filteredTofxReading, 0);

				//Store new filtered readings
				_filteredTofxReading.CopyTo(tofCache, 0);

				// Output filtered readings
				_filteredTofxReading.CopyTo(TofReading, 0);
			}
			else
			{
				// Output serial messages that aren't data
				if (_serialRead && (_sp_parsed.Length <= 1)) Debug.Log(sp_read);

				// Serial hasn't updated, read from cache
				tofCache.CopyTo(TofReading, 0);
				for (int i = 0; i < TofChanged.Length; i++) { TofChanged[i] = false; }
			}

			// Display debug data on DataDiagram
			int f = 1;
			DebugTools.DiagramPlot(Graspness[f], TofReading[f], RawReading[f], DebugTools.TestVariable, 0);
		}
	}
}