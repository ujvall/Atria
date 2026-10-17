using System;
using System.IO;
using Atria.Core.Data;

namespace Atria.Core.Tests
{
    /// <summary>Shared fixtures. Small inline graphs keep each test's geometry obvious.</summary>
    internal static class TestData
    {
        public const string SampleRelativePath = "Assets/Atria/Resources/Atria/Data/building.json";

        /// <summary>Loads the sample building shipped with the app (works in Unity and in the dotnet harness).</summary>
        public static BuildingData LoadShippedSample()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, SampleRelativePath);
                    if (File.Exists(candidate)) return BuildingDataLoader.FromJson(File.ReadAllText(candidate));
                }
            }
            throw new FileNotFoundException("Could not locate " + SampleRelativePath);
        }

        /// <summary>
        /// Plus-shaped test building: a 2.4 m wide east-west corridor crossing a north-south one at
        /// the origin, with an isolated room whose access node has no edges.
        ///
        ///            N(0,20)
        ///               |
        ///   W(-20,0) -- X(0,0) -- E(20,0)
        ///               |
        ///            S(0,-20)
        /// </summary>
        public const string PlusJson = @"{
  ""schemaVersion"": 1, ""buildingId"": ""TEST"", ""dataStatus"": ""sample"", ""walkingSpeedMps"": 1.25,
  ""floors"": [ { ""id"": ""F1"", ""elevation"": 0 } ],
  ""spaces"": [
    { ""id"": ""EW"", ""floor"": ""F1"", ""polygon"": [[-21, -1.2], [21, -1.2], [21, 1.2], [-21, 1.2]] },
    { ""id"": ""NS"", ""floor"": ""F1"", ""polygon"": [[-1.2, -21], [1.2, -21], [1.2, 21], [-1.2, 21]] },
    { ""id"": ""ISLAND"", ""floor"": ""F1"", ""polygon"": [[40, 40], [44, 40], [44, 44], [40, 44]] }
  ],
  ""nodes"": [
    { ""id"": ""X"", ""floor"": ""F1"", ""x"": 0, ""z"": 0 },
    { ""id"": ""N"", ""floor"": ""F1"", ""x"": 0, ""z"": 20 },
    { ""id"": ""S"", ""floor"": ""F1"", ""x"": 0, ""z"": -20 },
    { ""id"": ""E"", ""floor"": ""F1"", ""x"": 20, ""z"": 0 },
    { ""id"": ""W"", ""floor"": ""F1"", ""x"": -20, ""z"": 0 },
    { ""id"": ""ISO"", ""floor"": ""F1"", ""x"": 42, ""z"": 42 }
  ],
  ""edges"": [
    { ""from"": ""S"", ""to"": ""X"" }, { ""from"": ""X"", ""to"": ""N"" },
    { ""from"": ""W"", ""to"": ""X"" }, { ""from"": ""X"", ""to"": ""E"" }
  ],
  ""entrances"": [
    { ""id"": ""SOUTH"", ""node"": ""S"" }, { ""id"": ""WEST"", ""node"": ""W"" }, { ""id"": ""EAST"", ""node"": ""E"" }
  ],
  ""rooms"": [
    { ""id"": ""R-N"", ""number"": ""R-N"", ""floor"": ""F1"", ""accessNode"": ""N"", ""door"": { ""x"": 1.2, ""z"": 20 } },
    { ""id"": ""R-ISO"", ""number"": ""R-ISO"", ""floor"": ""F1"", ""accessNode"": ""ISO"" }
  ]
}";

        public static BuildingData Plus() => BuildingDataLoader.FromJson(PlusJson);
    }
}
