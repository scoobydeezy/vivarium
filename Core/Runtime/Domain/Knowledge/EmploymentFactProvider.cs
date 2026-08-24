using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Simulation;

namespace Vivarium.Domain.Knowledge
{
    /// <summary>Exposes employer, role, and character authority through the ordinary fact pipeline.</summary>
    public sealed class EmploymentFactProvider : IFactProvider
    {
        private static readonly AuthoredId[] Kinds =
        {
            FactKinds.EmploymentEmployer,
            FactKinds.EmploymentRole,
            FactKinds.EmploymentSupervisor,
        };

        public IReadOnlyList<AuthoredId> ProvidedFactKinds => Kinds;

        public IEnumerable<DiscoverableClaim> ClaimsAbout(
            WorldState world,
            EntityRef subject,
            DiscoveryChannel channel)
        {
            if (subject.Kind != EntityKind.Character) yield break;
            var characterId = new CharacterId(subject.RuntimeId);
            foreach (EmploymentId employmentId in world.EmploymentIndex.OfEmployee(characterId))
            {
                if (!world.Employments.TryGet(employmentId, out Employment.Employment employment)) continue;
                yield return new DiscoverableClaim(
                    new FactKey(FactKinds.EmploymentEmployer, subject, employment.DefinitionId),
                    ObservedValue.Of(employment.EmployerGroupId.Value),
                    channel);
                yield return new DiscoverableClaim(
                    new FactKey(FactKinds.EmploymentRole, subject, employment.DefinitionId),
                    ObservedValue.Of(employment.RoleId),
                    channel);
                if (employment.SupervisorId.IsSet)
                {
                    yield return new DiscoverableClaim(
                        new FactKey(FactKinds.EmploymentSupervisor, subject, employment.DefinitionId),
                        ObservedValue.Of(employment.SupervisorId.Value),
                        channel);
                }
            }
        }
    }
}
