using System;

namespace Atria.Core.Data
{
    /// <summary>Which building dataset was loaded, from where, and how the app must label it.</summary>
    public sealed class DatasetLoadResult
    {
        public BuildingData Building;
        public ValidationReport Report;
        public string SourcePath;
        public bool UsedOverride;
        /// <summary>Set when nothing could be loaded. The app shows it instead of any building.</summary>
        public string Error;

        public bool Success => Building != null;

        /// <summary>Banner for the search screen; null only for a verified dataset with no validation errors.</summary>
        public string Banner
        {
            get
            {
                if (!Success) return "DATA ERROR – " + Error;
                string banner = Building.IsVerified ? null
                    : Building.DataStatus == "sample"
                        ? "FICTIONAL DEMO – invented corridor and DEMO room numbers. This is NOT the PRP building."
                        : $"PROVISIONAL PRP DRAFT – {Building.DisplayName}. Estimated from sketches, not surveyed. NOT for real navigation.";
                if (!Report.IsValid) banner = (banner == null ? "" : banner + " ") + $"Data has {Report.Errors.Count} validation error(s) – see Console.";
                return banner;
            }
        }

        /// <summary>Short reminder kept on the walkthrough screen.</summary>
        public string WalkthroughTag =>
            !Success || Building.IsVerified ? null
            : Building.DataStatus == "sample" ? "FICTIONAL DEMO – not the PRP building" : "PROVISIONAL PRP DRAFT – not surveyed";

        public string Summary =>
            !Success ? $"[Atria] Dataset NOT loaded from {SourcePath} (override: {(UsedOverride ? "yes" : "no")}): {Error}"
            : $"[Atria] Dataset '{Building.DisplayName}' ({Building.BuildingId}), status '{Building.DataStatus}'{(Building.IsVerified ? "" : " (NOT verified)")}, " +
              $"source {SourcePath}, override: {(UsedOverride ? "yes" : "no")}, validation: {Report.Errors.Count} error(s), {Report.Warnings.Count} warning(s).";
    }

    /// <summary>
    /// Chooses between a development override and the packaged default dataset. An override that
    /// fails to load is reported as an error; it never silently falls back to the demo.
    /// </summary>
    public static class DatasetLoader
    {
        /// <param name="overrideJson">Override file contents, or null when no override is selected.</param>
        /// <param name="loadDefaultJson">Reads the default dataset; returns null if it is missing. Not called when an override is selected.</param>
        public static DatasetLoadResult Load(string overrideJson, string overridePath, Func<string> loadDefaultJson, string defaultPath)
        {
            bool useOverride = overrideJson != null;
            var result = new DatasetLoadResult { UsedOverride = useOverride, SourcePath = useOverride ? overridePath : defaultPath };
            string json = useOverride ? overrideJson : loadDefaultJson();
            if (json == null) { result.Error = $"building data file not found ({result.SourcePath})"; return result; }
            try
            {
                result.Building = BuildingDataLoader.FromJson(json);
            }
            catch (FormatException e)
            {
                result.Error = $"{result.SourcePath} could not be read: {e.Message}";
                return result;
            }
            result.Report = BuildingValidator.Validate(result.Building);
            return result;
        }
    }
}
