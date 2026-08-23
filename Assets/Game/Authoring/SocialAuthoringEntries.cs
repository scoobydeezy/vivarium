using Vivarium.Domain.Common;
using Vivarium.Domain.Social;

namespace Vivarium.Unity.Authoring
{
    [System.Serializable]
    public struct SocialLinearEntry
    {
        public string dimensionId;
        public long coefficient;
        public string provenanceId;

        public SocialLinearTerm ToDefinition() => new SocialLinearTerm(
            new AuthoredId(dimensionId), coefficient, new AuthoredId(provenanceId));
    }

    [System.Serializable]
    public struct SocialPairwiseEntry
    {
        public string firstDimensionId;
        public string secondDimensionId;
        public long coefficient;
        public string provenanceId;

        public SocialPairwiseTerm ToDefinition() => new SocialPairwiseTerm(
            new AuthoredId(firstDimensionId),
            new AuthoredId(secondDimensionId),
            coefficient,
            new AuthoredId(provenanceId));
    }
}
