using Dexmo.Unity;
using UnityEngine;
using System.Text;
using System.IO;

namespace Dexmo.Unity
{
    public static class DebugTools
    {
        public static string CsvFilePath = @"C:\Users\dexta\test_sdk\Assets\TofSensorUnity\graspnesstof.csv";
		public static DD_DataDiagram DataDiagram;
		public static GameObject dd;
        private static GameObject line1;
        private static GameObject line2;
        private static GameObject line3;
        private static GameObject line4;
        private static GameObject line5;
        public static float TestVariable;
        private static float colorgen = 0;
		public static Color NewColor()
		{
			return Color.HSVToRGB((colorgen += 0.1f) > 1 ? (colorgen - 1) : colorgen, 0.8f, 0.8f);
		}
        static DebugTools()
        {
            // Initialise DataDiagram debug
			dd = GameObject.Find("DataDiagram");
			DataDiagram = dd.GetComponent<DD_DataDiagram>();

			line1 = DataDiagram.AddLine("1", NewColor());
            line2 = DataDiagram.AddLine("2", NewColor());
            line3 = DataDiagram.AddLine("3", NewColor());
            line4 = DataDiagram.AddLine("4", NewColor());
            line5 = DataDiagram.AddLine("5", NewColor());
        }
        public static void DiagramPlot(float _channel1, float _channel2, float _channel3, float _channel4, float _channel5)
        {
			DataDiagram.InputPoint(line1, new Vector2(0.1f, _channel1));
            DataDiagram.InputPoint(line2, new Vector2(0.1f, _channel2));
            DataDiagram.InputPoint(line3, new Vector2(0.1f, _channel3));
            DataDiagram.InputPoint(line4, new Vector2(0.1f, _channel4));
            DataDiagram.InputPoint(line5, new Vector2(0.1f, _channel5));
        }
        public static void CSVtoFile(StringBuilder _sb, string _filePath, string _a = "write")
		{
			// Store CSV-formatted stringbuilder to .csv file
			if (_a=="write") {File.WriteAllText(_filePath, _sb.ToString());}
			else if (_a=="append") {File.AppendAllText(_filePath, _sb.ToString());}
			
			Debug.Log("<color=blue> Saving to csv </color> ");
		}
    }
}