// Console-only adapter for catalog text and unused art types. No Unity runtime or PlayerPrefs access.
using System;
using System.IO;
namespace UnityEngine
{
 public sealed class SerializeField : Attribute {}
 public static class Mathf {public static int Clamp(int value,int min,int max)=>Math.Max(min,Math.Min(max,value));}
 public class Texture2D { public FilterMode filterMode; public TextureWrapMode wrapMode; public int anisoLevel; }
 public class TextAsset {public string text;}
 public enum FilterMode { Bilinear }
 public enum TextureWrapMode { Clamp }
 public struct Color {}
 public static class ColorUtility {public static bool TryParseHtmlString(string s,out Color c){c=new Color();return true;}}
 public static class Debug {public static void LogError(object o){throw new InvalidOperationException(Convert.ToString(o));}}
 public static class Resources {public static string Root;public static T Load<T>(string path) where T:class {if(typeof(T)==typeof(TextAsset))return new TextAsset{text=File.ReadAllText(Path.Combine(Root,path+".csv"))} as T;return null;}}
 public static class JsonUtility {public static T FromJson<T>(string s){throw new NotSupportedException("User save loading is forbidden");}public static string ToJson(object x){throw new NotSupportedException("User save writing is forbidden");}}
}
