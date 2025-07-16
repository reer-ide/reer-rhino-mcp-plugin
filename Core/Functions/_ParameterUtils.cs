using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino.Geometry;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    /// <summary>
    /// Utility class for parsing JSON parameters commonly used across MCP function implementations
    /// </summary>
    public static class ParameterUtils
    {
        #region Basic Token Parsing

        /// <summary>
        /// Parse a JSON token as double with fallback to default value
        /// </summary>
        public static double GetDoubleFromToken(JToken token, double defaultValue = 0)
        {
            if (token != null && double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Parse a JSON token as integer with fallback to default value
        /// </summary>
        public static int GetIntFromToken(JToken token, int defaultValue = 0)
        {
            if (token != null && int.TryParse(token.ToString(), out int result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Parse a JSON token as boolean with fallback to default value
        /// </summary>
        public static bool GetBoolFromToken(JToken token, bool defaultValue = false)
        {
            if (token != null && bool.TryParse(token.ToString(), out bool result))
                return result;
            return defaultValue;
        }

        #endregion

        #region Parameter Object Parsing

        /// <summary>
        /// Get double value from JSON object by key with default fallback
        /// </summary>
        public static double GetDoubleValue(JObject parameters, string key, double defaultValue = 0)
        {
            var token = parameters[key];
            return GetDoubleFromToken(token, defaultValue);
        }

        /// <summary>
        /// Get integer value from JSON object by key with default fallback
        /// </summary>
        public static int GetIntValue(JObject parameters, string key, int defaultValue = 0)
        {
            var token = parameters[key];
            return GetIntFromToken(token, defaultValue);
        }

        /// <summary>
        /// Get boolean value from JSON object by key with default fallback
        /// </summary>
        public static bool GetBoolValue(JObject parameters, string key, bool defaultValue = false)
        {
            var token = parameters[key];
            return GetBoolFromToken(token, defaultValue);
        }

        /// <summary>
        /// Get string value from JSON object by key with default fallback
        /// </summary>
        public static string GetStringValue(JObject parameters, string key, string defaultValue = "")
        {
            var token = parameters[key];
            return token?.ToString() ?? defaultValue;
        }

        #endregion

        #region Color Parsing

        /// <summary>
        /// Parse color from JSON token as RGB array [R, G, B]
        /// </summary>
        public static int[] GetColorFromToken(JToken colorToken)
        {
            if (colorToken is JArray colorArray && colorArray.Count >= 3)
            {
                return new int[]
                {
                    GetIntFromToken(colorArray[0]),
                    GetIntFromToken(colorArray[1]),
                    GetIntFromToken(colorArray[2])
                };
            }
            return null;
        }

        /// <summary>
        /// Parse color from JSON token with validation (0-255 range)
        /// </summary>
        public static int[] GetValidatedColorFromToken(JToken colorToken)
        {
            if (colorToken is JArray colorArray && colorArray.Count >= 3)
            {
                var result = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    int colorValue = GetIntFromToken(colorArray[i]);
                    if (colorValue < 0 || colorValue > 255)
                        throw new ArgumentException($"Invalid color value: {colorValue}. Color values must be integers between 0 and 255.");
                    result[i] = colorValue;
                }
                return result;
            }
            return null;
        }

        /// <summary>
        /// Try to parse color string in various formats (#RRGGBB, "r,g,b", named colors)
        /// </summary>
        public static bool TryParseColor(string colorStr, out Color color)
        {
            color = Color.Black;
            
            if (string.IsNullOrEmpty(colorStr))
                return false;

            // Try hex format (#RRGGBB)
            if (colorStr.StartsWith("#") && colorStr.Length == 7)
            {
                try
                {
                    color = ColorTranslator.FromHtml(colorStr);
                    return true;
                }
                catch { }
            }
            
            // Try RGB format (r,g,b)
            if (colorStr.Contains(","))
            {
                var parts = colorStr.Split(',');
                if (parts.Length == 3)
                {
                    if (int.TryParse(parts[0].Trim(), out int r) && 
                        int.TryParse(parts[1].Trim(), out int g) && 
                        int.TryParse(parts[2].Trim(), out int b))
                    {
                        if (r >= 0 && r <= 255 && g >= 0 && g <= 255 && b >= 0 && b <= 255)
                        {
                            color = Color.FromArgb(r, g, b);
                            return true;
                        }
                    }
                }
            }
            
            // Try named color
            try
            {
                color = Color.FromName(colorStr);
                return color.IsKnownColor;
            }
            catch { }
            
            return false;
        }

        #endregion

        #region Geometry Parsing

        /// <summary>
        /// Parse Point3d from JSON object parameter
        /// </summary>
        public static Point3d GetPoint3d(JObject parameters, string key, Point3d defaultValue = default)
        {
            var token = parameters[key] as JArray;
            return GetPoint3dFromArray(token, defaultValue);
        }

        /// <summary>
        /// Parse Point3d from JSON array [x, y, z]
        /// </summary>
        public static Point3d GetPoint3dFromArray(JArray pointArray, Point3d defaultValue = default)
        {
            if (pointArray != null && pointArray.Count >= 3)
            {
                double x = GetDoubleFromToken(pointArray[0]);
                double y = GetDoubleFromToken(pointArray[1]);
                double z = GetDoubleFromToken(pointArray[2]);
                return new Point3d(x, y, z);
            }
            return defaultValue;
        }

        /// <summary>
        /// Parse list of Point3d from JSON array of point arrays
        /// </summary>
        public static List<Point3d> GetPoint3dList(JObject parameters, string key)
        {
            var points = new List<Point3d>();
            var token = parameters[key] as JArray;
            
            if (token != null)
            {
                foreach (var pointToken in token)
                {
                    var pointArray = pointToken as JArray;
                    if (pointArray != null && pointArray.Count >= 3)
                    {
                        points.Add(GetPoint3dFromArray(pointArray));
                    }
                }
            }
            
            return points;
        }

        /// <summary>
        /// Parse Vector3d from JSON array [x, y, z]
        /// </summary>
        public static Vector3d GetVector3dFromArray(JArray vectorArray, Vector3d defaultValue = default)
        {
            if (vectorArray != null && vectorArray.Count >= 3)
            {
                double x = GetDoubleFromToken(vectorArray[0]);
                double y = GetDoubleFromToken(vectorArray[1]);
                double z = GetDoubleFromToken(vectorArray[2]);
                return new Vector3d(x, y, z);
            }
            return defaultValue;
        }

        #endregion

        #region Array Parsing

        /// <summary>
        /// Parse integer array from JSON parameter
        /// </summary>
        public static int[] GetIntArray(JObject parameters, string key, int[] defaultValue = null)
        {
            var token = parameters[key] as JArray;
            if (token != null)
            {
                var result = new int[token.Count];
                for (int i = 0; i < token.Count; i++)
                {
                    result[i] = GetIntFromToken(token[i]);
                }
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Parse boolean array from JSON parameter
        /// </summary>
        public static bool[] GetBoolArray(JObject parameters, string key, bool[] defaultValue = null)
        {
            var token = parameters[key] as JArray;
            if (token != null)
            {
                var result = new bool[token.Count];
                for (int i = 0; i < token.Count; i++)
                {
                    result[i] = GetBoolFromToken(token[i]);
                }
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Parse double array from JSON parameter
        /// </summary>
        public static double[] GetDoubleArray(JObject parameters, string key, double[] defaultValue = null)
        {
            var token = parameters[key] as JArray;
            if (token != null)
            {
                var result = new double[token.Count];
                for (int i = 0; i < token.Count; i++)
                {
                    result[i] = GetDoubleFromToken(token[i]);
                }
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Parse string array from JSON parameter
        /// </summary>
        public static string[] GetStringArray(JObject parameters, string key, string[] defaultValue = null)
        {
            var token = parameters[key] as JArray;
            if (token != null)
            {
                var result = new string[token.Count];
                for (int i = 0; i < token.Count; i++)
                {
                    result[i] = token[i]?.ToString() ?? "";
                }
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Convert single JSON token to string list (handles both single values and arrays)
        /// </summary>
        public static List<string> CastToStringList(JToken token)
        {
            var result = new List<string>();

            if (token is JArray array)
            {
                foreach (var item in array)
                {
                    result.Add(item.ToString());
                }
            }
            else if (token != null)
            {
                result.Add(token.ToString());
            }

            return result;
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validate GUID string and return parsed GUID
        /// </summary>
        public static bool TryParseGuid(string guidStr, out Guid guid)
        {
            return Guid.TryParse(guidStr, out guid);
        }

        /// <summary>
        /// Validate required parameter exists
        /// </summary>
        public static void RequireParameter(JObject parameters, string key)
        {
            if (!parameters.ContainsKey(key) || parameters[key] == null)
            {
                throw new ArgumentException($"Required parameter '{key}' is missing or null");
            }
        }

        /// <summary>
        /// Validate required string parameter exists and is not empty
        /// </summary>
        public static void RequireStringParameter(JObject parameters, string key)
        {
            RequireParameter(parameters, key);
            string value = parameters[key].ToString();
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"Required parameter '{key}' cannot be empty");
            }
        }

        #endregion

        #region Transform Helpers

        /// <summary>
        /// Parse translation vector from parameters
        /// </summary>
        public static Transform GetTranslationTransform(JObject parameters, string key = "translation")
        {
            if (parameters[key] is JArray translationArray && translationArray.Count >= 3)
            {
                var vector = GetVector3dFromArray(translationArray);
                return Transform.Translation(vector);
            }
            return Transform.Identity;
        }

        /// <summary>
        /// Parse uniform scale transform from parameters
        /// </summary>
        public static Transform GetScaleTransform(JObject parameters, Point3d anchor, string key = "scale")
        {
            var scaleToken = parameters[key];
            if (scaleToken is JArray scaleArray && scaleArray.Count >= 3)
            {
                // Non-uniform scale [sx, sy, sz]
                var scale = GetDoubleArray(parameters, key);
                var plane = Plane.WorldXY;
                plane.Origin = anchor;
                return Transform.Scale(plane, scale[0], scale[1], scale[2]);
            }
            else if (scaleToken != null)
            {
                // Uniform scale
                double scaleValue = GetDoubleFromToken(scaleToken);
                if (scaleValue > 0 && Math.Abs(scaleValue - 1.0) > 1e-6)
                {
                    return Transform.Scale(anchor, scaleValue);
                }
            }
            return Transform.Identity;
        }

        /// <summary>
        /// Parse rotation transform from parameters
        /// </summary>
        public static Transform GetRotationTransform(JObject parameters, Point3d center, string key = "rotation")
        {
            if (parameters[key] is JArray rotationArray && rotationArray.Count >= 3)
            {
                var rotation = GetDoubleArray(parameters, key);
                
                // Apply rotations around center (in radians)
                var xform = Transform.Identity;
                if (Math.Abs(rotation[0]) > 1e-6) // X-axis rotation
                    xform = xform * Transform.Rotation(rotation[0], Vector3d.XAxis, center);
                if (Math.Abs(rotation[1]) > 1e-6) // Y-axis rotation
                    xform = xform * Transform.Rotation(rotation[1], Vector3d.YAxis, center);
                if (Math.Abs(rotation[2]) > 1e-6) // Z-axis rotation
                    xform = xform * Transform.Rotation(rotation[2], Vector3d.ZAxis, center);
                
                return xform;
            }
            return Transform.Identity;
        }

        #endregion
    }
} 