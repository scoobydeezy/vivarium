using System.Collections.Generic;

namespace Vivarium.Application.Persistence
{
    public sealed class AffectData
    {
        public string Kind;
        public ProgressionData Progression = new ProgressionData();
        public int Revision;
    }

    public sealed class SocialTermData
    {
        public string FirstDimension;
        public string SecondDimension;
        public long Coefficient;
        public string Provenance;
    }

    public sealed class IdealFactorData
    {
        public string Id;
        public string Provenance;
        public List<SocialTermData> Coefficients = new List<SocialTermData>();
    }

    public sealed class AppraisalContextModifierData
    {
        public string ContextId;
        public long BiasDelta;
        public string Provenance;
        public List<SocialTermData> LinearDeltas = new List<SocialTermData>();
        public List<SocialTermData> PairwiseDeltas = new List<SocialTermData>();
        public List<AuthoredLongData> IdealPointDelta = new List<AuthoredLongData>();
        public List<IdealFactorData> IdealFactorDeltas = new List<IdealFactorData>();
    }

    public sealed class AppraisalFieldData
    {
        public string LensId;
        public long Bias;
        public string CalibrationProfileId;
        public int Revision;
        public List<SocialTermData> LinearTerms = new List<SocialTermData>();
        public List<SocialTermData> PairwiseTerms = new List<SocialTermData>();
        public List<AuthoredLongData> IdealPoint = new List<AuthoredLongData>();
        public List<IdealFactorData> IdealFactors = new List<IdealFactorData>();
        public List<AppraisalContextModifierData> ContextModifiers = new List<AppraisalContextModifierData>();
    }

    public sealed class CovarianceData
    {
        public string FirstDimension;
        public string SecondDimension;
        public long Value;
    }

    public sealed class SocialBeliefData
    {
        public int ObserverKind;
        public int ObserverCharacterId;
        public int TargetCharacterId;
        public int EvidenceRevision;
        public int Retention;
        public long LastUpdatedAtMinutes;
        public List<AuthoredLongData> Mean = new List<AuthoredLongData>();
        public List<CovarianceData> Covariance = new List<CovarianceData>();
    }

    public sealed class RelationshipChannelData
    {
        public string ChannelId;
        public ProgressionData Progression = new ProgressionData();
    }

    public sealed class RelationshipMemoryData
    {
        public string MemoryKind;
        public long OccurredAtMinutes;
        public string ExplanationId;
        public int SourceHistoryEntryId;
        public int SourceOutcomeId;
        public List<AuthoredLongData> ChannelEffects = new List<AuthoredLongData>();
    }

    public sealed class DirectionalRelationshipData
    {
        public int ObserverId;
        public int TargetId;
        public int Familiarity;
        public ProgressionData FamiliarityProgression = new ProgressionData();
        public bool HasFamiliarityProgression;
        public long ExposureMinutes;
        public long LastInteractionAtMinutes = -1;
        public int Revision;
        public List<RelationshipChannelData> Channels = new List<RelationshipChannelData>();
        public List<RelationshipMemoryData> Memories = new List<RelationshipMemoryData>();
    }
}
