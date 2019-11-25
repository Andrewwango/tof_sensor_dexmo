using System;
using System.Collections.Generic;
using UnityEngine;
using Dexmo.Unity;

namespace Dexmo.Unity
{
	/// <summary>
    /// Static class implementing regression functions
    /// </summary>
    public static class TofRegression
    {
        /// <summary>
        /// Returns f(x) where f is a polynomial with coefficients _coeffs
        /// </summary>
        public static float RegressionPrediction (double[] _coeffs, double x) 
		{
			double r = 0;
            double c;
			for (int i=0; i<_coeffs.Length; i++)
			{
                c=1;
                for (int j=0; j<i; j++)
                {
                    c *= x;
                }
				r += c * _coeffs[i];
			}
			return (float)r;
		}

        /// <summary>
        /// Returns list of all abs(y_i - f(x_i)) where f is a polynomial with _coeffs
        /// </summary>
        public static List<double> RegressionAbsDeviations (List<double> x, List<double> y, double[] _coeffs)
        {
            double[] _residuals = new double[x.Count];
            for (int i=0; i<x.Count; i++)
            {
                _residuals[i] = Math.Abs(y[i] - RegressionPrediction(_coeffs, x[i]));
            }
            return new List<double>(_residuals);
        }

        /// <summary>
        /// Returns sample standard deviation, sample mean optionally set to 0
        /// </summary>
        /// <param name="x">List of sample values</param>
        /// <param name="_meanIsZero">If true, function skips out calculating mean, default false</param>
        public static double SStDev (List<double> x, bool _meanIsZero=false)
        {
            double _mean = 0;
            double _len = x.Count;
            double _SS = 0;
            if (!_meanIsZero)
            {
                for (int i=0; i<_len; i++)
                {
                    _mean += x[i];
                }
                _mean /= _len;
            }

            for (int i=0; i<_len; i++)
            {
                _SS += (x[i] - _mean) * (x[i] - _mean);
            }
            return (Math.Sqrt(_SS / (_len-1)));
        }

        /// <summary>
        /// Returns coefficient of determination of regression _coeffs
        /// </summary>
        public static double RegressionRSquared (double[] x, double[] y, double[] _coeffs)
		{
			double y_bar = 0;
			double y_hat = 0;
			int n = x.Length;
			double SSR = 0;
			double SSTO = 0;

			for (int i=0; i<n; i++)
			{
				y_bar += y[i];
			}
			y_bar /= (double)n;

			for (int i=0; i<n; i++)
			{
				y_hat = (double)RegressionPrediction(_coeffs, x[i]);
				SSR += (y_hat - y_bar) * (y_hat - y_bar);
				SSTO += (y[i] - y_bar) * (y[i] - y_bar);
			}
			return SSR/SSTO;
		}

        /// <summary>
        /// Returns sum of x_i^_power over all i
        /// </summary>
        private static double _sumXPowers(List<double> x, int _power)
        {
            double s = 0;
            for (int i=0; i<x.Count; i++)
            {
                s += Math.Pow(x[i], _power);
            }
            return s;
        }

        /// <summary>
        /// Returns sum of x_i^_xPower * y_i^_yPower over all i
        /// </summary>
        private static double _sumXYPowers(List<double> x, int _xPower, List<double> y, int _yPower)
        {
            double s = 0;
            for (int i=0; i<x.Count; i++)
            {
                s += (Math.Pow(x[i], _xPower) * Math.Pow(y[i], _yPower));
            }
            return s;       
        }

        /// <summary>
        /// Returns determinant of 3x3 matrix array
        /// </summary>
        private static double _det3x3(double[,] M)
        {
            double _det = 0;
            _det += M[0,0] * (M[1,1]*M[2,2] - M[2,1]*M[1,2]);
            _det -= M[0,1] * (M[1,0]*M[2,2] - M[2,0]*M[1,2]);
            _det += M[0,2] * (M[1,0]*M[2,1] - M[1,1]*M[2,0]);
            return _det;

        }
        /// <summary>
        /// Returns matrix array where a column is replaced by _c
        /// </summary>
        private static double[,] _replaceColumn(double[,] _M, double[] _c, int _colIndex)
        {
            double[,] _MOut = new double[_M.GetLength(0), _M.GetLength(1)];
            Array.Copy(_M, 0, _MOut, 0, _M.Length);

            for (int r=0; r<_M.GetLength(0); r++) //rows
            {
                _MOut[r, _colIndex] = _c[r];
            }
            return _MOut;
        }

        /// <summary>
        /// Returns quadratic coefficients fit on lists x and y by OLS method
        /// </summary>
        public static double[] QuadraticFit(List<double> x, List<double> y)
        {
            double[,] _M = new double[3,3];
            double [] _b = new double[3];
            double[] _betas = new double[3];

            //Construct sums of powers matrix
            for (int i=0; i<3; i++)
            {
                for (int j=0; j<3; j++)
                {
                    _M[i,j] = _sumXPowers(x, i+j);
                }
            }

            //Construct sum of y*xi^k vector
            for (int i=0; i<3; i++)
            {
                _b[i] = _sumXYPowers(x, i, y, 1);
            }
            
            double _detMInv = 1f/ _det3x3(_M);

            // Use cramers rule to solve matrix equation with determinant ratios
            for (int i=0; i<3; i++)
            {
                double d = _det3x3(_replaceColumn(_M, _b, i));
                _betas[i] = _detMInv * d;//_det3x3(_replaceColumn(_M, _b, i));
            }
            return _betas;
        }

        /// <summary>
        /// Returns linear coefficients fit on lists x and y by OLS method
        /// </summary>
        public static double[] LinearFit(List<double> x, List<double> y)
        {
            // Fits linear OLS coefficients to 2D data
            double _yBar = 0;
            double _xBar = 0;
            double _xLen = x.Count;
			double _xVar = 0;
			double _xyCov = 0;

			for (int i=0; i<_xLen; i++)
			{
				_xBar += x[i];
                _yBar += y[i];
			}
			_yBar /= _xLen;
            _xBar /= _xLen;

			for (int i=0; i<_xLen; i++)
			{
				_xVar += (x[i] - _xBar) * (x[i] - _xBar);
				_xyCov += ((y[i] - _yBar) * (x[i] - _xBar));
			}

			double _slope = _xyCov / _xVar;
			double _intercept = _yBar - (_slope * _xBar);

			return (new double[2] {_intercept, _slope});
		}
    }
}