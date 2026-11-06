#if UNITY_EDITOR
using System.Runtime.CompilerServices;

// EditMode fixtures arrange internal battle state without exposing it to players.
// Player builds do not grant any friend-assembly access.
[assembly: InternalsVisibleTo("ShatteredSail.Core.Tests")]
#endif
