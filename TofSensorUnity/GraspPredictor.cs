using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dexmo.Unity;
using System;
using System.IO;
using System.Text;

namespace Dexmo.Unity 
{
	/// <summary>
    /// Predicts hand posture between fully plate and fully power for the whole bend value range
    /// </summary>
	public class GraspPredictor 
    {
        /// <summary>
        /// Adjustment constants to make prediction in better range
        /// </summary>
        public static float[][] RemapPredictionConsts = new float[5][];

        /// <summary>
        /// Type of finger
        /// </summary>
        private FingerType fingerType;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="_fingerType">Type of finger</param>
        public GraspPredictor (FingerType _fingerType)
        {
            fingerType = _fingerType;
            RemapPredictionConsts[0] = new float[] {0f,1f,0f,1f};
            RemapPredictionConsts[1] = new float[] {-0.2f,1.5f,-0.2f,1f};
            RemapPredictionConsts[2] = new float[] {-0.2f,1.5f,-0.2f,1f};
            RemapPredictionConsts[3] = new float[] {-0.2f,1.5f,-0.2f,1f};
            RemapPredictionConsts[4] = new float[] {-0.2f,1.5f,-0.2f,1f};
        }

        /// <summary>
        /// Remap _v from [inFrom, inTo] to [outFrom, outTo], where [inFrom, inTo, outFrom, outTo] from _remapConsts
        /// </summary>
        /// <param name="_val">Value to remap</param>
        /// <param name="_remapConsts">Remap constants</param>
        private float _remap (float _val, float[] _remapConsts)
        {
            return (_val - _remapConsts[0]) / (_remapConsts[1] - _remapConsts[0]) * (_remapConsts[3] - _remapConsts[2]) + _remapConsts[2];
        }

        /// <summary>
        /// Return _x as a linear percentage between min and max, remap then clamp
        /// </summary>
        /// <param name="_max">100%</param>
        /// <param name="_min">0%</param>
        /// <param name="_x">Value to map</param>
		private float _linearPercentage (float _max, float _min, float _x)
		{
			float _percent = (_x-_min)/(_max-_min);
            _percent = _remap(_percent, RemapPredictionConsts[(int)fingerType]);
            _percent = (Single.IsNaN(_percent) || Single.IsInfinity(_percent))? 0f : _percent;
            return Mathf.Clamp(_percent,-0.2f,1f);
		}

        /// <summary>
        /// Return grasp prediction between plate(min, graspPrediction=0) and power(max, graspPrediction=100) for given graspness value
        /// </summary>
        /// <param name="_graspness">Current graspness value</param>
		public float PredictGrasp (float _graspness) {

            // Retrieve Tof reading
            float _tofreading = TofManager.TofReading[(int)fingerType];

            // Use coeffs from correct segment
            int _segmentIndMax = (_graspness < GraspLearner.Max_SegmentPoint) ? 0 : 1;
            int _segmentIndMin = (_graspness < GraspLearner.Min_SegmentPoint) ? 0 : 1;

            // Predict tof max and min based on regression (max = power, min = plate)
			float _tofGraspMax = TofRegression.RegressionPrediction(GraspPredictManager.GraspTofCoeffsMax[(int)fingerType][_segmentIndMax], _graspness);
            float _tofGraspMin = TofRegression.RegressionPrediction(GraspPredictManager.GraspTofCoeffsMin[(int)fingerType][_segmentIndMin], _graspness);

            _tofGraspMax = Mathf.Clamp(_tofGraspMax, 0, _tofGraspMax);
            _tofGraspMin = Mathf.Clamp(_tofGraspMin, 0, _tofGraspMin);
            
            // Predict grasp between max and min
            float _graspPrediction = _linearPercentage(_tofGraspMax, _tofGraspMin, _tofreading);

            //Debug.Log($"{fingerType} graspness {_graspness} tof {_tofreading} pred {_graspPrediction} [min {_tofGraspMin} max {_tofGraspMax}] from predictor");
			
            return _graspPrediction;
		}
	}
}
