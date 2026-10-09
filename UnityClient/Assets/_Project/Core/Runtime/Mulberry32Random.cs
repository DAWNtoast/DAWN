namespace ShatteredSail.Core.Random
{
    /// <summary>
    /// Deterministic PRNG compatible with the browser demo's mulberry32 function.
    /// Keep separate instances for map, terrain, encounters, and combat streams.
    /// </summary>
    public sealed class Mulberry32Random
    {
        private uint state;

        public Mulberry32Random(uint seed)
        {
            state = seed;
        }

        /// <summary>The next draw can be resumed exactly by constructing an instance with this state.</summary>
        public uint State => state;

        public uint NextUInt()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint mixed = state;
                mixed = (mixed ^ (mixed >> 15)) * (mixed | 1u);
                mixed ^= mixed + ((mixed ^ (mixed >> 7)) * (mixed | 61u));
                return mixed ^ (mixed >> 14);
            }
        }

        public double NextDouble()
        {
            return NextUInt() / 4294967296.0d;
        }
    }
}
