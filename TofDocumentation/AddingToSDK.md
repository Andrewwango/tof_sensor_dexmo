## Integrating ToF Sensor in Dexmo SDK ##

**Static Classes**

- `TofManager` reads ToF sensor data from serial and updates smoothed values to a public array. Any sensor can be used in this way, instead of the ToF sensor, for example rotation sensors on Dexmo or other distance sensors.
- `GraspPredictManager` handles events for reading and writing the regression coefficients associated with the different grasp trajectories. This class also manages the learning of the maximum and minimum grasp trajectories.
- `TofRegression` provides functions for regression and regression prediction.
- `DebugTools` contains some tools for showing graphs and writing to Excel files. 

**Classes**

- `GraspPredictor` serves the purpose of predicting the grasp posture between the maximum (power grasp) and minimum (plate grasp). Unlike the other Dexmo sensors, this maximum and minimum depends on the current bend value.
- `GraspLearner` is created by `GraspPredictManager` to handle the data collection and regression for one finger and one grasp trajectory.
- `DataCollector` is a container for the sensor (`TofReading`) and bend value (`Graspness`) values collected during learning.

**Additions to existing SDK**

- `HandData.Parse`

Additions get each finger's bend value, stores in `TofManager.Graspness`, retrieves grasp prediction based on this and modifies the bend values for each joint to be outputted. PIP and DIP are now directly updated by `graspPrediction` and MCP is offset with a scaled value (try switching from plate and power with Dexmo whilst maintaining the same finger bend value).

    for (int i = 0; i < _rawHandData.Fingers.Length; ++i)
    {
        float _graspness = 100f * _rawHandData.GetJointBendData(i, 0);
        TofManager.Graspness[i] = _graspness;
        float _graspPrediction = fingerDatas[i].MyGraspPredictor.PredictGrasp(_graspness);
        float _MCPAdjust = Mathf.Clamp01((1f-_graspPrediction) * 0.2f);

        for (int j = 0; j < _rawHandData.Fingers[i].Joints.Length; ++j)
        {
            float _bendness = _rawHandData.GetJointBendData(i, j);
            fingerDatas[i].JointDatas[j].BendValue = _bendness;

            if (_graspness < 8f) break;
            
            switch ((JointType)j)
            {
	            case (JointType.MCP):
	                _bendness += _MCPAdjust;
	                break;
	            case (JointType.PIP):
	                if (_graspness > 8f) // Disallow IP change with small graspness as too unreliable (for now)
	                {
	                    if (i == (int)FingerType.THUMB)
	                    {
	                        _bendness += _MCPAdjust;
	                    }
	                    else
	                    {
	                        _bendness = _graspPrediction;
	                    }
	                }
	                break;
	            case (JointType.DIP):
	                if (_graspness > 8f) // Disallow IP change with small graspness as too unreliable (for now)
	                    _bendness = _graspPrediction;
	                break;
            }

            fingerDatas[i].JointDatas[j].BendValue = _bendness;
        }

- `TouchInteractionBehaviourDefault.OnInteractionStay`

Additions get each finger's bend value from `FingerData`, retrieves grasp prediction based on this and offsets the force feedback position so the contact looks more realistic.

    if (_touchTarget.EnableForceFeedback)
    {                        
        float _forceFeedBackBendValue = _fingerData.FingerDataOnSurface[JointType.MCP].BendValue + (_data.Value.IsInward ? forceFeedbackPositionOffset : -forceFeedbackPositionOffset);

        float _graspPrediction = _fingerData.FingerDataOnSurface.MyGraspPredictor.PredictGrasp(100f * _fingerData.FingerDataOnSurface[JointType.MCP].BendValue);
        _forceFeedBackBendValue -= Mathf.Clamp01((1f-(_graspPrediction)) * 0.2f);
        
	...
            _forceFeedBackData.Update(_forceFeedBackBendValue, _touchTarget.Stiffness, _data.Value.IsInward);
    ...
        }
	}

- `FingerData.FingerData` (all constructors)

        MyGraspPredictor = new GraspPredictor(_type);
or

    	MyGraspPredictor = new GraspPredictor(_fingerData.Type);

- *DexmoDatabase.asset*

Setting of `TuringValue` and `TuringPoint` to 1 for PIP joints and 0 for DIP joints, as now these bend values are directly set by the grasp prediction.

