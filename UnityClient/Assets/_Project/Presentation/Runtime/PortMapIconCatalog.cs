using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Dedicated transparent chart markers for every playable port.</summary>
    public sealed class PortMapIconCatalog
    {
        private static readonly Dictionary<string, string> IconNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "断锚港", "port_broken_anchor_marker_v1" }, { "白贝港", "port_white_shell_marker_v1" },
            { "铜钟港", "port_bronze_bell_marker_v1" }, { "盐冠港", "port_salt_crown_marker_v1" },
            { "暮潮港", "port_twilight_tide_marker_v1" }, { "灰鸥港", "port_gray_gull_marker_v1" },
        };
        private readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Func<string, Texture2D> resourceLoader;

        public PortMapIconCatalog() : this(Resources.Load<Texture2D>) { }

        internal PortMapIconCatalog(Func<string, Texture2D> resourceLoader)
        {
            this.resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        }

        public int Count => IconNames.Count;

        /// <summary>Releases cached references, leaving live UI and Resources ownership intact.</summary>
        public void ReleaseLoadedTextures() => icons.Clear();

        public Texture2D Get(string portName)
        {
            string name = !string.IsNullOrWhiteSpace(portName) && IconNames.TryGetValue(portName, out string resolved)
                ? resolved : IconNames["断锚港"];
            return Load(name);
        }

        private Texture2D Load(string fileName)
        {
            if (icons.TryGetValue(fileName, out Texture2D texture)) return texture;
            texture = resourceLoader("PortIcons/" + fileName);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            icons.Add(fileName, texture);
            return texture;
        }
    }
}
