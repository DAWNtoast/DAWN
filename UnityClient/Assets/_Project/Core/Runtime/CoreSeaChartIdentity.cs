using System;
using System.Globalization;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreSeaChartDefinition
    {
        internal CoreSeaChartDefinition(string seed, int route, int portA, int portB, int forward, int kind, uint hash)
        {
            Seed = seed; RouteIndex = route; PortA = CoreVoyageSession.DestinationPorts[portA];
            PortB = CoreVoyageSession.DestinationPorts[portB];
            ForwardDeparturePort = forward == 0 ? PortA : PortB;
            ForwardDestinationPort = forward == 0 ? PortB : PortA;
            GeographyHash = hash; TutorialWaters = kind == 2;
        }
        public string Seed { get; }
        public int RouteIndex { get; }
        public string PortA { get; }
        public string PortB { get; }
        public string ForwardDeparturePort { get; }
        public string ForwardDestinationPort { get; }
        public uint GeographyHash { get; }
        public bool TutorialWaters { get; }
        public bool ContainsPort(string port) => port == PortA || port == PortB;
        public string OtherPort(string port) => port == PortA ? PortB : port == PortB ? PortA : string.Empty;
    }

    public sealed partial class CoreVoyageSession
    {
        public static string CreateChartSeed(int routeIndex, string departurePort, string destinationPort)
        {
            if (!TryChartPorts(routeIndex, departurePort, destinationPort, out int a, out int b, out int forward))
                throw new ArgumentException("A chart requires a known route and two distinct known ports.");
            uint salt = unchecked((uint)Guid.NewGuid().GetHashCode());
            uint hash = NewChartHash(routeIndex, a, b, forward, salt);
            return EncodeChartSeed(routeIndex, a, b, forward, 0, hash, salt);
        }

        public static bool TryParseChartSeed(string seed, out CoreSeaChartDefinition definition)
        {
            definition = null;
            string value = (seed ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Length > 100) return false;
            string[] pieces = value.Split('.');
            if (pieces.Length != 9 || pieces[0] != "SS2" ||
                !TryChartDigit(pieces[1], out int route) || !TryChartDigit(pieces[2], out int a) ||
                !TryChartDigit(pieces[3], out int b) || !TryChartDigit(pieces[4], out int forward) ||
                !TryChartDigit(pieces[5], out int kind) || route >= Routes.Count || a >= b ||
                b >= DestinationPorts.Count || forward > 1 || kind > 2 ||
                !TryChartHex(pieces[6], out uint hash) || !TryChartHex(pieces[7], out uint salt) ||
                !TryChartHex(pieces[8], out uint checksum)) return false;
            string payload = string.Join(".", pieces, 0, 8);
            if (checksum != SeedHash.Compute(payload)) return false;
            if (kind == 0 && hash != NewChartHash(route, a, b, forward, salt)) return false;
            if (kind != 0 && salt != 0) return false;
            if (kind == 2)
            {
                string from = DestinationPorts[forward == 0 ? a : b], to = DestinationPorts[forward == 0 ? b : a];
                if (route != 0 || hash != SeedHash.Compute(CoreGuidedTutorialContent.Seed + "::" + from + "::" + to)) return false;
            }
            definition = new CoreSeaChartDefinition(value, route, a, b, forward, kind, hash);
            return true;
        }

        private static bool TryChartPorts(int route, string from, string to, out int a, out int b, out int forward)
        {
            a = b = -1; forward = 0;
            for (int i = 0; i < DestinationPorts.Count; i++)
            {
                if (DestinationPorts[i] == from) a = i;
                if (DestinationPorts[i] == to) b = i;
            }
            if (route < 0 || route >= Routes.Count || a < 0 || b < 0 || a == b) return false;
            if (a > b) { int held = a; a = b; b = held; forward = 1; }
            return true;
        }

        private static uint NewChartHash(int route, int a, int b, int forward, uint salt) =>
            SeedHash.Compute("SEA-CHART-V2::" + route + "::" + a + "::" + b + "::" + forward + "::" + salt.ToString("X8", CultureInfo.InvariantCulture));

        private static string EncodeChartSeed(int route, int a, int b, int forward, int kind, uint hash, uint salt)
        {
            string payload = "SS2." + route + "." + a + "." + b + "." + forward + "." + kind + "." +
                hash.ToString("X8", CultureInfo.InvariantCulture) + "." + salt.ToString("X8", CultureInfo.InvariantCulture);
            return payload + "." + SeedHash.Compute(payload).ToString("X8", CultureInfo.InvariantCulture);
        }
        private static bool TryChartDigit(string value, out int result)
        {
            result = value.Length == 1 ? value[0] - '0' : -1;
            return result >= 0 && result <= 9;
        }
        private static bool TryChartHex(string value, out uint result)
        {
            result = 0;
            return value.Length == 8 && uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
        }
    }
}
