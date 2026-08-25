using Vivarium.Domain.Common;

namespace Vivarium.Domain.Activities
{
    public static class ActivityNeedParameters
    {
        private const string SatisfactionPrefix = "activity.param.need_satisfaction.";

        public static AuthoredId SatisfactionOffset(AuthoredId needId) =>
            new AuthoredId(SatisfactionPrefix + needId.Value);
    }
}
