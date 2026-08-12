namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Orchestrates the profile publishing workflow.
/// Issue #27: profiling: publish-flow polish + community metadata
/// 
/// Handles:
/// - Validation for publication
/// - Community metadata enrichment
/// - Hardware fingerprint generation
/// - Deduplication strategy
/// - Pre-publish checks
/// </summary>
public class PublishService
{
    private readonly ProfileValidator _validator;
    private readonly ProfileReader _reader;
    private readonly HardwareProfileMatcher _matcher;

    public PublishService(
        ProfileValidator? validator = null,
        ProfileReader? reader = null,
        HardwareProfileMatcher? matcher = null)
    {
        _validator = validator ?? new ProfileValidator();
        _reader = reader ?? new ProfileReader();
        _matcher = matcher ?? new HardwareProfileMatcher();
    }

    /// <summary>
    /// Validates a profile for publishing and enriches its metadata.
    /// </summary>
    public PublishCheckResult ValidateForPublish(Profile profile, List<Profile>? existingProfiles = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Run standard validation
        var validationResult = _validator.ValidateForPublishing(profile);
        if (!validationResult.IsValid)
            errors.AddRange(validationResult.Errors);

        // Check engine-specific settings
        var engineResult = _validator.ValidateEngineSettings(profile);
        if (!engineResult.IsValid)
            errors.AddRange(engineResult.Errors);

        // Check for duplicates by dedupKey if existing profiles provided
        if (existingProfiles != null && profile.Community?.DedupKey != null)
        {
            var duplicates = _reader.FindDuplicatesByDedupKey(
                new List<Profile> { profile }.Concat(existingProfiles).ToList()
            );

            if (duplicates.ContainsKey(profile.Community.DedupKey))
            {
                var duplicateCount = duplicates[profile.Community.DedupKey].Count - 1;
                warnings.Add($"Found {duplicateCount} existing profile(s) with same dedupKey. Will keep newest.");
            }
        }

        // Validate hardware fingerprint
        if (profile.Hardware == null)
            errors.Add("Hardware fingerprint is required for publishing.");

        // Warn if community metadata is minimal
        if (profile.Community != null)
        {
            if (string.IsNullOrEmpty(profile.Community.Description))
                warnings.Add("Profile lacks description - consider adding one for discoverability.");

            if (profile.Community.Tags == null || !profile.Community.Tags.Any())
                warnings.Add("Profile lacks tags - consider adding tags for searching and filtering.");

            if (string.IsNullOrEmpty(profile.Community.HardwareFamily))
                warnings.Add("Profile lacks explicit hardware family - auto-detecting from chip name.");
        }

        var isValid = !errors.Any();
        return new PublishCheckResult(isValid, errors, warnings);
    }

    /// <summary>
    /// Enriches a profile with computed metadata for publishing.
    /// </summary>
    public Profile EnrichForPublish(Profile profile)
    {
        var enrichedCommunity = profile.Community ?? new CommunityMetadata();

        // Auto-detect hardware family if not specified
        if (string.IsNullOrEmpty(enrichedCommunity.HardwareFamily))
        {
            enrichedCommunity = enrichedCommunity with
            {
                HardwareFamily = _matcher.DetermineHardwareFamily(profile.Hardware.Chip)
            };
        }

        // Generate hardware fingerprint for deduplication if using auto-generated dedupKey
        if (string.IsNullOrEmpty(enrichedCommunity.DedupKey))
        {
            enrichedCommunity = enrichedCommunity with
            {
                DedupKey = GenerateDedupKey(profile, enrichedCommunity)
            };
        }

        return profile with { Community = enrichedCommunity };
    }

    /// <summary>
    /// Generates a deduplication key based on model, engine, hardware, and tier.
    /// </summary>
    public string GenerateDedupKey(Profile profile, CommunityMetadata? community = null)
    {
        var community_val = community ?? profile.Community;
        var model = ExtractModelName(profile.ModelHfId);
        var engine = profile.Engine.ToLowerInvariant();
        var hardware = ExtractHardwareKey(profile.Hardware.Chip);
        var tier = profile.Tier.ToLowerInvariant();

        return $"{model}-{engine}-{hardware}-{tier}".ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "");
    }

    /// <summary>
    /// Processes a batch of profiles for publishing.
    /// Returns deduplicated, enriched, and validated profiles ready for publication.
    /// </summary>
    public async Task<PublishBatchResult> PrepareForPublishAsync(
        List<Profile> profiles,
        List<Profile>? existingProfiles = null)
    {
        var results = new List<ProfilePublishStatus>();
        var validProfiles = new List<Profile>();
        var errors = new List<string>();

        foreach (var profile in profiles)
        {
            var checkResult = ValidateForPublish(profile, existingProfiles);
            
            if (checkResult.IsValid)
            {
                var enrichedProfile = EnrichForPublish(profile);
                validProfiles.Add(enrichedProfile);
                
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

        // Deduplicate by dedupKey, keeping newest
        var dedupedProfiles = _reader.DeduplicateByDedupKey(validProfiles);

        return await Task.FromResult(new PublishBatchResult(
            TotalCount: profiles.Count,
            ValidCount: validProfiles.Count,
            DeduplicatedCount: dedupedProfiles.Count,
            ReadyProfiles: dedupedProfiles,
            StatusPerProfile: results,
            Errors: errors
        ));
    }

    /// <summary>
    /// Finds similar published profiles to a candidate profile.
    /// Useful for checking against existing community profiles.
    /// </summary>
    public List<HardwareProfileMatcher.HardwareMatch> FindSimilarProfiles(
        Profile candidateProfile,
        List<Profile> publishedProfiles)
    {
        return _matcher.FindCompatibleProfiles(
            publishedProfiles,
            targetMemoryGb: candidateProfile.Hardware.MemoryGb,
            targetChip: candidateProfile.Hardware.Chip,
            targetHardwareFamily: candidateProfile.Community?.HardwareFamily
        );
    }

    /// <summary>
    /// Generates comprehensive publish report.
    /// </summary>
    public PublishReport GenerateReport(PublishBatchResult batchResult)
    {
        var dedupSavings = batchResult.ValidCount - batchResult.DeduplicatedCount;
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
            DeduplicatedProfiles: batchResult.DeduplicatedCount,
            DuplicateSavings: dedupSavings,
            SuccessRate: successRate,
            FailedProfileIds: failedProfiles,
            HasErrors: batchResult.Errors.Any(),
            DetailedStatusPerProfile: batchResult.StatusPerProfile
        );
    }

    private static string ExtractModelName(string hfId)
    {
        var parts = hfId.Split('/');
        return parts.Length > 1 ? parts[1] : hfId;
    }

    private static string ExtractHardwareKey(string chip)
    {
        return chip
            .Replace(" ", "-")
            .Replace(".", "")
            .ToLowerInvariant();
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
    int DeduplicatedCount,
    List<Profile> ReadyProfiles,
    List<ProfilePublishStatus> StatusPerProfile,
    List<string> Errors);

/// <summary>
/// Comprehensive publish report.
/// </summary>
public record PublishReport(
    int TotalProfiles,
    int ValidProfiles,
    int DeduplicatedProfiles,
    int DuplicateSavings,
    double SuccessRate,
    List<string> FailedProfileIds,
    bool HasErrors,
    List<ProfilePublishStatus> DetailedStatusPerProfile);
