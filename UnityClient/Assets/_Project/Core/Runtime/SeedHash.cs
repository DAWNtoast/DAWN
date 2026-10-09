namespace ShatteredSail.Core.Random
{
    /// <summary>
    /// Reproduces the browser demo's UTF-16 FNV-1a seed hashing behavior.
    /// Changing this implementation invalidates existing deterministic maps.
    /// </summary>
    public static class SeedHash
    {
        public const string DefaultSeed = "SHATTERED-SAIL";

        public static uint Compute(string value)
        {
            string text = string.IsNullOrEmpty(value) ? DefaultSeed : value;
            uint hash = 2166136261u;

            unchecked
            {
                for (int index = 0; index < text.Length; index += 1)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }
            }

            return hash;
        }
    }
}

