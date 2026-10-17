using System;
using System.IO;
using Atria.Core.Data;
using NUnit.Framework;

namespace Atria.Core.Tests
{
    /// <summary>Which dataset the app loads, how it is labelled, and that failures never fall back to the demo.</summary>
    public class DatasetSelectionTests
    {
        private const string DefaultPath = "Resources/Atria/Data/building.json";
        private static string DemoJson() => File.ReadAllText(TestData.FindRepoFile(TestData.SampleRelativePath));
        private static string DraftJson() => File.ReadAllText(TestData.FindRepoFile(PrpDraftTests.DraftRelativePath));
        private static string MustNotLoadDefault() => throw new AssertionException("default dataset must not be read when an override is selected");

        [Test]
        public void NoOverride_LoadsDemoLabelledAsFictional()
        {
            DatasetLoadResult r = DatasetLoader.Load(null, null, DemoJson, DefaultPath);
            Assert.IsTrue(r.Success, r.Error);
            Assert.IsFalse(r.UsedOverride);
            Assert.AreEqual(DefaultPath, r.SourcePath);
            Assert.AreEqual("DEMO-SAMPLE", r.Building.BuildingId);
            StringAssert.DoesNotContain("Pearl Research Park", r.Building.DisplayName);
            StringAssert.StartsWith("FICTIONAL DEMO", r.Banner);
            StringAssert.StartsWith("FICTIONAL DEMO", r.WalkthroughTag);
            StringAssert.Contains("override: no", r.Summary);
            StringAssert.Contains(DefaultPath, r.Summary);
        }

        [Test]
        public void Override_LoadsPrpDraftAndLabelsItProvisional()
        {
            const string path = "Assets/Atria/MappingData/prp_g_left_draft.json";
            DatasetLoadResult r = DatasetLoader.Load(DraftJson(), path, MustNotLoadDefault, DefaultPath);
            Assert.IsTrue(r.Success, r.Error);
            Assert.IsTrue(r.UsedOverride);
            Assert.AreEqual(path, r.SourcePath);
            Assert.AreEqual("PRP-G-DRAFT-LEFT", r.Building.BuildingId);
            Assert.IsNotNull(r.Building.FindRoom("G23"));
            Assert.IsNull(r.Building.FindRoom("DEMO-122"));
            StringAssert.StartsWith("PROVISIONAL PRP DRAFT", r.Banner);
            StringAssert.StartsWith("PROVISIONAL PRP DRAFT", r.WalkthroughTag);
            StringAssert.Contains("override: yes", r.Summary);
            StringAssert.Contains("NOT verified", r.Summary);
            StringAssert.Contains(path, r.Summary);
        }

        [Test]
        public void InvalidOverrideJson_IsAnErrorAndDoesNotFallBackToDemo()
        {
            DatasetLoadResult r = DatasetLoader.Load("{ not json", "Assets/broken.json", MustNotLoadDefault, DefaultPath);
            Assert.IsFalse(r.Success);
            Assert.IsNull(r.Building);
            Assert.IsTrue(r.UsedOverride);
            StringAssert.Contains("Assets/broken.json", r.Error);
            StringAssert.StartsWith("DATA ERROR", r.Banner);
            Assert.IsNull(r.WalkthroughTag);
            StringAssert.Contains("NOT loaded", r.Summary);
        }

        [Test]
        public void UnsupportedSchemaOverride_IsAnError()
        {
            DatasetLoadResult r = DatasetLoader.Load("{\"schemaVersion\": 2}", "Assets/v2.json", MustNotLoadDefault, DefaultPath);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("schemaVersion", r.Error);
        }

        [Test]
        public void MissingDefault_IsAnError()
        {
            DatasetLoadResult r = DatasetLoader.Load(null, null, () => null, DefaultPath);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("not found", r.Error);
            StringAssert.Contains(DefaultPath, r.Error);
        }

        [Test]
        public void DatasetWithValidationErrors_LoadsButBannerSaysSo()
        {
            string broken = DraftJson().Replace("\"to\": \"HALL_C\"", "\"to\": \"GHOST_NODE\"");
            Assert.AreNotEqual(DraftJson(), broken);
            DatasetLoadResult r = DatasetLoader.Load(broken, "Assets/x.json", MustNotLoadDefault, DefaultPath);
            Assert.IsTrue(r.Success);
            Assert.IsFalse(r.Report.IsValid);
            StringAssert.Contains("validation error", r.Banner);
        }
    }
}
