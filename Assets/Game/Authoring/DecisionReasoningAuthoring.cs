using System.Collections.Generic;
using Vivarium.Domain.Common;
using Vivarium.Domain.Decisions;
using Vivarium.Domain.Evaluation;

namespace Vivarium.Unity.Authoring
{
    [System.Serializable]
    public struct DecisionParameterValueEntry
    {
        public string key;
        public DecisionParameterKind kind;
        public long integer;
        public string authoredId;
        public EntityKind entityKind;
        public int runtimeId;

        public DecisionParameterValue ToValue()
        {
            switch (kind)
            {
                case DecisionParameterKind.Integer: return DecisionParameterValue.FromInteger(integer);
                case DecisionParameterKind.AuthoredId: return DecisionParameterValue.FromAuthoredId(new AuthoredId(authoredId));
                case DecisionParameterKind.Entity: return DecisionParameterValue.FromEntity(new EntityRef(entityKind, runtimeId));
                default: throw new System.InvalidOperationException("Unknown Decision parameter kind " + kind);
            }
        }
    }

    [System.Serializable]
    public struct DecisionReasoningProgramEntry
    {
        public CompiledConsiderationBindingEntry[] bindings;
        public bool IsConfigured => bindings != null && bindings.Length > 0;
        public DecisionReasoningProgram ToDefinition()
        {
            var result = new CompiledConsiderationBinding[bindings?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = bindings[i].ToDefinition();
            return new DecisionReasoningProgram(result);
        }
    }

    [System.Serializable]
    public struct CompiledConsiderationBindingEntry
    {
        public string bindingId;
        public string considerationId;
        public int definitionVersion;
        public ConsiderationParameterEntry[] parameterSchema;
        public CompiledParameterBindingEntry[] parameterBindings;
        public DecisionSignalRequestEntry[] signals;
        public SignalFieldEntry field;
        public string reasonChannelId;
        public ReasonChannelConsolidationPolicy consolidationPolicy;
        public string scaleId;
        public ReasonDieThresholdEntry[] scaleThresholds;
        public string categoryId;
        public string positiveLabelId;
        public string negativeLabelId;
        public InfluenceVisibility visibility;

        public CompiledConsiderationBinding ToDefinition()
        {
            var schema = new ConsiderationParameter[parameterSchema?.Length ?? 0];
            for (int i = 0; i < schema.Length; i++) schema[i] = parameterSchema[i].ToDefinition();
            var parameters = new CompiledParameterBinding[parameterBindings?.Length ?? 0];
            for (int i = 0; i < parameters.Length; i++) parameters[i] = parameterBindings[i].ToDefinition();
            var requests = new DecisionSignalRequest[signals?.Length ?? 0];
            for (int i = 0; i < requests.Length; i++) requests[i] = signals[i].ToDefinition();
            var thresholds = new ReasonDieThreshold[scaleThresholds?.Length ?? 0];
            for (int i = 0; i < thresholds.Length; i++) thresholds[i] = scaleThresholds[i].ToDefinition();
            return new CompiledConsiderationBinding(
                new AuthoredId(bindingId), new AuthoredId(considerationId), definitionVersion,
                schema, parameters, requests, field.ToDefinition(),
                new ReasonChannelDefinition(new AuthoredId(reasonChannelId), consolidationPolicy),
                new ReasonScaleProfile(new AuthoredId(scaleId), thresholds),
                new AuthoredId(categoryId), new AuthoredId(positiveLabelId),
                new AuthoredId(negativeLabelId), visibility);
        }
    }

    [System.Serializable]
    public struct ConsiderationParameterEntry
    {
        public string parameterId;
        public DecisionParameterKind kind;
        public bool required;
        public ConsiderationParameter ToDefinition() =>
            new ConsiderationParameter(new AuthoredId(parameterId), kind, required);
    }

    [System.Serializable]
    public struct CompiledParameterBindingEntry
    {
        public string parameterId;
        public ParameterBindingSource source;
        public string sourceParameterId;
        public DecisionParameterValueEntry literal;
        public CompiledParameterBinding ToDefinition() => new CompiledParameterBinding(
            new AuthoredId(parameterId), source, new AuthoredId(sourceParameterId), literal.ToValue());
    }

    [System.Serializable]
    public struct DecisionSignalRequestEntry
    {
        public string signalId;
        public string providerId;
        public DecisionSignalRequest ToDefinition() =>
            new DecisionSignalRequest(new AuthoredId(signalId), new AuthoredId(providerId));
    }

    [System.Serializable]
    public struct ReasonDieThresholdEntry
    {
        public long minimumMagnitude;
        public int dieSides;
        public ReasonDieThreshold ToDefinition() => new ReasonDieThreshold(minimumMagnitude, new Die(dieSides));
    }

    [System.Serializable]
    public struct SignalFieldEntry
    {
        public string authoredId;
        public long bias;
        public int revision;
        public SignalLinearTermEntry[] linearTerms;
        public SignalPairwiseTermEntry[] pairwiseTerms;
        public SignalIdealPointEntry[] idealPoint;
        public SignalIdealFactorEntry[] idealFactors;

        public SignalFieldDefinition ToDefinition()
        {
            var linear = new SignalLinearTerm[linearTerms?.Length ?? 0];
            for (int i = 0; i < linear.Length; i++) linear[i] = linearTerms[i].ToDefinition();
            var pairwise = new SignalPairwiseTerm[pairwiseTerms?.Length ?? 0];
            for (int i = 0; i < pairwise.Length; i++) pairwise[i] = pairwiseTerms[i].ToDefinition();
            var ideal = new SortedDictionary<AuthoredId, long>();
            for (int i = 0; i < (idealPoint?.Length ?? 0); i++) ideal[new AuthoredId(idealPoint[i].signalId)] = idealPoint[i].value;
            var factors = new SignalIdealFactor[idealFactors?.Length ?? 0];
            for (int i = 0; i < factors.Length; i++) factors[i] = idealFactors[i].ToDefinition();
            return new SignalFieldDefinition(new AuthoredId(authoredId), bias, linear, pairwise, ideal, factors, revision);
        }
    }

    [System.Serializable]
    public struct SignalLinearTermEntry
    {
        public string signalId;
        public long coefficient;
        public string provenanceId;
        public SignalLinearTerm ToDefinition() =>
            new SignalLinearTerm(new AuthoredId(signalId), coefficient, new AuthoredId(provenanceId));
    }

    [System.Serializable]
    public struct SignalPairwiseTermEntry
    {
        public string firstSignalId;
        public string secondSignalId;
        public long coefficient;
        public string provenanceId;
        public SignalPairwiseTerm ToDefinition() => new SignalPairwiseTerm(
            new AuthoredId(firstSignalId), new AuthoredId(secondSignalId), coefficient, new AuthoredId(provenanceId));
    }

    [System.Serializable]
    public struct SignalIdealPointEntry { public string signalId; public long value; }

    [System.Serializable]
    public struct SignalIdealFactorEntry
    {
        public string authoredId;
        public string provenanceId;
        public SignalLinearTermEntry[] coefficients;
        public SignalIdealFactor ToDefinition()
        {
            var result = new SignalLinearTerm[coefficients?.Length ?? 0];
            for (int i = 0; i < result.Length; i++) result[i] = coefficients[i].ToDefinition();
            return new SignalIdealFactor(new AuthoredId(authoredId), result, new AuthoredId(provenanceId));
        }
    }
}
