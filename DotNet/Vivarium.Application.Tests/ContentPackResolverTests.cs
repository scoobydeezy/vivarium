using System;
using Vivarium.Application.Content;
using Vivarium.Domain.Activities;
using Vivarium.Domain.Common;
using Vivarium.Domain.Content;
using Vivarium.Domain.Employment;
using Vivarium.Domain.Time;
using Xunit;

namespace Vivarium.Application.Tests
{
    public sealed class ContentPackResolverTests
    {
        private static readonly AuthoredId Working = new AuthoredId("activity.working");
        private static readonly AuthoredId Accountability = new AuthoredId("accountability.work");

        [Fact]
        public void Definition_set_is_a_snapshot_of_its_builder()
        {
            var builder = new DefinitionCatalog.Builder();
            builder.Add(Activity(Working, "Original", 5));
            DefinitionSet snapshot = builder.BuildSet();

            builder.Add(Activity(new AuthoredId("activity.later"), "Later", 10));

            Assert.Single(snapshot.Activities);
            Assert.False(snapshot.Activities.ContainsKey(new AuthoredId("activity.later")));
        }

        [Fact]
        public void Packs_add_distinct_definitions_in_load_order()
        {
            ResolvedContent resolved = ContentPackResolver.Resolve(new[]
            {
                Pack("vivarium.base", Activity(Working, "Working", 5)),
                Pack("mod.example", Activity(new AuthoredId("mod.example.activity.painting"), "Painting", 60)),
            });

            Assert.Equal(2, resolved.Catalog.Activities.Count);
            Assert.Equal("vivarium.base", resolved.Manifest.PacksInLoadOrder[0].PackId);
            Assert.Equal("mod.example", resolved.Manifest.PacksInLoadOrder[1].PackId);
        }

        [Fact]
        public void Cross_pack_collision_requires_declared_override()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                ContentPackResolver.Resolve(new[]
                {
                    Pack("vivarium.base", Activity(Working, "Working", 5)),
                    Pack("mod.example", Activity(Working, "Changed", 90)),
                }));

            Assert.Contains("without declaring an override", error.Message);
        }

        [Fact]
        public void Declared_override_replaces_the_complete_record_and_records_provenance()
        {
            ContentPackContribution replacement = Pack(
                "mod.example",
                Activity(Working, "Deliberate Replacement", 90),
                new ContentOverrideDeclaration(
                    ContentDefinitionFamily.Activity,
                    Working,
                    "vivarium.base"));

            ResolvedContent resolved = ContentPackResolver.Resolve(new[]
            {
                Pack("vivarium.base", Activity(Working, "Working", 5)),
                replacement,
            });

            ActivityDefinition winner = resolved.Catalog.Activities[Working];
            Assert.Equal("Deliberate Replacement", winner.DisplayName);
            Assert.Equal(SimDuration.FromMinutes(90), winner.DefaultDuration);
            Assert.Single(resolved.Overrides);
            Assert.Equal("vivarium.base", resolved.Overrides[0].ReplacedPackId);
            Assert.Equal("mod.example", resolved.Overrides[0].WinningPackId);
        }

        [Fact]
        public void Override_rejects_an_unexpected_current_source()
        {
            ContentPackContribution replacement = Pack(
                "mod.example",
                Activity(Working, "Changed", 90),
                new ContentOverrideDeclaration(ContentDefinitionFamily.Activity, Working, "other.pack"));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                ContentPackResolver.Resolve(new[]
                {
                    Pack("vivarium.base", Activity(Working, "Working", 5)),
                    replacement,
                }));

            Assert.Contains("expected", error.Message);
            Assert.Contains("vivarium.base", error.Message);
        }

        [Fact]
        public void Employment_reference_binds_to_policy_from_another_pack_after_overlay()
        {
            var policy = new CommitmentAccountabilityPolicy(id: Accountability);
            var baseBuilder = new DefinitionCatalog.Builder();
            baseBuilder.Add(Activity(Working, "Working", 5));
            baseBuilder.Add(policy);

            var employmentBuilder = new DefinitionCatalog.Builder();
            employmentBuilder.Add(Employment(new CommitmentAccountabilityPolicy(id: Accountability)));

            ResolvedContent resolved = ContentPackResolver.Resolve(new[]
            {
                Contribution("vivarium.base", baseBuilder),
                Contribution("mod.jobs", employmentBuilder),
            });

            Assert.Same(policy, resolved.Catalog.EmploymentDefinitions[new AuthoredId("employment.test")]
                .ObligationPatterns[0].AccountabilityPolicy);
        }

        [Fact]
        public void Employment_reference_binds_to_the_winning_policy_override()
        {
            var original = new CommitmentAccountabilityPolicy(id: Accountability);
            var replacement = new CommitmentAccountabilityPolicy(id: Accountability);
            var baseBuilder = new DefinitionCatalog.Builder();
            baseBuilder.Add(Activity(Working, "Working", 5));
            baseBuilder.Add(original);
            baseBuilder.Add(Employment(original));

            var overrideBuilder = new DefinitionCatalog.Builder();
            overrideBuilder.Add(replacement);

            ResolvedContent resolved = ContentPackResolver.Resolve(new[]
            {
                Contribution("vivarium.base", baseBuilder),
                Contribution(
                    "mod.policy",
                    overrideBuilder,
                    new ContentOverrideDeclaration(
                        ContentDefinitionFamily.CommitmentAccountabilityPolicy,
                        Accountability,
                        "vivarium.base")),
            });

            Assert.Same(replacement, resolved.Catalog.EmploymentDefinitions[new AuthoredId("employment.test")]
                .ObligationPatterns[0].AccountabilityPolicy);
        }

        [Fact]
        public void Commitment_template_reference_binds_to_policy_from_another_pack_after_overlay()
        {
            var policy = new CommitmentAccountabilityPolicy(id: Accountability);
            var baseBuilder = new DefinitionCatalog.Builder();
            baseBuilder.Add(Activity(Working, "Working", 5));
            baseBuilder.Add(policy);

            var templateBuilder = new DefinitionCatalog.Builder();
            templateBuilder.Add(Template(new CommitmentAccountabilityPolicy(id: Accountability)));

            ResolvedContent resolved = ContentPackResolver.Resolve(new[]
            {
                Contribution("vivarium.base", baseBuilder),
                Contribution("mod.routines", templateBuilder),
            });

            Assert.Same(policy, resolved.Catalog.CommitmentTemplates[new AuthoredId("commitment_template.test")]
                .AccountabilityPolicy);
        }

        private static ContentPackContribution Pack(
            string packId,
            ActivityDefinition activity,
            params ContentOverrideDeclaration[] overrides)
        {
            var builder = new DefinitionCatalog.Builder();
            builder.Add(activity);
            return new ContentPackContribution(packId, packId, 1, builder.BuildSet(), overrides);
        }

        private static ContentPackContribution Contribution(
            string packId,
            DefinitionCatalog.Builder builder,
            params ContentOverrideDeclaration[] overrides) =>
            new ContentPackContribution(packId, packId, 1, builder.BuildSet(), overrides);

        private static EmploymentDefinition Employment(CommitmentAccountabilityPolicy policy) =>
            new EmploymentDefinition(
                new AuthoredId("employment.test"),
                new AuthoredId("employment_role.test"),
                new[]
                {
                    new EmploymentObligationPattern(
                        new AuthoredId("routine.test"),
                        new AuthoredId("commitment.test"),
                        7,
                        1,
                        480,
                        SimDuration.FromMinutes(60),
                        1,
                        Working,
                        accountabilityPolicy: policy),
                });

        private static CommitmentTemplate Template(CommitmentAccountabilityPolicy policy) =>
            new CommitmentTemplate(
                new AuthoredId("commitment_template.test"),
                new AuthoredId("commitment.test"),
                7,
                1,
                480,
                SimDuration.FromMinutes(60),
                default,
                1,
                Working,
                accountabilityPolicy: policy);

        private static ActivityDefinition Activity(AuthoredId id, string name, int minutes) =>
            new ActivityDefinition(id, name, SimDuration.FromMinutes(minutes), false);
    }
}
