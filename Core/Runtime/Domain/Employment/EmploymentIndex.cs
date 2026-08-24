using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Employment
{
    /// <summary>Rebuildable, deterministic lookup by employee and employer.</summary>
    public sealed class EmploymentIndex
    {
        private readonly SortedDictionary<CharacterId, SortedSet<EmploymentId>> _byEmployee =
            new SortedDictionary<CharacterId, SortedSet<EmploymentId>>();
        private readonly SortedDictionary<GroupId, SortedSet<EmploymentId>> _byEmployer =
            new SortedDictionary<GroupId, SortedSet<EmploymentId>>();

        public void Register(Employment employment)
        {
            Add(_byEmployee, employment.EmployeeId, employment.Id);
            Add(_byEmployer, employment.EmployerGroupId, employment.Id);
        }

        public IReadOnlyCollection<EmploymentId> OfEmployee(CharacterId employeeId) =>
            _byEmployee.TryGetValue(employeeId, out SortedSet<EmploymentId> ids)
                ? ids
                : (IReadOnlyCollection<EmploymentId>)new EmploymentId[0];

        public IReadOnlyCollection<EmploymentId> OfEmployer(GroupId employerId) =>
            _byEmployer.TryGetValue(employerId, out SortedSet<EmploymentId> ids)
                ? ids
                : (IReadOnlyCollection<EmploymentId>)new EmploymentId[0];

        public void Clear()
        {
            _byEmployee.Clear();
            _byEmployer.Clear();
        }

        private static void Add<TKey>(SortedDictionary<TKey, SortedSet<EmploymentId>> index, TKey key, EmploymentId id)
        {
            if (!index.TryGetValue(key, out SortedSet<EmploymentId> ids))
            {
                ids = new SortedSet<EmploymentId>();
                index.Add(key, ids);
            }
            ids.Add(id);
        }
    }
}
