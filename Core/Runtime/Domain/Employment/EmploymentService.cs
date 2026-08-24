using System;
using System.Collections.Generic;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Groups;
using Vivarium.Domain.Simulation;
using Vivarium.Domain.Time;

namespace Vivarium.Domain.Employment
{
    /// <summary>Creates Employment truth and materializes its bounded recurring obligations.</summary>
    public sealed class EmploymentService
    {
        private readonly DefinitionCatalog _catalog;
        private readonly SchedulePlanner _planner;

        public EmploymentService(DefinitionCatalog catalog, SchedulePlanner planner)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        }

        public Employment Create(
            SimulationContext context,
            CharacterId employeeId,
            GroupId employerGroupId,
            AuthoredId definitionId,
            CharacterId supervisorId = default,
            IReadOnlyList<AuthoredId> assignedPatternIds = null)
        {
            WorldState world = context.World;
            if (!world.Characters.Contains(employeeId)) throw new InvalidOperationException($"Unknown employee '{employeeId}'.");
            if (supervisorId.IsSet && !world.Characters.Contains(supervisorId)) throw new InvalidOperationException($"Unknown supervisor '{supervisorId}'.");
            if (!world.Groups.TryGet(employerGroupId, out Group employer) || employer.Kind != GroupKinds.Employer)
                throw new InvalidOperationException($"Employment requires an employer group; '{employerGroupId}' is not one.");
            if (!employer.PrimaryLocationId.IsSet) throw new InvalidOperationException($"Employer '{employerGroupId}' has no workplace location.");
            if (!_catalog.EmploymentDefinitions.TryGetValue(definitionId, out EmploymentDefinition definition))
                throw new InvalidOperationException($"Unknown Employment definition '{definitionId}'.");

            IReadOnlyList<EmploymentObligationPattern> patterns = SelectPatterns(definition, assignedPatternIds);
            var employment = new Employment(
                world.RuntimeIds.Employments.Next(),
                employeeId,
                employerGroupId,
                definition.Id,
                definition.RoleId,
                employer.PrimaryLocationId,
                supervisorId,
                patterns);

            world.Employments.Add(employment.Id, employment);
            world.EmploymentIndex.Register(employment);
            world.Memberships.Join(employerGroupId, employeeId);
            if (supervisorId.IsSet) world.Memberships.Join(employerGroupId, supervisorId);
            world.BumpRevision(employment.RevisionKey);
            return employment;
        }

        public IReadOnlyList<Commitment> MaterializeCommitments(
            SimulationContext context,
            Employment employment,
            SimDuration horizon = default)
        {
            var templates = new CommitmentTemplate[employment.ObligationPatterns.Count];
            IReadOnlyList<StakeholderRef> stakeholders = employment.SupervisorId.IsSet
                ? new[] { new StakeholderRef(employment.SupervisorId.ToRef(), StakeholderRole.Authority) }
                : null;

            for (int i = 0; i < templates.Length; i++)
            {
                EmploymentObligationPattern pattern = employment.ObligationPatterns[i];
                templates[i] = new CommitmentTemplate(
                    pattern.Id,
                    pattern.CommitmentKind,
                    pattern.CycleLengthDays,
                    pattern.ActiveDaysMask,
                    pattern.StartMinuteOfDay,
                    pattern.Duration,
                    employment.WorkLocationId,
                    pattern.Priority,
                    pattern.ActivityDefinitionId,
                    pattern.StartWindow,
                    employment.Id.ToRef(),
                    pattern.AccountabilityPolicy,
                    stakeholders);
            }

            IReadOnlyList<Commitment> created = _planner.MaterializeCommitments(
                context, employment.EmployeeId, templates, horizon);
            for (int i = 0; i < created.Count; i++) _planner.TryPlanCommitmentStart(context, created[i]);
            return created;
        }

        private static IReadOnlyList<EmploymentObligationPattern> SelectPatterns(
            EmploymentDefinition definition,
            IReadOnlyList<AuthoredId> assignedPatternIds)
        {
            if (assignedPatternIds == null) return definition.ObligationPatterns;
            var selected = new List<EmploymentObligationPattern>(assignedPatternIds.Count);
            var seen = new HashSet<AuthoredId>();
            for (int i = 0; i < assignedPatternIds.Count; i++)
            {
                AuthoredId id = assignedPatternIds[i];
                if (!seen.Add(id)) throw new InvalidOperationException($"Employment pattern '{id}' was assigned twice.");
                EmploymentObligationPattern match = null;
                for (int p = 0; p < definition.ObligationPatterns.Count; p++)
                    if (definition.ObligationPatterns[p].Id == id) { match = definition.ObligationPatterns[p]; break; }
                if (match == null) throw new InvalidOperationException($"Employment definition '{definition.Id}' has no pattern '{id}'.");
                selected.Add(match);
            }
            selected.Sort((a, b) => a.Id.CompareTo(b.Id));
            return selected;
        }
    }
}
