using System;
using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Employment
{
    /// <summary>
    /// Authoritative relationship between one employee, an employer group, a role, a workplace, and
    /// optional character authority. Obligation patterns are snapshotted for content-hot-reload safety.
    /// </summary>
    public sealed class Employment
    {
        private readonly EmploymentObligationPattern[] _obligationPatterns;

        public Employment(
            EmploymentId id,
            CharacterId employeeId,
            GroupId employerGroupId,
            AuthoredId definitionId,
            AuthoredId roleId,
            LocationId workLocationId,
            CharacterId supervisorId,
            IReadOnlyList<EmploymentObligationPattern> obligationPatterns)
        {
            if (!id.IsSet) throw new ArgumentException("An Employment needs an allocated runtime id.", nameof(id));
            if (!employeeId.IsSet) throw new ArgumentException("An Employment needs an employee.", nameof(employeeId));
            if (!employerGroupId.IsSet) throw new ArgumentException("An Employment needs an employer group.", nameof(employerGroupId));
            if (!definitionId.IsSet) throw new ArgumentException("An Employment needs a definition id.", nameof(definitionId));
            if (!roleId.IsSet) throw new ArgumentException("An Employment needs a role.", nameof(roleId));
            if (!workLocationId.IsSet) throw new ArgumentException("An Employment needs a work location.", nameof(workLocationId));
            if (supervisorId == employeeId) throw new ArgumentException("An employee cannot supervise their own Employment.", nameof(supervisorId));

            Id = id;
            EmployeeId = employeeId;
            EmployerGroupId = employerGroupId;
            DefinitionId = definitionId;
            RoleId = roleId;
            WorkLocationId = workLocationId;
            SupervisorId = supervisorId;
            _obligationPatterns = CopyPatterns(obligationPatterns);
        }

        public EmploymentId Id { get; }
        public CharacterId EmployeeId { get; }
        public GroupId EmployerGroupId { get; }
        public AuthoredId DefinitionId { get; }
        public AuthoredId RoleId { get; }
        public LocationId WorkLocationId { get; }
        public CharacterId SupervisorId { get; }
        public IReadOnlyList<EmploymentObligationPattern> ObligationPatterns => _obligationPatterns;
        public RevisionKey RevisionKey => new RevisionKey(Id.ToRef(), RevisionAspects.Employment);

        private static EmploymentObligationPattern[] CopyPatterns(IReadOnlyList<EmploymentObligationPattern> source)
        {
            if (source == null || source.Count == 0) return new EmploymentObligationPattern[0];
            var result = new EmploymentObligationPattern[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null) throw new ArgumentException("Employment obligation snapshots cannot contain null.", nameof(source));
                result[i] = source[i];
            }
            Array.Sort(result, (a, b) => a.Id.CompareTo(b.Id));
            return result;
        }
    }
}
