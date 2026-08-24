# Vivarium Documentation Guide

This directory uses progressive disclosure: short routing documents lead to deeper references only
when a task needs them.

## Always start here

1. [`../README.md`](../README.md) — concise architectural contract and source precedence.
2. [`../AGENTS.md`](../AGENTS.md) — agent workflow and task-specific reading rules.
3. [`Architecture.md`](Architecture.md) — concrete repository boundaries and execution commands.

Do not read every design brief by default.

## Active sources

| Document | Purpose | Read when |
| --- | --- | --- |
| [`Architecture/Reference.md`](Architecture/Reference.md) | Complete frozen architecture, invariants, acceptance criteria, deferrals | A task changes authoritative behavior or touches an unfamiliar invariant |
| [`Architecture.md`](Architecture.md) | Repository realization and dependency boundaries | Planning any code or build change |
| [`ImplementationStatus.md`](ImplementationStatus.md) | Detailed implemented/thin capability and test map | Verifying whether a capability exists |
| [`IMPLEMENTATION_GUIDELINES.md`](IMPLEMENTATION_GUIDELINES.md) | Delivery discipline and completion checks | Implementing a vertical slice |
| [`Product/Roadmap.md`](Product/Roadmap.md) | Current priority and ordered product sequence | Selecting or evaluating next work |
| [`Product/RoadmapPhases.md`](Product/RoadmapPhases.md) | Detailed phase rationale and acceptance matrices | Implementing or reviewing a specific roadmap phase |
| [`Product/CoreIdentity.md`](Product/CoreIdentity.md) | Product identity and long-range mechanical obligations | Evaluating player power, interference, Observer beliefs, culture, habitats, or macro progression |
| [`Product/MinimumPlayableScenario.md`](Product/MinimumPlayableScenario.md) | Small-world acceptance contract | Work affecting the MVP world, routines, cast, or scenario |
| [`Product/PlayerAgencyBrief.md`](Product/PlayerAgencyBrief.md) | Locked MVP player verbs, intervention economy, management lever, and UI contract | Work affecting player Attention, agency, recap, or MVP Unity surfaces |

## Focused design references

These briefs lock topic-specific product decisions. Read the brief whose subsystem is in scope, not the
whole set.

| Topic | Source |
| --- | --- |
| Matrix-first social model, beliefs, appraisal, and social scaling | [`Design/SocialModel.md`](Design/SocialModel.md) |
| SignalFields, Considerations, ReasonChannels, and Decision explanations | [`Design/DecisionReasoning.md`](Design/DecisionReasoning.md) |
| Joint commitment feasibility, plans, deadlines, and Dissolution | [`Design/CommitmentConflict.md`](Design/CommitmentConflict.md) |
| Commitment outcomes, attribution, stakeholders, and accountability | [`Design/CommitmentAccountability.md`](Design/CommitmentAccountability.md) |

The implementation stages inside completed design briefs are historical design rationale, not the
current roadmap. Use [`Product/Roadmap.md`](Product/Roadmap.md) for present sequencing and
[`ImplementationStatus.md`](ImplementationStatus.md) for evidence of completion.

## Historical material

[`History/ImplementationCheckpoint-2026-08-23.md`](History/ImplementationCheckpoint-2026-08-23.md)
preserves the former combined implementation guide and checkpoint. It is not an active source of
priority or agent instructions.

## Maintenance ownership

- Architectural rule changes update `README.md` and the relevant section of
  `Architecture/Reference.md`.
- Repository realization or capability changes update `Architecture.md` and, when material,
  `ImplementationStatus.md`.
- Product sequencing changes update `Product/Roadmap.md` only.
- Product-identity changes update `Product/CoreIdentity.md` and then flow into architecture/roadmap
  owners where they create mechanical obligations.
- Scenario changes update `Product/MinimumPlayableScenario.md`.
- MVP player-agency decisions update `Product/PlayerAgencyBrief.md`.
- Delivery-process changes update `IMPLEMENTATION_GUIDELINES.md`.
- A focused design brief changes only when its topic-level product decision changes.

Avoid copying status narratives between documents. Link to the owner instead.
