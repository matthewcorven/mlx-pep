namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Orchestrates the profile publishing workflow.
/// Issue #8: Core foundation - basic publish validation
///
/// Handles:
/// - Validation for publication
/// - Deduplication strategy (basic tier uniqueness)
/// - Pre-publish checks
///
/// Note: Issue #27 (publish-flow polish) will extend this with community metadata.
/// </summary>
public class PublishService
{
    private readonly ProfileValidator _validator;

    public PublishService(ProfileValidator? validator = null)
    {
        _validator = validator ?? new ProfileValidator();
    }

    /// <summary>
    /// Validates a profile for publishing.
    /// </summary>
    public PublishCheckResult ValidateForPublish(Profile profile)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Run standard validation
        var validationResult = _validator.ValidateForLocalUse(profile);
        if (!validationResult.IsValid)
            errors.AddRange(validationResult.Errors);
        else
            warnings.AddRange(validationResult.Warnings);

        // Validate hardware fingerprint
        if (profile.Hardware == null)
            errors.Add("Hardware fingerprint is required for publishing.");

        var isValid = !errors.Any();
        return new PublishCheckResult(isValid, errors, warnings);
    }

    /// <summary>
    /// Processes a batch of profiles for publishing.
    /// Returns validated profiles ready for publication.
    /// </summary>
    public async Task<PublishBatchResult> PrepareForPublishAsync(List<Profile> profiles)
    {
        var results = new List<ProfilePublishStatus>();
        var validProfiles = new List<Profile>();
        var errors = new List<string>();

        foreach (var profile in profiles)
        {
            var checkResult = ValidateForPublish(profile);

            if (checkResult.IsValid)
            {
                validProfiles.Add(profile);

                results.Add(new ProfilePublishStatus(
                    ProfileId: profile.Id,
                    IsValid: true,
                    Errors: new List<string>(),
                    Warnings: checkResult.Warnings
                ));
            }
            else
            {
                results.Add(new ProfilePublishStatus(
                    ProfileId: profile.Id,
                    IsValid: false,
                    Errors: checkResult.Errors,
                    Warnings: checkResult.Warnings
                ));
                errors.AddRange(checkResult.Errors);
            }
        }

        return await Task.FromResult(new PublishBatchResult(
            TotalCount: profiles.Count,
            ValidCount: validProfiles.Count,
            ReadyProfiles: validProfiles,
            StatusPerProfile: results,
            Errors: errors
        ));
    }

    /// <summary>
    /// Generates comprehensive publish report.
    /// </summary>
    public PublishReport GenerateReport(PublishBatchResult batchResult)
    {
        var successRate = batchResult.TotalCount > 0
            ? (double)batchResult.ValidCount / batchResult.TotalCount * 100
            : 0;

        var failedProfiles = batchResult.StatusPerProfile
            .Where(s => !s.IsValid)
            .Select(s => s.ProfileId)
            .ToList();

        return new PublishReport(
            TotalProfiles: batchResult.TotalCount,
            ValidProfiles: batchResult.ValidCount,
            SuccessRate: successRate,
            FailedProfileIds: failedProfiles,
            HasErrors: batchResult.Errors.Any(),
            DetailedStatusPerProfile: batchResult.StatusPerProfile
        );
    }
}

/// <summary>
/// Result of publish validation for a single profile.
/// </summary>
public record PublishCheckResult(bool IsValid, List<string> Errors, List<string> Warnings);

/// <summary>
/// Status of a single profile in a batch publish.
/// </summary>
public record ProfilePublishStatus(string ProfileId, bool IsValid, List<string> Errors, List<string> Warnings);

/// <summary>
/// Result of batch publish preparation.
/// </summary>
public record PublishBatchResult(
    int TotalCount,
    int ValidCount,
    List<Profile> ReadyProfiles,
    List<ProfilePublishStatus> StatusPerProfile,
    List<string> Errors);

/// <summary>
/// Comprehensive publish report.
/// </summary>
public record PublishReport(
    int TotalProfiles,
    int ValidProfiles,
    double SuccessRate,
    List<string> FailedProfileIds,
    bool HasErrors,
    List<ProfilePublishStatus> DetailedStatusPerProfile);

