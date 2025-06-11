using System;
using System.IO;
using System.Management.Automation;
using Newtonsoft.Json;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Imports a Windows image build recipe from JSON file
    /// </summary>
    [Cmdlet(VerbsData.Import, "WindowsImageBuildRecipe")]
    [OutputType(typeof(BuildRecipe))]
    public class ImportWindowsImageBuildRecipeCmdlet : PSCmdlet
    {
        /// <summary>
        /// Path to the JSON recipe file
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("FullName", "FilePath")]
        public string Path { get; set; } = null!;

        /// <summary>
        /// Validate the recipe structure after import
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter Validate { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                LoggingService.LogOperationStart(this, "ImportBuildRecipe", $"Importing recipe from: {Path}");

                // Validate input file
                var inputFile = new FileInfo(Path);
                if (!inputFile.Exists)
                {
                    var errorMessage = $"Recipe file not found: {Path}";
                    LoggingService.WriteError(this, errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        new FileNotFoundException(errorMessage),
                        "FileNotFound",
                        ErrorCategory.ObjectNotFound,
                        Path));
                    return;
                }

                LoggingService.WriteVerbose(this, $"Reading recipe file: {inputFile.FullName} ({inputFile.Length} bytes)");

                // Read JSON content
                string jsonContent;
                try
                {
                    jsonContent = File.ReadAllText(inputFile.FullName);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Failed to read recipe file: {ex.Message}";
                    LoggingService.WriteError(this, errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        ex,
                        "FileReadFailed",
                        ErrorCategory.ReadError,
                        Path));
                    return;
                }

                LoggingService.WriteVerbose(this, $"Read {jsonContent.Length} characters from recipe file");

                // Deserialize JSON to recipe object
                BuildRecipe recipe;
                try
                {
                    var jsonSettings = new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    recipe = JsonConvert.DeserializeObject<BuildRecipe>(jsonContent, jsonSettings);
                    
                    if (recipe == null)
                    {
                        throw new InvalidOperationException("Deserialization returned null recipe");
                    }
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Failed to parse recipe JSON: {ex.Message}";
                    LoggingService.WriteError(this, errorMessage);
                    ThrowTerminatingError(new ErrorRecord(
                        ex,
                        "JsonDeserializationFailed",
                        ErrorCategory.InvalidData,
                        jsonContent));
                    return;
                }

                LoggingService.WriteVerbose(this, $"Successfully deserialized recipe '{recipe.Metadata.Name}'");

                // Validate recipe if requested
                if (Validate.IsPresent)
                {
                    ValidateRecipe(recipe);
                }

                // Log recipe information
                var enabledSections = GetEnabledSectionsCount(recipe);
                LoggingService.WriteVerbose(this, $"Recipe '{recipe.Metadata.Name}' has {enabledSections} enabled sections");
                LoggingService.WriteVerbose(this, $"Recipe version: {recipe.Metadata.Version}, Author: {recipe.Metadata.Author}");
                LoggingService.WriteVerbose(this, $"Created: {recipe.Metadata.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC, Modified: {recipe.Metadata.ModifiedUtc:yyyy-MM-dd HH:mm:ss} UTC");

                // Output the recipe object
                WriteObject(recipe);

                var duration = DateTime.UtcNow - startTime;
                LoggingService.LogOperationComplete(this, "ImportBuildRecipe", duration, 
                    $"Recipe '{recipe.Metadata.Name}' imported successfully from {Path}");
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, "ImportBuildRecipe", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "ImportBuildRecipeFailed",
                    ErrorCategory.NotSpecified,
                    Path));
            }
        }

        /// <summary>
        /// Validates the recipe structure and content
        /// </summary>
        /// <param name="recipe">Recipe to validate</param>
        private void ValidateRecipe(BuildRecipe recipe)
        {
            LoggingService.WriteVerbose(this, "Validating recipe structure...");

            // Validate metadata
            if (string.IsNullOrWhiteSpace(recipe.Metadata.Name))
            {
                WriteWarning("Recipe metadata is missing a name");
            }

            if (string.IsNullOrWhiteSpace(recipe.Metadata.Version))
            {
                WriteWarning("Recipe metadata is missing a version");
            }

            // Validate enabled sections have content
            if (recipe.RemoveAppxPackages.Enabled && recipe.RemoveAppxPackages.Patterns.Count == 0)
            {
                WriteWarning("RemoveAppxPackages section is enabled but has no patterns defined");
            }

            if (recipe.CopyFiles.Enabled && recipe.CopyFiles.Items.Count == 0)
            {
                WriteWarning("CopyFiles section is enabled but has no items defined");
            }

            if (recipe.EnableFeatures.Enabled && recipe.EnableFeatures.Patterns.Count == 0)
            {
                WriteWarning("EnableFeatures section is enabled but has no patterns defined");
            }

            if (recipe.IntegrateDrivers.Enabled && recipe.IntegrateDrivers.Paths.Count == 0)
            {
                WriteWarning("IntegrateDrivers section is enabled but has no paths defined");
            }

            if (recipe.IntegrateUpdates.Enabled && recipe.IntegrateUpdates.Paths.Count == 0)
            {
                WriteWarning("IntegrateUpdates section is enabled but has no paths defined");
            }

            if (recipe.RegistryModifications.Enabled && recipe.RegistryModifications.Modifications.Count == 0)
            {
                WriteWarning("RegistryModifications section is enabled but has no modifications defined");
            }

            LoggingService.WriteVerbose(this, "Recipe validation completed");
        }

        /// <summary>
        /// Counts the number of enabled sections in the recipe
        /// </summary>
        /// <param name="recipe">Recipe to analyze</param>
        /// <returns>Number of enabled sections</returns>
        private int GetEnabledSectionsCount(BuildRecipe recipe)
        {
            int count = 0;
            if (recipe.ImageFilter.Enabled) count++;
            if (recipe.RemoveAppxPackages.Enabled) count++;
            if (recipe.CopyFiles.Enabled) count++;
            if (recipe.SetWallpapers.Enabled) count++;
            if (recipe.EnableFeatures.Enabled) count++;
            if (recipe.IntegrateDrivers.Enabled) count++;
            if (recipe.IntegrateUpdates.Enabled) count++;
            if (recipe.IntegrateFeaturesOnDemand.Enabled) count++;
            if (recipe.RegistryModifications.Enabled) count++;
            return count;
        }
    }
}
