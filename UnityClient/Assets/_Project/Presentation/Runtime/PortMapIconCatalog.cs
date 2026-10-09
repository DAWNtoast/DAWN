using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Dedicated transparent chart markers for every playable port.</summary>
    public sealed class PortMapIconCatalog
    {
        private readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        public PortMapIconCatalog()
        {
            icons["断锚港"] = Load("port_broken_anchor_marker_v1");
            icons["白贝港"] = Load("port_white_shell_marker_v1");
            icons["铜钟港"] = Load("port_bronze_bell_marker_v1");
            icons["盐冠港"] = Load("port_salt_crown_marker_v1");
            icons["暮潮港"] = Load("port_twilight_tide_marker_v1");
            icons["灰鸥港"] = Load("port_gray_gull_marker_v1");
        }

        public int Count => icons.Count;

        public Texture2D Get(string portName)
        {
            return !string.IsNullOrWhiteSpace(portName) && icons.TryGetValue(portName, out Texture2D icon)
                ? icon
                : icons["断锚港"];
        }

        private static Texture2D Load(string fileName)
        {
            Texture2D texture = Resources.Load<Texture2D>("PortIcons/" + fileName);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }
    }
}
