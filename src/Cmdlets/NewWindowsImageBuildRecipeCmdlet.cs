using System;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Creates a new Windows image build recipe object
    /// </summary>
    [Cmdlet(VerbsCommon.New, "WindowsImageBuildRecipe")]
    [OutputType(typeof(BuildRecipe))]
    public class NewWindowsImageBuildRecipeCmdlet : PSCmdlet
    {
        /// <summary>
        /// Recipe name
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; } = "Custom Windows Image Recipe";

        /// <summary>
        /// Recipe description
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Description { get; set; } = "A custom Windows image build recipe";

        /// <summary>
        /// Recipe version
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Recipe author
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Author { get; set; } = Environment.UserName;

        /// <summary>
        /// Include default configurations for common scenarios
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter IncludeDefaults { get; set; }

        /// <summary>
        /// Processes the cmdlet
        /// </summary>
        protected override void ProcessRecord()
        {
            var startTime = DateTime.UtcNow;

            try
            {
                LoggingService.LogOperationStart(this, "NewBuildRecipe", $"Creating recipe: {Name}");

                // Create the recipe object
                var recipe = new BuildRecipe
                {
                    Metadata = new RecipeMetadata
                    {
                        Name = Name,
                        Description = Description,
                        Version = Version,
                        Author = Author,
                        CreatedUtc = DateTime.UtcNow,
                        ModifiedUtc = DateTime.UtcNow
                    }
                };

                // Apply defaults if requested
                if (IncludeDefaults.IsPresent)
                {
                    ApplyDefaultConfiguration(recipe);
                    LoggingService.WriteVerbose(this, "Applied default configuration to recipe");
                }

                LoggingService.WriteVerbose(this, $"Created recipe '{Name}' with {GetEnabledSectionsCount(recipe)} enabled sections");

                // Output the recipe object
                WriteObject(recipe);

                var duration = DateTime.UtcNow - startTime;
                LoggingService.LogOperationComplete(this, "NewBuildRecipe", duration, $"Recipe '{Name}' created successfully");
            }
            catch (Exception ex)
            {
                LoggingService.LogOperationFailure(this, "NewBuildRecipe", ex);
                ThrowTerminatingError(new ErrorRecord(
                    ex,
                    "NewBuildRecipeFailed",
                    ErrorCategory.NotSpecified,
                    null));
            }
        }

        /// <summary>
        /// Applies default configuration to the recipe
        /// </summary>
        /// <param name="recipe">Recipe to configure</param>
        private void ApplyDefaultConfiguration(BuildRecipe recipe)
        {
            // Enable common AppX package removal
            recipe.RemoveAppxPackages.Enabled = true;
            recipe.RemoveAppxPackages.Patterns.AddRange(new[]
            {
                "*Microsoft.BingWeather*",
                "*Microsoft.GetHelp*",
                "*Microsoft.Getstarted*",
                "*Microsoft.Microsoft3DViewer*",
                "*Microsoft.MicrosoftOfficeHub*",
                "*Microsoft.MicrosoftSolitaireCollection*",
                "*Microsoft.MixedReality.Portal*",
                "*Microsoft.Office.OneNote*",
                "*Microsoft.People*",
                "*Microsoft.SkypeApp*",
                "*Microsoft.Wallet*",
                "*Microsoft.Xbox*",
                "*Microsoft.YourPhone*",
                "*Microsoft.ZuneMusic*",
                "*Microsoft.ZuneVideo*"
            });

            // Enable common Windows features
            recipe.EnableFeatures.Enabled = true;
            recipe.EnableFeatures.Patterns.AddRange(new[]
            {
                "NetFx3",
                "IIS-WebServerRole",
                "IIS-WebServer",
                "IIS-CommonHttpFeatures",
                "IIS-HttpErrors",
                "IIS-HttpRedirect",
                "IIS-ApplicationDevelopment",
                "IIS-NetFxExtensibility45",
                "IIS-HealthAndDiagnostics",
                "IIS-HttpLogging",
                "IIS-Security",
                "IIS-RequestFiltering",
                "IIS-Performance",
                "IIS-WebServerManagementTools",
                "IIS-ManagementConsole",
                "IIS-IIS6ManagementCompatibility",
                "IIS-Metabase"
            });

            // Enable basic registry modifications
            recipe.RegistryModifications.Enabled = true;
            recipe.RegistryModifications.Modifications.AddRange(new[]
            {
                new RegistryModification
                {
                    Hive = "HKEY_LOCAL_MACHINE",
                    Key = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                    ValueName = "NoAutoUpdate",
                    ValueData = 1,
                    ValueType = "DWORD"
                },
                new RegistryModification
                {
                    Hive = "HKEY_LOCAL_MACHINE",
                    Key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    ValueName = "EnableLUA",
                    ValueData = 0,
                    ValueType = "DWORD"
                }
            });

            LoggingService.WriteVerbose(this, "Applied default AppX removal patterns, Windows features, and registry modifications");
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
